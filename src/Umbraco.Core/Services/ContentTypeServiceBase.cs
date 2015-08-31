using System;
using System.Collections.Generic;
using System.Linq;
using Umbraco.Core.Cache;
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
    public abstract class ContentTypeServiceBase : RepositoryService
    {
        protected ContentTypeServiceBase(IDatabaseUnitOfWorkProvider provider, RepositoryFactory repositoryFactory, ILogger logger, IEventMessagesFactory eventMessagesFactory)
            : base(provider, repositoryFactory, logger, eventMessagesFactory)
        { }

        #region Events

        [Flags]
        public enum ChangeTypes : byte
        {
            None = 0,
            RefreshMain = 1, // changed, impacts content (adding ppty or composition does NOT)
            RefreshOther = 2, // changed, other changes
            Remove = 4 // item type has been removed
        }

        #endregion


        #region Services

        public static IContentTypeServiceBase<T> GetService<T>(ServiceContext services)
        {
            if (typeof(T).Implements<IContentType>())
                return services.ContentTypeService as IContentTypeServiceBase<T>;
            if (typeof(T).Implements<IMediaType>())
                return services.MediaTypeService as IContentTypeServiceBase<T>;
            if (typeof(T).Implements<IMemberType>())
                return services.MemberTypeService as IContentTypeServiceBase<T>;
            throw new ArgumentException("Type " + typeof(T).FullName + " does not have a service.");
        }

        #endregion
    }

    internal static class ContentTypeServiceBaseChangeExtensions
    {
        public static ContentTypeServiceBase<TItem>.Change.EventArgs ToEventArgs<TItem>(this IEnumerable<ContentTypeServiceBase<TItem>.Change> changes)
            where TItem : class, IContentTypeComposition
        {
            return new ContentTypeServiceBase<TItem>.Change.EventArgs(changes);
        }

        public static bool HasType(this ContentTypeServiceBase.ChangeTypes change, ContentTypeServiceBase.ChangeTypes type)
        {
            return (change & type) != ContentTypeServiceBase.ChangeTypes.None;
        }

        public static bool HasTypesAll(this ContentTypeServiceBase.ChangeTypes change, ContentTypeServiceBase.ChangeTypes types)
        {
            return (change & types) == types;
        }

        public static bool HasTypesAny(this ContentTypeServiceBase.ChangeTypes change, ContentTypeServiceBase.ChangeTypes types)
        {
            return (change & types) != ContentTypeServiceBase.ChangeTypes.None;
        }

        public static bool HasTypesNone(this ContentTypeServiceBase.ChangeTypes change, ContentTypeServiceBase.ChangeTypes types)
        {
            return (change & types) == ContentTypeServiceBase.ChangeTypes.None;
        }
    }

    internal abstract class ContentTypeServiceBase<TItem> : ContentTypeServiceBase
        where TItem : class, IContentTypeComposition
    {
        protected ContentTypeServiceBase(IDatabaseUnitOfWorkProvider provider, RepositoryFactory repositoryFactory, ILogger logger, IEventMessagesFactory eventMessagesFactory)
            : base(provider, repositoryFactory, logger, eventMessagesFactory)
        { }

        #region Events

        public class Change
        {
            public Change(TItem item, ChangeTypes changeTypes)
            {
                Item = item;
                ChangeTypes = changeTypes;
            }

            public TItem Item { get; private set; }
            public ChangeTypes ChangeTypes { get; internal set; }

            public EventArgs ToEventArgs(Change change)
            {
                return new EventArgs(change);
            }

            public class EventArgs : System.EventArgs
            {
                public EventArgs(IEnumerable<Change> changes)
                {
                    Changes = changes.ToArray();
                }

                public EventArgs(Change change)
                    : this(new[] { change })
                { }

                public IEnumerable<Change> Changes { get; private set; }
            }
        }

        internal static event TypedEventHandler<ContentTypeServiceBase<TItem>, Change.EventArgs> Changed;

        protected void OnChanged(Change.EventArgs args)
        {
            Changed.RaiseEvent(args, this);
        }

        public static event TypedEventHandler<ContentTypeServiceBase<TItem>, Change.EventArgs> TxEntityRefreshed;

        protected void OnTxEntityRefreshed(Change.EventArgs args)
        {
            TxEntityRefreshed.RaiseEvent(args, this);
        }

        public static event TypedEventHandler<ContentTypeServiceBase<TItem>, SaveEventArgs<TItem>> Saving;
        public static event TypedEventHandler<ContentTypeServiceBase<TItem>, SaveEventArgs<TItem>> Saved;

        protected void OnSaving(SaveEventArgs<TItem> args)
        {
            Saving.RaiseEvent(args, this);
        }

        protected bool OnSavingCancelled(SaveEventArgs<TItem> args)
        {
            return Saving.IsRaisedEventCancelled(args, this);
        }

        protected void OnSaved(SaveEventArgs<TItem> args)
        {
            Saved.RaiseEvent(args, this);
        }

        public static event TypedEventHandler<ContentTypeServiceBase<TItem>, DeleteEventArgs<TItem>> Deleting;
        public static event TypedEventHandler<ContentTypeServiceBase<TItem>, DeleteEventArgs<TItem>> Deleted;

        protected void OnDeleting(DeleteEventArgs<TItem> args)
        {
            Deleting.RaiseEvent(args, this);
        }

        protected bool OnDeletingCancelled(DeleteEventArgs<TItem> args)
        {
            return Deleting.IsRaisedEventCancelled(args, this);
        }

        protected void OnDeleted(DeleteEventArgs<TItem> args)
        {
            Deleted.RaiseEvent(args, this);
        }

        #endregion
    }

    /// <summary>
    /// Provides a base class for <see cref="ContentTypeService"/>, <see cref="MediaTypeService"/> and <see cref="MemberTypeService"/>.
    /// </summary>
    /// <typeparam name="TRepository">The type of the underlying repository.</typeparam>
    /// <typeparam name="TItem">The type of the item.</typeparam>
    internal abstract class ContentTypeServiceBase<TRepository, TItem> : ContentTypeServiceBase<TItem>, IContentTypeServiceBase<TItem>
        where TRepository : ContentTypeBaseRepository<TItem>, IDisposable
        where TItem : class, IContentTypeComposition
    {
        protected ContentTypeServiceBase(IDatabaseUnitOfWorkProvider provider, RepositoryFactory repositoryFactory, ILogger logger, IEventMessagesFactory eventMessagesFactory, LockingRepository<TRepository> lrepo)
            : base(provider, repositoryFactory, logger, eventMessagesFactory)
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

        internal IEnumerable<Change> ComposeContentTypeChanges(params TItem[] contentTypes)
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

            var changes = new List<Change>();

            foreach (var contentType in contentTypes)
            {
                var dirty = (IRememberBeingDirty) contentType;

                // skip new content types
                var isNewContentType = dirty.WasPropertyDirty("HasIdentity");
                if (isNewContentType)
                {
                    AddChange(changes, contentType, ChangeTypes.RefreshOther);
                    continue;
                }

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

                // main impact on properties?
                var hasPropertyMainImpact = hasAnyCompositionBeenRemoved || hasAnyPropertyBeenRemoved || hasAnyPropertyChangedAlias;

                if (hasAliasChanged || hasPropertyMainImpact)
                {
                    // add that one, as a main change
                    AddChange(changes, contentType, ChangeTypes.RefreshMain);

                    if (hasPropertyMainImpact)
                        foreach (var c in contentType.ComposedOf())
                            AddChange(changes, c, ChangeTypes.RefreshMain);
                }
                else
                {
                    // add that one, as an other change
                    AddChange(changes, contentType, ChangeTypes.RefreshOther);
                }
            }

            return changes;
        }

        // ensures changes contains no duplicates
        private static void AddChange(ICollection<Change> changes, TItem contentType, ChangeTypes changeTypes)
        {
            var change = changes.FirstOrDefault(x => x.Item == contentType);
            if (change == null)
            {
                changes.Add(new Change(contentType, changeTypes));
                return;
            }
            change.ChangeTypes |= changeTypes;
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

        public TItem Get(Guid id)
        {
            var query = Query<TItem>.Builder.Where(x => x.Key == id);
            return LRepo.WithReadLocked(xr => xr.Repository.GetByQuery(query).FirstOrDefault());
        }

        public IEnumerable<TItem> GetAll(params int[] ids)
        {
            return LRepo.WithReadLocked(xr => xr.Repository.GetAll(ids));
        }

        public IEnumerable<TItem> GetAll(params Guid[] ids)
        {
            return LRepo.WithReadLocked(xr => xr.Repository.GetAll(ids));
        }

        public IEnumerable<TItem> GetChildren(int id)
        {
            var query = Query<TItem>.Builder.Where(x => x.ParentId == id);
            return LRepo.WithReadLocked(xr => xr.Repository.GetByQuery(query));
        }

        public IEnumerable<TItem> GetChildren(Guid id)
        {
            var parent = Get(id);
            return parent == null ? Enumerable.Empty<TItem>() : GetChildren(parent.Id);
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
            if (OnSavingCancelled(new SaveEventArgs<TItem>(item)))
                return;

            Change.EventArgs args = null;

            LRepo.WithWriteLocked(xr =>
            {
                // validate the DAG transform, within the lock
                ValidateLocked(xr.Repository, item); // throws if invalid

                item.CreatorId = userId;
                xr.Repository.AddOrUpdate(item); // also updates contents
                xr.UnitOfWork.Commit(); // commits the UOW but NOT the transaction

                // figure out impacted content types
                var changes = ComposeContentTypeChanges(item).ToArray();
                args = changes.ToEventArgs();
                OnTxEntityRefreshed(args);
            });

            using (ChangeSet.WithAmbient)
            {
                OnChanged(args);
            }

            OnSaved(new SaveEventArgs<TItem>(item, false));
            Audit(AuditType.Save, "Save MediaType performed by user", userId, item.Id);
        }

        public void Save(IEnumerable<TItem> items, int userId = 0)
        {
            var itemsA = items.ToArray();

            if (OnSavingCancelled(new SaveEventArgs<TItem>(itemsA)))
                return;

            Change.EventArgs args = null;

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

                // figure out impacted content types
                var changes = ComposeContentTypeChanges(itemsA).ToArray();
                args = changes.ToEventArgs();
                OnTxEntityRefreshed(args);
            });

            using (ChangeSet.WithAmbient)
            {
                OnChanged(args);
            }

            OnSaved(new SaveEventArgs<TItem>(itemsA, false));
            Audit(AuditType.Save, "Save MediaTypes performed by user", userId, -1);
        }

        #endregion

        #region Delete

        public void Delete(TItem item, int userId = 0)
        {
            if (OnDeletingCancelled(new DeleteEventArgs<TItem>(item)))
                return;

            Change.EventArgs args = null;

            LRepo.WithWriteLocked(xr =>
            {
                // all descendants are going to be deleted
                var descendantsAndSelf = item.DescendantsAndSelf()
                    .ToArray();

                // all impacted (through composition) probably lose some properties
                // don't try to be too clever here, just report them all
                // do this before anything is deleted
                var changed = descendantsAndSelf.SelectMany(xx => xx.ComposedOf())
                    .Distinct()
                    .Except(descendantsAndSelf)
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

                var changes = descendantsAndSelf.Select(x => new Change(x, ChangeTypes.Remove))
                    .Concat(changed.Select(x => new Change(x, ChangeTypes.RefreshMain | ChangeTypes.RefreshOther)));
                args = changes.ToEventArgs();

                OnTxEntityRefreshed(args);
            });

            using (ChangeSet.WithAmbient)
            {
                OnChanged(args);
            }

            OnDeleted(new DeleteEventArgs<TItem>(item, false));
            Audit(AuditType.Delete, "Delete MediaType performed by user", userId, item.Id);
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

            if (OnDeletingCancelled(new DeleteEventArgs<TItem>(itemsA)))
                return;

            Change.EventArgs args = null;

            LRepo.WithWriteLocked(xr =>
            {
                // all descendants are going to be deleted
                var allDescendantsAndSelf = itemsA.SelectMany(xx => xx.DescendantsAndSelf())
                    .Distinct()
                    .ToArray();

                // all impacted (through composition) probably lose some properties
                // don't try to be too clever here, just report them all
                // do this before anything is deleted
                var changed = allDescendantsAndSelf.SelectMany(x => x.ComposedOf())
                    .Distinct()
                    .Except(allDescendantsAndSelf)
                    .ToArray();

                // delete content
                DeleteItemsOfTypes(allDescendantsAndSelf.Select(x => x.Id));

                // finally delete the content types
                // (see notes in overload)
                foreach (var item in itemsA)
                    xr.Repository.Delete(item);

                xr.UnitOfWork.Commit(); // commits the UOW but NOT the transaction

                var changes = allDescendantsAndSelf.Select(x => new Change(x, ChangeTypes.Remove))
                    .Concat(changed.Select(x => new Change(x, ChangeTypes.RefreshMain | ChangeTypes.RefreshOther)));
                args = changes.ToEventArgs();

                OnTxEntityRefreshed(args);
            });

            using (ChangeSet.WithAmbient)
            {
                OnChanged(args);
            }

            OnDeleted(new DeleteEventArgs<TItem>(itemsA, false));
            Audit(AuditType.Delete, "Delete MediaTypes performed by user", userId, -1);
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
    }
}