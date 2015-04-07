using System;
using System.Collections.Generic;
using System.Linq;
using Umbraco.Core.Events;
using Umbraco.Core.Logging;
using Umbraco.Core.Models;
using Umbraco.Core.Models.EntityBase;
using Umbraco.Core.Persistence;
using Umbraco.Core.Persistence.Querying;
using Umbraco.Core.Persistence.Repositories;
using Umbraco.Core.Persistence.UnitOfWork;

namespace Umbraco.Core.Services
{
    /// <summary>
    /// Provides a base class for <see cref="ContentTypeService"/>, <see cref="MediaTypeService"/> and <see cref="MemberTypeService"/>.
    /// </summary>
    /// <typeparam name="TRepository">The type of the underlying repository.</typeparam>
    /// <typeparam name="TItem">The type of the item.</typeparam>
    internal abstract class ContentTypeServiceBase<TRepository, TItem> : RepositoryService, IContentTypeServiceBase<TItem>
        where TRepository : ContentTypeBaseRepository<TItem>, IDisposable
        where TItem : class, IContentTypeComposition
    {
        protected ContentTypeServiceBase(IDatabaseUnitOfWorkProvider provider, RepositoryFactory repositoryFactory, ILogger logger, LockingRepository<TRepository> lrepo)
            : base(provider, repositoryFactory, logger)
        {
            LRepo = lrepo;
        }

        #region Locking

        protected readonly LockingRepository<TRepository> LRepo;

        #endregion

        #region Validation

        public void Validate(IContentTypeComposition compo)
        {
            // locking the content types but it really does not matter
            // because it locks ALL content types incl. medias & members &...
            LRepo.WithReadLocked(xr => ValidateLocked(xr.Repository, compo));
        }

        private void ValidateLocked(TRepository repository, IContentTypeComposition compositionContentType)
        {
            // performs business-level validation of the composition
            // should ensure that it is absolutely safe to save the composition

            // eg maybe a property has been added, with an alias that's OK (no conflict with ancestors)
            // but that cannot be used (conflict with descendants)

            var allContentTypes = repository.GetAll().Cast<IContentTypeComposition>().ToArray();

            var compositionAliases = compositionContentType.CompositionAliases();
            var compositions = allContentTypes.Where(x => compositionAliases.Any(y => x.Alias.Equals(y)));
            var propertyTypeAliases = compositionContentType.PropertyTypes.Select(x => x.Alias.ToLowerInvariant()).ToArray();
            var indirectReferences = allContentTypes.Where(x => x.ContentTypeComposition.Any(y => y.Id == compositionContentType.Id));
            var comparer = new DelegateEqualityComparer<IContentTypeComposition>((x, y) => x.Id == y.Id, x => x.Id);
            var dependencies = new HashSet<IContentTypeComposition>(compositions, comparer);
            var stack = new Stack<IContentTypeComposition>();
            indirectReferences.ForEach(stack.Push); // push indirect references to a stack, so we can add recursively
            while (stack.Count > 0)
            {
                var indirectReference = stack.Pop();
                dependencies.Add(indirectReference);
                // get all compositions for the current indirect reference
                var directReferences = indirectReference.ContentTypeComposition;

                foreach (var directReference in directReferences)
                {
                    if (directReference.Id == compositionContentType.Id || directReference.Alias.Equals(compositionContentType.Alias)) continue;
                    dependencies.Add(directReference);
                    // a direct reference has compositions of its own - these also need to be taken into account
                    var directReferenceGraph = directReference.CompositionAliases();
                    allContentTypes.Where(x => directReferenceGraph.Any(y => x.Alias.Equals(y, StringComparison.InvariantCultureIgnoreCase))).ForEach(c => dependencies.Add(c));
                }
                // recursive lookup of indirect references
                allContentTypes.Where(x => x.ContentTypeComposition.Any(y => y.Id == indirectReference.Id)).ForEach(stack.Push);
            }

            foreach (var dependency in dependencies)
            {
                if (dependency.Id == compositionContentType.Id) continue;
                var contentTypeDependency = allContentTypes.FirstOrDefault(x => x.Alias.Equals(dependency.Alias, StringComparison.InvariantCultureIgnoreCase));
                if (contentTypeDependency == null) continue;
                var intersect = contentTypeDependency.PropertyTypes.Select(x => x.Alias.ToLowerInvariant()).Intersect(propertyTypeAliases).ToArray();
                if (intersect.Length == 0) continue;

                var message = string.Format("The following PropertyType aliases from the current ContentType conflict with existing PropertyType aliases: {0}.",
                    string.Join(", ", intersect));
                throw new Exception(message);
            }
        }

        #endregion

        #region Composition

        internal IEnumerable<TreeChange<IContentTypeBase>> ComposeContentTypeChangesForDistributedCache(IEnumerable<IContentTypeBase> contentTypes)
        {
            var changes = new Dictionary<int, TreeChange<IContentTypeBase>>();

            // handle changes:
            // anything other that the alias =>  reload descendants
            // FIXME should be ANYTHING that changes the PUBLISHED CONTENT TYPE
            // ie ID, ALIAS, PROPERTIES
            // and for each property, ALIAS, DATATYPEID, PROPERTYEDITORALIAS
            //
            // FIXME NOT! want them all because ContentTypeCacheRefresher refreshers MORE than the published cache
            // FIXME reindexing all medias?!

            // just the alias => reload just that one

            foreach (var contentType in contentTypes)
            {
                var dirtyProperties = ((ContentType)contentType).GetDirtyProperties();
                var hasOtherThanAliasChanged = dirtyProperties.Any(x => x.InvariantEquals("Alias") == false);
                var hasAliasChanged = ((ContentType)contentType).IsPropertyDirty("Alias");

                if (hasOtherThanAliasChanged)
                {
                    TreeChange<IContentTypeBase> change;
                    if (changes.TryGetValue(contentType.Id, out change))
                        change.ChangeTypes = TreeChangeTypes.RefreshBranch;
                    else
                        changes.Add(contentType.Id, new TreeChange<IContentTypeBase>(contentType, TreeChangeTypes.RefreshBranch));
                }
                else if (hasAliasChanged)
                {
                    if (changes.ContainsKey(contentType.Id) == false)
                        changes.Add(contentType.Id, new TreeChange<IContentTypeBase>(contentType, TreeChangeTypes.RefreshNode));
                }
            }

            return changes.Values;
        }

        internal IEnumerable<IContentTypeBase> ComposeContentTypeChangesForTransactionEvent(IContentTypeBase contentType)
        {
            return ComposeContentTypeChangesForTransactionEvent(new[] { contentType });
        }

        internal IEnumerable<IContentTypeBase> ComposeContentTypeChangesForTransactionEvent(IEnumerable<IContentTypeBase> contentTypes)
        {
            // find all content types impacted by the changes,
            // - content type alias changed
            // - content type property removed, or alias changed
            // - content type composition removed (not testing if composition had properties...)
            //
            // because these are the changes that would impact the raw content data

            // note
            // this is meant to run *after* uow.Commit() so must use WasPropertyDirty() everywhere
            // instead of IsPropertyDirty() since dirty properties have been resetted already

            // hash set handles duplicates
            var changes = new HashSet<IContentTypeBase>(new DelegateEqualityComparer<IContentTypeBase>(
                (x, y) => x.Id == y.Id,
                x => x.Id.GetHashCode()));

            foreach (var contentType in contentTypes)
            {
                var dirty = contentType as IRememberBeingDirty;
                if (dirty == null) throw new Exception("oops");

                // skip new content types
                var isNewContentType = dirty.WasPropertyDirty("HasIdentity");
                if (isNewContentType) continue;

                // alias change?
                var hasAliasChanged = dirty.WasPropertyDirty("Alias");

                // existing property alias change?
                var hasAnyPropertyChangedAlias = contentType.PropertyTypes.Any(propertyType =>
                {
                    var dirtyProperty = propertyType as IRememberBeingDirty;
                    if (dirtyProperty == null) throw new Exception("oops");

                    // skip new properties
                    var isNewProperty = dirtyProperty.WasPropertyDirty("HasIdentity");
                    if (isNewProperty) return false;

                    // alias change?
                    var hasPropertyAliasBeenChanged = dirtyProperty.WasPropertyDirty("Alias");
                    return hasPropertyAliasBeenChanged;
                });

                // removed properties?
                var hasAnyPropertyBeenRemoved = dirty.WasPropertyDirty("HasPropertyTypeBeenRemoved");

                // removed compositions?
                var hasAnyCompositionBeenRemoved = dirty.WasPropertyDirty("HasCompositionTypeBeenRemoved");

                if (hasAliasChanged || hasAnyCompositionBeenRemoved || hasAnyPropertyBeenRemoved || hasAnyPropertyChangedAlias)
                {
                    // add that one
                    changes.Add(contentType);
                }

                if (hasAnyCompositionBeenRemoved || hasAnyPropertyBeenRemoved || hasAnyPropertyChangedAlias)
                {
                    // add all of these that are directly or indirectly composed of that one
                    foreach (var c in contentType.ComposedOf())
                        changes.Add(c);
                }
            }

            return changes;
        }

        /// <summary>
        /// Determines which content types are impacted by content types changes, in a way that needs
        /// to be notified to the content service.
        /// </summary>
        /// <param name="contentTypes">The changed content types.</param>
        /// <returns>The impacted content types.</returns>
        internal IEnumerable<IContentTypeBase> ComposeContentTypeChanges(params IContentTypeBase[] contentTypes)
        {
            // hash set handles duplicates
            var notify = new HashSet<IContentTypeBase>(new DelegateEqualityComparer<IContentTypeBase>(
                (x, y) => x.Id == y.Id,
                x => x.Id.GetHashCode()));

            // fixme
            // this method was originally GetContentTypesForXmlUpdates and was targetted at the XML cache
            // so it does NOT handle some situations that would have no impact on the XML cache
            //
            // ALSO it looks like it's never been updated for compositions?
            //
            // situations
            // - local change, eg change the alias of the content type
            //  content cache must know about the change, but no impact to descendants
            // - DAG change, eg change anything WRT properties
            //  
            // eg 'new property' => don't really need to 'rebuild' the xml stuff
            // but need to update the content types = impact on content cache
            //
            // edited & removed properties, must rebuild xml stuff & impact on cache
            // edited property data types, nothing to do & impact the cache
            //
            // TODO try to figure out
            // - refresh the xml/json/serialized in database
            //    ctype alias changed = just that one
            //    ptype alias changed or removed = descendants
            //    composition removed = ptype removed = descendants
            //    composition added, ... = nothing because NO impact on XML
            // - reload the content in cache
            //    if serialized has changed = same as above
            // - update the content type in cache
            //    if the content type has been updated in any way
            //    ctype alias change = just that one
            //    anything else = descendants
            //
            // TODO try this:
            //  pass the contentTypes to the transaction event, let it figure things out
            //  trigger changed ThisType/ThisBranch events?
            //
            // FIXME note that the transaction events CANNOT run the IsDirty, only Was DIRTY * TOO LATE
            // would have the same impact if moving the ContentRepository events to ContentService...
            // BUT could do it by gathering the types before uow.Commit()

            foreach (var contentType in contentTypes)
            {
                // fixme experiment
                var hasOtherThanAliasChanged = ((ContentType)contentType).GetDirtyProperties().Any(x => x.InvariantEquals("Alias") == false);

                var dirty = contentType as IRememberBeingDirty;
                if (dirty == null) throw new Exception("oops");

                // skip new content types
                var isNewContentType = dirty.WasPropertyDirty("HasIdentity");
                if (isNewContentType) continue;

                // fixme - contentType.PropertyTypes is LOCAL ONLY or COMPOSED?
                // fixme - what about ADDED PROPERTIES?
                // fixme - what about properties that CHANGE THEIR DATA TYPE?
                // fixme - what about COMPOSITION CHANGES

                // existing property alias change?
                var hasAnyPropertyChangedAlias = contentType.PropertyTypes.Any(propertyType =>
                {
                    var dirtyProperty = propertyType as IRememberBeingDirty;
                    if (dirtyProperty == null) return false;

                    // skip new properties
                    var isNewProperty = dirtyProperty.WasPropertyDirty("HasIdentity");
                    if (isNewProperty) return false;

                    // alias change?
                    var hasPropertyAliasBeenChanged = dirtyProperty.WasPropertyDirty("Alias");
                    return hasPropertyAliasBeenChanged;
                });

                // removed properties?
                var hasAnyPropertyBeenRemoved = dirty.WasPropertyDirty("HasPropertyTypeBeenRemoved");

                // alias change?
                var hasAliasBeenChanged = dirty.WasPropertyDirty("Alias");

                // skip if nothing changed
                if ((hasAliasBeenChanged || hasAnyPropertyBeenRemoved || hasAnyPropertyChangedAlias) == false) continue;

                // alias changes impact only the current content type whereas property changes impact descendants
                if (hasAnyPropertyBeenRemoved || hasAnyPropertyChangedAlias)
                {
                    foreach (var c in contentType.DescendantsAndSelf())
                        notify.Add(c);
                }
                else
                {
                    notify.Add(contentType);
                }
            }

            return notify;
        }
        
        #endregion

        #region Get, Has, Is

        public TItem Get(int id)
        {
            return LRepo.WithReadLocked(xr => xr.Repository.Get(id));
        }

        public TItem Get(string alias)
        {
            var query = Query<TItem>.Builder.Where(x => x.Alias == alias);
            return LRepo.WithReadLocked(xr => xr.Repository.GetByQuery(query).FirstOrDefault());
        }

        public IEnumerable<TItem> GetAll(params int[] ids)
        {
            return LRepo.WithReadLocked(xr => xr.Repository.GetAll(ids));
        }

        public IEnumerable<TItem> GetChildren(int id)
        {
            var query = Query<TItem>.Builder.Where(x => x.ParentId == id);
            return LRepo.WithReadLocked(xr => xr.Repository.GetByQuery(query));
        }

        public bool HasChildren(int id)
        {
            var query = Query<TItem>.Builder.Where(x => x.ParentId == id);
            return LRepo.WithReadLocked(xr => xr.Repository.Count(query) > 0);
        }

        public IEnumerable<TItem> GetDescendants(int id, bool andSelf)
        {
            return LRepo.WithReadLocked(xr =>
            {
                var descendants = new List<TItem>();
                if (andSelf) descendants.Add(xr.Repository.Get(id));
                var ids = new Stack<int>();
                ids.Push(id);

                while (ids.Count > 0)
                {
                    var i = ids.Pop();
                    var query = Query<TItem>.Builder.Where(x => x.ParentId == i);
                    var result = xr.Repository.GetByQuery(query).ToArray();

                    foreach (var c in result)
                    {
                        descendants.Add(c);
                        ids.Push(c.Id);
                    }
                }

                return descendants.ToArray();
            });
        }

        public IEnumerable<TItem> GetComposedOf(int id)
        {
            return LRepo.WithReadLocked(xr =>
            {
                // hash set handles duplicates
                var composed = new HashSet<TItem>(new DelegateEqualityComparer<TItem>(
                    (x, y) => x.Id == y.Id,
                    x => x.Id.GetHashCode()));

                var ids = new Stack<int>();
                ids.Push(id);

                while (ids.Count > 0)
                {
                    var i = ids.Pop();
                    var result = xr.Repository.GetTypesDirectlyComposedOf(i).ToArray();

                    foreach (var c in result)
                    {
                        composed.Add(c);
                        ids.Push(c.Id);
                    }
                }

                return composed.ToArray();
            });
        }

        #endregion

        #region Save

        public void Save(TItem item, int userId = 0)
        {
            if (Saving.IsRaisedEventCancelled(new SaveEventArgs<TItem>(item), this))
                return;

            LRepo.WithWriteLocked(xr =>
            {
                // validate the DAG transform, within the lock
                ValidateLocked(xr.Repository, item); // throws if invalid

                item.CreatorId = userId;
                xr.Repository.AddOrUpdate(item); // also updates contents
                xr.UnitOfWork.Commit(); // commits the UOW but NOT the transaction

                // figure out content types impacted by the changes through composition
                var changed = ComposeContentTypeChangesForTransactionEvent(item).Cast<TItem>().ToArray();
                TransactionRefreshedEntity.RaiseEvent(new EntityChangeEventArgs(xr.UnitOfWork, changed), this);
            });

            // fixme - raise distributed events
            // raise event fot that type only, because it's a distributed event,
            // so impacted types (through composition) will be determined locally
            // fixme - can we do it OR do we need extra infos eg RefreshTypeLocally, RefreshTypeComposition
            //Changed.RaiseEvent(new ChangeEventArgs(mediaType), this);
            //ApplyChangesToContent(changedTypes);

            Saved.RaiseEvent(new SaveEventArgs<TItem>(item, false), this);
            Audit(AuditType.Save, string.Format("Save MediaType performed by user"), userId, item.Id);
        }

        public void Save(IEnumerable<TItem> items, int userId = 0)
        {
            var itemsA = items.ToArray();

            if (Saving.IsRaisedEventCancelled(new SaveEventArgs<TItem>(itemsA), this))
                return;

            LRepo.WithWriteLocked(xr =>
            {
                // all-or-nothing, validate the DAG transforms, within the lock
                foreach (var item in itemsA)
                    ValidateLocked(xr.Repository, item); // throws if invalid

                foreach (var item in itemsA)
                {
                    item.CreatorId = userId;
                    xr.Repository.AddOrUpdate(item); // also updates contents
                }

                xr.UnitOfWork.Commit(); // commits the UOW but NOT the transaction

                // figure out content types impacted by the changes through composition
                var changed = ComposeContentTypeChangesForTransactionEvent(itemsA).Cast<TItem>().ToArray();
                TransactionRefreshedEntity.RaiseEvent(new EntityChangeEventArgs(xr.UnitOfWork, changed), this);
            });

            // fixme - raise distributed events
            // raise event fot that type only, because it's a distributed event,
            // so impacted types (through composition) will be determined locally
            // fixme - can we do it OR do we need extra infos eg RefreshTypeLocally, RefreshTypeComposition
            //Changed.RaiseEvent(new ChangeEventArgs(contentTypesA), this);

            Saved.RaiseEvent(new SaveEventArgs<TItem>(itemsA, false), this);
            Audit(AuditType.Save, string.Format("Save MediaTypes performed by user"), userId, -1);
        }

        #endregion

        #region Delete

        public void Delete(TItem item, int userId = 0)
        {
            if (Deleting.IsRaisedEventCancelled(new DeleteEventArgs<TItem>(item), this))
                return;

            LRepo.WithWriteLocked(xr =>
            {
                // all descendants are going to be deleted
                var descendantsAndSelf = item.DescendantsAndSelf().ToArray();

                // all impacted (through composition) probably lose some properties
                var changed = descendantsAndSelf.SelectMany(xx => xx.ComposedOf())
                    .Distinct()
                    .Except(descendantsAndSelf) // will be deleted anyway
                    .Cast<TItem>()
                    .ToArray();

                // delete content
                DeleteItemsOfTypes(descendantsAndSelf.Select(x => x.Id));

                // finally delete the content type
                // - recursively deletes all descendants
                // - deletes all associated property data
                //  (contents of any descendant type have been deleted but
                //   contents of any composed (impacted) type remain but
                //   need to have their property data cleared)
                xr.Repository.Delete(item);

                xr.UnitOfWork.Commit(); // commits the UOW but NOT the transaction

                // no need for 'transaction event' for deleted content
                // because deleting content does trigger its own event
                //
                // need 'transaction event' for content that have changed
                // ie those that were composed of the deleted content OR
                // of any of its descendants
                TransactionRefreshedEntity.RaiseEvent(new EntityChangeEventArgs(xr.UnitOfWork, changed), this);
            });

            // fixme - raise distributed events

            Deleted.RaiseEvent(new DeleteEventArgs<TItem>(item, false), this);
            Audit(AuditType.Delete, string.Format("Delete MediaType performed by user"), userId, item.Id);
        }

        /// <summary>
        /// Deletes a collection of <see cref="IMediaType"/> objects
        /// </summary>
        /// <param name="items">Collection of <see cref="IMediaType"/> to delete</param>
        /// <param name="userId"></param>
        /// <remarks>Deleting a <see cref="IMediaType"/> will delete all the <see cref="IMedia"/> objects based on this <see cref="IMediaType"/></remarks>
        public void Delete(IEnumerable<TItem> items, int userId = 0)
        {
            var itemsA = items.ToArray();

            if (Deleting.IsRaisedEventCancelled(new DeleteEventArgs<TItem>(itemsA), this))
                return;

            LRepo.WithWriteLocked(xr =>
            {
                // all descendants are going to be deleted
                var allDescendantsAndSelf = itemsA.SelectMany(xx => xx.DescendantsAndSelf())
                    .Distinct()
                    .ToArray();

                // all impacted (through composition) probably lose some properties
                var changed = allDescendantsAndSelf.SelectMany(x => x.ComposedOf())
                    .Distinct()
                    .Except(allDescendantsAndSelf) // will be deleted anyway
                    .Cast<TItem>()
                    .ToArray();

                // delete content
                DeleteItemsOfTypes(allDescendantsAndSelf.Select(x => x.Id));

                // finally delete the content types
                // (see notes in overload)
                foreach (var item in itemsA)
                    xr.Repository.Delete(item);

                xr.UnitOfWork.Commit(); // commits the UOW but NOT the transaction

                // (see notes in overload)
                TransactionRefreshedEntity.RaiseEvent(new EntityChangeEventArgs(xr.UnitOfWork, changed), this);
            });

            // fixme - raise distributed events
            // allDescendantsAndSelf: report REMOVE
            // impacted: report REFRESH
            // BUT async => just report WTF?!

            Deleted.RaiseEvent(new DeleteEventArgs<TItem>(itemsA, false), this);
            Audit(AuditType.Delete, string.Format("Delete MediaTypes performed by user"), userId, -1);
        }

        protected abstract void DeleteItemsOfTypes(IEnumerable<int> typeIds);

        #endregion

        #region Copy

        public TItem Copy(TItem original, string alias, string name, int parentId = -1)
        {
            TItem parent = null;
            if (parentId > 0)
            {
                parent = Get(parentId);
                if (parent == null)
                    throw new InvalidOperationException("Could not find content type with id " + parentId);
            }
            return Copy(original, alias, name, parent);
        }

        public TItem Copy(TItem original, string alias, string name, TItem parent)
        {
            Mandate.ParameterNotNull(original, "original");
            Mandate.ParameterNotNullOrEmpty(alias, "alias");

            if (parent != null)
                Mandate.That(parent.HasIdentity, () => new InvalidOperationException("The parent content type must have an identity"));

            // illegal - http://stackoverflow.com/questions/19166133/why-does-a-direct-cast-fail-but-the-as-operator-succeed-when-testing-a-constra
            //var originalb = (ContentTypeCompositionBase) original;
            //
            // verbose - we *know* it's a ContentTypeCompositionBase anyway
            //var originalb = original as ContentTypeCompositionBase;
            //if (originalb == null) throw new Exception("oops.");
            //
            // so this *must* be ok
            var originalb = (ContentTypeCompositionBase) (object) original;

            var clone = (TItem) originalb.DeepCloneWithResetIdentities(alias);

            clone.Name = name;

            var compositionAliases = clone.CompositionAliases().Except(new[] { alias }).ToList();
            //remove all composition that is not it's current alias
            foreach (var a in compositionAliases)
            {
                clone.RemoveContentType(a);
            }

            //if a parent is specified set it's composition and parent
            if (parent != null)
            {
                //add a new parent composition
                clone.AddContentType(parent);
                clone.ParentId = parent.Id;
            }
            else
            {
                //set to root
                clone.ParentId = -1;
            }

            Save(clone);
            return clone;
        }

        #endregion

        #region Audit

        protected void Audit(AuditType type, string message, int userId, int objectId)
        {
            var uow = UowProvider.GetUnitOfWork();
            using (var auditRepo = RepositoryFactory.CreateAuditRepository(uow))
            {
                auditRepo.AddOrUpdate(new AuditItem(objectId, message, type, userId));
                uow.Commit();
            }
        }

        #endregion

        #region Events

        public static event TypedEventHandler<ContentTypeServiceBase<TRepository, TItem>, SaveEventArgs<TItem>> Saving;
        public static event TypedEventHandler<ContentTypeServiceBase<TRepository, TItem>, SaveEventArgs<TItem>> Saved;

        public static event TypedEventHandler<ContentTypeServiceBase<TRepository, TItem>, DeleteEventArgs<TItem>> Deleting;
        public static event TypedEventHandler<ContentTypeServiceBase<TRepository, TItem>, DeleteEventArgs<TItem>> Deleted;

        public class EntityChangeEventArgs : EventArgs
        {
            public EntityChangeEventArgs(IDatabaseUnitOfWork unitOfWork, IEnumerable<TItem> entities)
            {
                UnitOfWork = unitOfWork;
                Entities = entities;
            }

            public IEnumerable<TItem> Entities { get; private set; }
            public IDatabaseUnitOfWork UnitOfWork { get; private set; }
        }

        public static event TypedEventHandler<ContentTypeServiceBase<TRepository, TItem>, EntityChangeEventArgs> TransactionRefreshedEntity;

        #endregion
    }
}