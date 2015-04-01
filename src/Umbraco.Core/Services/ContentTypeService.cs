using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using Umbraco.Core.Events;
using Umbraco.Core.Logging;
using Umbraco.Core.Models;
using Umbraco.Core.Persistence;
using Umbraco.Core.Persistence.Querying;
using Umbraco.Core.Persistence.Repositories;
using Umbraco.Core.Persistence.UnitOfWork;

namespace Umbraco.Core.Services
{
    /// <summary>
    /// Represents the ContentType Service, which is an easy access to operations involving <see cref="IContentType"/>.
    /// </summary>
    public class ContentTypeService : ContentTypeServiceBase, IContentTypeService
    {
	    private readonly IContentService _contentService;
        private readonly IMediaService _mediaService;

        public ContentTypeService(IDatabaseUnitOfWorkProvider provider, RepositoryFactory repositoryFactory, ILogger logger, IContentService contentService, IMediaService mediaService)
            : base(provider, repositoryFactory, logger)
        {
            if (contentService == null) throw new ArgumentNullException("contentService");
            if (mediaService == null) throw new ArgumentNullException("mediaService");
            _contentService = contentService;
            _mediaService = mediaService;
        }

        /// <summary>
        /// Gets all property type aliases.
        /// </summary>
        /// <returns></returns>
        public IEnumerable<string> GetAllPropertyTypeAliases()
        {
            using (var repository = RepositoryFactory.CreateContentTypeRepository(UowProvider.GetUnitOfWork()))
            {
                return repository.GetAllPropertyTypeAliases();
            }
        }

        #region Lock Helper Methods

        // note
        // locking content or media types ends up locking ALL types

        private T WithReadLockedContentTypes<T>(Func<ContentTypeRepository, T> func, bool autoCommit = true)
        {
            var uow = UowProvider.GetUnitOfWork();
            using (var transaction = uow.Database.GetTransaction(IsolationLevel.RepeatableRead))
            using (var irepository = RepositoryFactory.CreateContentTypeRepository(uow))
            {
                var repository = irepository as ContentTypeRepository;
                if (repository == null) throw new Exception("oops");
                repository.AcquireReadLock();
                var ret = func(repository);
                if (autoCommit == false) return ret;
                repository.UnitOfWork.Commit();
                transaction.Complete();
                return ret;
            }
        }

        private void WithReadLockedContentTypes(Action<ContentTypeRepository> action, bool autoCommit = true)
        {
            var uow = UowProvider.GetUnitOfWork();
            using (var transaction = uow.Database.GetTransaction(IsolationLevel.RepeatableRead))
            using (var irepository = RepositoryFactory.CreateContentTypeRepository(uow))
            {
                var repository = irepository as ContentTypeRepository;
                if (repository == null) throw new Exception("oops");
                repository.AcquireReadLock();
                action(repository);
                if (autoCommit == false) return;
                repository.UnitOfWork.Commit();
                transaction.Complete();
            }
        }

        private T WithReadLockedMediaTypes<T>(Func<MediaTypeRepository, T> func, bool autoCommit = true)
        {
            var uow = UowProvider.GetUnitOfWork();
            using (var transaction = uow.Database.GetTransaction(IsolationLevel.RepeatableRead))
            using (var irepository = RepositoryFactory.CreateMediaTypeRepository(uow))
            {
                var repository = irepository as MediaTypeRepository;
                if (repository == null) throw new Exception("oops");
                repository.AcquireReadLock();
                var ret = func(repository);
                if (autoCommit == false) return ret;
                repository.UnitOfWork.Commit();
                transaction.Complete();
                return ret;
            }
        }

        private void WithWriteLockedContentAndContentTypes(Action<ContentTypeRepository> action, bool autoCommit = true)
        {
            var uow = UowProvider.GetUnitOfWork();
            using (var transaction = uow.Database.GetTransaction(IsolationLevel.RepeatableRead))
            using (var irepository = RepositoryFactory.CreateContentTypeRepository(uow))
            using (var iContentRepository = RepositoryFactory.CreateContentRepository(uow))
            {
                var repository = irepository as ContentTypeRepository;
                if (repository == null) throw new Exception("oops");

                var contentRepository = iContentRepository as ContentRepository;
                if (contentRepository == null) throw new Exception("oops");

                // respect order to avoid deadlocks
                contentRepository.AcquireWriteLock();
                repository.AcquireWriteLock();

                action(repository);
                if (autoCommit == false) return;
                repository.UnitOfWork.Commit();
                transaction.Complete();
            }
        }

        private void WithWriteLockedMediaAndMediaTypes(Action<MediaTypeRepository> action, bool autoCommit = true)
        {
            var uow = UowProvider.GetUnitOfWork();
            using (var transaction = uow.Database.GetTransaction(IsolationLevel.RepeatableRead))
            using (var irepository = RepositoryFactory.CreateMediaTypeRepository(uow))
            using (var iMediaRepository = RepositoryFactory.CreateMediaRepository(uow))
            {
                var repository = irepository as MediaTypeRepository;
                if (repository == null) throw new Exception("oops");

                var mediaRepository = iMediaRepository as MediaRepository;
                if (mediaRepository == null) throw new Exception("oops");

                // respect order to avoid deadlocks
                mediaRepository.AcquireWriteLock();
                repository.AcquireWriteLock();

                action(repository);
                if (autoCommit == false) return;
                repository.UnitOfWork.Commit();
                transaction.Complete();
            }
        }

        #endregion

        #region All - Copy

        /// <summary>
        /// Copies a content type as a child under the specified parent if specified (otherwise to the root)
        /// </summary>
        /// <param name="original">
        /// The content type to copy
        /// </param>
        /// <param name="alias">
        /// The new alias of the content type
        /// </param>
        /// <param name="name">
        /// The new name of the content type
        /// </param>
        /// <param name="parentId">
        /// The parent to copy the content type to, default is -1 (root)
        /// </param>
        /// <returns></returns>
        public IContentType Copy(IContentType original, string alias, string name, int parentId = -1)
        {
            IContentType parent = null;            
            if (parentId > 0)
            {
                parent = GetContentType(parentId);
                if (parent == null)
                {
                    throw new InvalidOperationException("Could not find content type with id " + parentId);
                }
            }
            return Copy(original, alias, name, parent);
        }

        /// <summary>
        /// Copies a content type as a child under the specified parent if specified (otherwise to the root)
        /// </summary>
        /// <param name="original">
        /// The content type to copy
        /// </param>
        /// <param name="alias">
        /// The new alias of the content type
        /// </param>
        /// <param name="name">
        /// The new name of the content type
        /// </param>
        /// <param name="parent">
        /// The parent to copy the content type to, default is null (root)
        /// </param>
        /// <returns></returns>
        public IContentType Copy(IContentType original, string alias, string name, IContentType parent)
        {
            Mandate.ParameterNotNull(original, "original");
            Mandate.ParameterNotNullOrEmpty(alias, "alias");
            if (parent != null)
            {
                Mandate.That(parent.HasIdentity, () => new InvalidOperationException("The parent content type must have an identity"));    
            }

            var clone = original.DeepCloneWithResetIdentities(alias);

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

        #region All - Validate

        public void Validate(IContentTypeComposition compo)
        {
            // locking the content types but it really does not matter
            // because it locks ALL content types incl. medias & members &...
            WithReadLockedContentTypes(repository => ValidateLocked(compo));
        }

        private void ValidateLocked(IContentTypeComposition compositionContentType)
        {
            // performs business-level validation of the composition
            // should ensure that it is absolutely safe to save the composition

            // eg maybe a property has been added, with an alias that's OK (no conflict with ancestors)
            // but that cannot be used (conflict with descendants)

            var contentType = compositionContentType as IContentType;
            var mediaType = compositionContentType as IMediaType;

            IContentTypeComposition[] allContentTypes;
            if (contentType != null)
                allContentTypes = GetAllContentTypes().Cast<IContentTypeComposition>().ToArray();
            else if (mediaType != null)
                allContentTypes = GetAllMediaTypes().Cast<IContentTypeComposition>().ToArray();
            else
                throw new Exception("Composition is neither IContentType nor IMediaType?");

            var compositionAliases = compositionContentType.CompositionAliases();
            var compositions = allContentTypes.Where(x => compositionAliases.Any(y => x.Alias.Equals(y)));
            var propertyTypeAliases = compositionContentType.PropertyTypes.Select(x => x.Alias.ToLowerInvariant()).ToArray();
            var indirectReferences = allContentTypes.Where(x => x.ContentTypeComposition.Any(y => y.Id == compositionContentType.Id));
            var comparer = new DelegateEqualityComparer<IContentTypeComposition>((x, y) => x.Id == y.Id, x => x.Id);
            var dependencies = new HashSet<IContentTypeComposition>(compositions, comparer);
            var stack = new Stack<IContentTypeComposition>();
            indirectReferences.ForEach(stack.Push);//Push indirect references to a stack, so we can add recursively
            while (stack.Count > 0)
            {
                var indirectReference = stack.Pop();
                dependencies.Add(indirectReference);
                //Get all compositions for the current indirect reference
                var directReferences = indirectReference.ContentTypeComposition;

                foreach (var directReference in directReferences)
                {
                    if (directReference.Id == compositionContentType.Id || directReference.Alias.Equals(compositionContentType.Alias)) continue;
                    dependencies.Add(directReference);
                    //A direct reference has compositions of its own - these also need to be taken into account
                    var directReferenceGraph = directReference.CompositionAliases();
                    allContentTypes.Where(x => directReferenceGraph.Any(y => x.Alias.Equals(y, StringComparison.InvariantCultureIgnoreCase))).ForEach(c => dependencies.Add(c));
                }
                //Recursive lookup of indirect references
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

        #region Content - Get, Has, Is

        /// <summary>
        /// Gets an <see cref="IContentType"/> object by its Id
        /// </summary>
        /// <param name="id">Id of the <see cref="IContentType"/> to retrieve</param>
        /// <returns><see cref="IContentType"/></returns>
        public IContentType GetContentType(int id)
        {
            return WithReadLockedContentTypes(repository => repository.Get(id));
        }

        /// <summary>
        /// Gets an <see cref="IContentType"/> object by its Alias
        /// </summary>
        /// <param name="alias">Alias of the <see cref="IContentType"/> to retrieve</param>
        /// <returns><see cref="IContentType"/></returns>
        public IContentType GetContentType(string alias)
        {
            var query = Query<IContentType>.Builder.Where(x => x.Alias == alias);
            return WithReadLockedContentTypes(repository => repository.GetByQuery(query).FirstOrDefault());
        }

        /// <summary>
        /// Gets a list of all available <see cref="IContentType"/> objects
        /// </summary>
        /// <param name="ids">Optional list of ids</param>
        /// <returns>An Enumerable list of <see cref="IContentType"/> objects</returns>
        public IEnumerable<IContentType> GetAllContentTypes(params int[] ids)
        {
            return WithReadLockedContentTypes(repository => repository.GetAll(ids));
        }

        /// <summary>
        /// Gets a list of children for a <see cref="IContentType"/> object
        /// </summary>
        /// <param name="id">Id of the Parent</param>
        /// <returns>An Enumerable list of <see cref="IContentType"/> objects</returns>
        public IEnumerable<IContentType> GetContentTypeChildren(int id)
        {
            var query = Query<IContentType>.Builder.Where(x => x.ParentId == id);
            return WithReadLockedContentTypes(repository => repository.GetByQuery(query));
        }

        public IEnumerable<IContentType> GetContentTypeDescendants(int id, bool andSelf)
        {
            return WithReadLockedContentTypes(repository =>
            {
                var descendants = new List<IContentType>();
                if (andSelf) descendants.Add(repository.Get(id));
                var ids = new Stack<int>();
                ids.Push(id);

                while (ids.Count > 0)
                {
                    var i = ids.Pop();
                    var query = Query<IContentType>.Builder.Where(x => x.ParentId == i);
                    var result = repository.GetByQuery(query).ToArray();

                    foreach (var c in result)
                    {
                        descendants.Add(c);
                        ids.Push(c.Id);
                    }
                }

                return descendants.ToArray();
            });
        }

        public IEnumerable<IContentType> GetContentTypesComposedOf(int id)
        {
            return WithReadLockedContentTypes(repository =>
            {
                // hash set handles duplicates
                var composed = new HashSet<IContentType>(new DelegateEqualityComparer<IContentType>(
                    (x, y) => x.Id == y.Id,
                    x => x.Id.GetHashCode()));

                var ids = new Stack<int>();
                ids.Push(id);

                while (ids.Count > 0)
                {
                    var i = ids.Pop();
                    var result = repository.GetTypesDirectlyComposedOf(i).ToArray();

                    foreach (var c in result)
                    {
                        composed.Add(c);
                        ids.Push(c.Id);
                    }
                }

                return composed.ToArray();
            });
        }

        /// <summary>
        /// Checks whether an <see cref="IContentType"/> item has any children
        /// </summary>
        /// <param name="id">Id of the <see cref="IContentType"/></param>
        /// <returns>True if the content type has any children otherwise False</returns>
        public bool HasChildren(int id)
        {
            var query = Query<IContentType>.Builder.Where(x => x.ParentId == id);
            return WithReadLockedContentTypes(repository => repository.Count(query) > 0);
        }

        #endregion

        #region Content - Save, Delete

        /// <summary>
        /// Saves a single <see cref="IContentType"/> object
        /// </summary>
        /// <param name="contentType"><see cref="IContentType"/> to save</param>
        /// <param name="userId">Optional id of the user saving the ContentType</param>
        public void Save(IContentType contentType, int userId = 0)
        {
	        if (SavingContentType.IsRaisedEventCancelled(new SaveEventArgs<IContentType>(contentType), this)) 
				return;

            var branch = false;

            WithWriteLockedContentAndContentTypes(repository =>
            {
                // validate the DAG transform, within the lock
                ValidateLocked(contentType); // throws if invalid

                contentType.CreatorId = userId;
                repository.AddOrUpdate(contentType); // also updates contents
                repository.UnitOfWork.Commit(); // commits the UOW but NOT the transaction

                // figure out content types impacted by the changes through composition
                var changedTypes = ComposeContentTypeChangesForTransactionEvent(contentType).ToArray();
                branch = changedTypes.Length > 1; // just that one, or those composed of
                OnTransactionRefreshedEntity(repository.UnitOfWork, changedTypes);
            });

            var changeType = branch ? TreeChangeTypes.RefreshBranch : TreeChangeTypes.RefreshNode;
            TreeChanged.RaiseEvent(new TreeChange<IContentTypeBase>(contentType, changeType).ToEventArgs(), this);
            SavedContentType.RaiseEvent(new SaveEventArgs<IContentType>(contentType, false), this);
	        Audit(AuditType.Save, string.Format("Save ContentType performed by user"), userId, contentType.Id);
        }

        /// <summary>
        /// Saves a collection of <see cref="IContentType"/> objects
        /// </summary>
        /// <param name="contentTypes">Collection of <see cref="IContentType"/> to save</param>
        /// <param name="userId">Optional id of the user saving the ContentType</param>
        public void Save(IEnumerable<IContentType> contentTypes, int userId = 0)
        {
            var contentTypesA = contentTypes.ToArray();

            if (SavingContentType.IsRaisedEventCancelled(new SaveEventArgs<IContentType>(contentTypesA), this)) 
				return;

            WithWriteLockedContentAndContentTypes(repository =>
            {
                // all-or-nothing, validate the DAG transforms, within the lock
                foreach (var contentType in contentTypesA)
                    ValidateLocked(contentType); // throws if invalid

                foreach (var contentType in contentTypesA)
                {
                    contentType.CreatorId = userId;
                    repository.AddOrUpdate(contentType); // also updates contents
                }

                repository.UnitOfWork.Commit(); // commits the UOW but NOT the transaction

                // figure out content types impacted by the changes through composition
                var changedTypes = ComposeContentTypeChangesForTransactionEvent(contentTypesA).ToArray();
                OnTransactionRefreshedEntity(repository.UnitOfWork, changedTypes);
            });

            // FIXME THIS IS COMPLETELY BROKEN
            var changeType = /*branch ? TreeChangeTypes.RefreshBranch :*/ TreeChangeTypes.RefreshNode;
            TreeChanged.RaiseEvent(contentTypesA.Select(x => new TreeChange<IContentTypeBase>(x, changeType)).ToEventArgs(), this);
            SavedContentType.RaiseEvent(new SaveEventArgs<IContentType>(contentTypesA, false), this);
	        Audit(AuditType.Save, string.Format("Save ContentTypes performed by user"), userId, -1);
        }

        /// <summary>
        /// Deletes a single <see cref="IContentType"/> object
        /// </summary>
        /// <param name="contentType"><see cref="IContentType"/> to delete</param>
        /// <param name="userId">Optional id of the user issueing the delete</param>
        /// <remarks>Deleting a <see cref="IContentType"/> will delete all the <see cref="IContent"/> objects based on this <see cref="IContentType"/></remarks>
        public void Delete(IContentType contentType, int userId = 0)
        {            
	        if (DeletingContentType.IsRaisedEventCancelled(new DeleteEventArgs<IContentType>(contentType), this)) 
				return;

            IContentTypeBase[] descendantsAndSelf = null;
            IContentTypeBase[] impacted = null;

            WithWriteLockedContentAndContentTypes(repository =>
            {
                // all descendants are going to be deleted
                descendantsAndSelf = contentType.DescendantsAndSelf().ToArray();

                // all impacted (through composition) probably lose some properties
                impacted = descendantsAndSelf.SelectMany(x => x.ComposedOf())
                    .Distinct()
                    .Except(descendantsAndSelf) // will be deleted anyway
                    .ToArray();

                // delete content
                foreach (var d in descendantsAndSelf)
                    _contentService.DeleteContentOfType(d.Id);

                // finally delete the content type
                // - recursively deletes all descendants
                // - deletes all associated property data
                //  (contents of any descendant type have been deleted but
                //   contents of any composed (impacted) type remain but
                //   need to have their property data cleared)
                repository.Delete(contentType);

                repository.UnitOfWork.Commit(); // commits the UOW but NOT the transaction

                // no need for 'transaction event' for deleted content
                // because deleting content does trigger its own event
                //
                // need 'transaction event' for content that have changed
                // ie those that were composed of the deleted content OR
                // of any of its descendants

                OnTransactionRefreshedEntity(repository.UnitOfWork, impacted);
            });

            using (ChangeSet.WithAmbient)
            {
                TreeChanged.RaiseEvent(descendantsAndSelf
                    .Select(x => new TreeChange<IContentTypeBase>(x, TreeChangeTypes.Remove)).ToEventArgs(), this);
                TreeChanged.RaiseEvent(impacted
                    .Select(x => new TreeChange<IContentTypeBase>(x, TreeChangeTypes.RefreshNode)).ToEventArgs(), this);
            }

            DeletedContentType.RaiseEvent(new DeleteEventArgs<IContentType>(contentType, false), this);
            Audit(AuditType.Delete, string.Format("Delete ContentType performed by user"), userId, contentType.Id);
        }

        /// <summary>
        /// Deletes a collection of <see cref="IContentType"/> objects.
        /// </summary>
        /// <param name="contentTypes">Collection of <see cref="IContentType"/> to delete</param>
        /// <param name="userId">Optional id of the user issueing the delete</param>
        /// <remarks>
        /// Deleting a <see cref="IContentType"/> will delete all the <see cref="IContent"/> objects based on this <see cref="IContentType"/>
        /// </remarks>
        public void Delete(IEnumerable<IContentType> contentTypes, int userId = 0)
        {
            var contentTypesA = contentTypes.ToArray();

            if (DeletingContentType.IsRaisedEventCancelled(new DeleteEventArgs<IContentType>(contentTypesA), this)) 
				return;

            IContentTypeBase[] allDescendantsAndSelf = null;
            IContentTypeBase[] impacted = null;

            WithWriteLockedContentAndContentTypes(repository =>
            {
                // all descendants are going to be deleted
                allDescendantsAndSelf = contentTypesA.SelectMany(x => x.DescendantsAndSelf())
                    .Distinct()
                    .ToArray();

                // all impacted (through composition) probably lose some properties
                impacted = allDescendantsAndSelf.SelectMany(x => x.ComposedOf())
                    .Distinct()
                    .Except(allDescendantsAndSelf) // will be deleted anyway
                    .ToArray();

                // delete content
                foreach (var d in allDescendantsAndSelf)
                    _contentService.DeleteContentOfType(d.Id);

                // finally delete the content types
                // (see notes in overload)
                foreach (var contentType in contentTypesA)
                    repository.Delete(contentType);

                repository.UnitOfWork.Commit(); // commits the UOW but NOT the transaction

                // (see notes in overload)
                OnTransactionRefreshedEntity(repository.UnitOfWork, impacted);
            });

            using (ChangeSet.WithAmbient)
            {
                TreeChanged.RaiseEvent(allDescendantsAndSelf
                    .Select(x => new TreeChange<IContentTypeBase>(x, TreeChangeTypes.Remove)).ToEventArgs(), this);
                TreeChanged.RaiseEvent(impacted
                    .Select(x => new TreeChange<IContentTypeBase>(x, TreeChangeTypes.RefreshNode)).ToEventArgs(), this);
            }

            DeletedContentType.RaiseEvent(new DeleteEventArgs<IContentType>(contentTypesA, false), this);
            Audit(AuditType.Delete, string.Format("Delete ContentTypes performed by user"), userId, -1);
        }

        #endregion

        #region Content - Move

        // not implemented
        // would update PATH, etc
        // but what would happen with PARENT vs COMPOSITION?

        #endregion

        #region Media - Get, Has, Is

        /// <summary>
        /// Gets an <see cref="IMediaType"/> object by its Id
        /// </summary>
        /// <param name="id">Id of the <see cref="IMediaType"/> to retrieve</param>
        /// <returns><see cref="IMediaType"/></returns>
        public IMediaType GetMediaType(int id)
        {
            return WithReadLockedMediaTypes(repository => repository.Get(id));
        }

        /// <summary>
        /// Gets an <see cref="IMediaType"/> object by its Alias
        /// </summary>
        /// <param name="alias">Alias of the <see cref="IMediaType"/> to retrieve</param>
        /// <returns><see cref="IMediaType"/></returns>
        public IMediaType GetMediaType(string alias)
        {
            var query = Query<IMediaType>.Builder.Where(x => x.Alias == alias);
            return WithReadLockedMediaTypes(repository => repository.GetByQuery(query).FirstOrDefault());
        }

        /// <summary>
        /// Gets a list of all available <see cref="IMediaType"/> objects
        /// </summary>
        /// <param name="ids">Optional list of ids</param>
        /// <returns>An Enumerable list of <see cref="IMediaType"/> objects</returns>
        public IEnumerable<IMediaType> GetAllMediaTypes(params int[] ids)
        {
            return WithReadLockedMediaTypes(repository => repository.GetAll(ids));
        }

        /// <summary>
        /// Gets a list of children for a <see cref="IMediaType"/> object
        /// </summary>
        /// <param name="id">Id of the Parent</param>
        /// <returns>An Enumerable list of <see cref="IMediaType"/> objects</returns>
        public IEnumerable<IMediaType> GetMediaTypeChildren(int id)
        {
            var query = Query<IMediaType>.Builder.Where(x => x.ParentId == id);
            return WithReadLockedMediaTypes(repository => repository.GetByQuery(query));
        }

        /// <summary>
        /// Checks whether an <see cref="IMediaType"/> item has any children
        /// </summary>
        /// <param name="id">Id of the <see cref="IMediaType"/></param>
        /// <returns>True if the media type has any children otherwise False</returns>
        public bool MediaTypeHasChildren(int id)
        {
            var query = Query<IMediaType>.Builder.Where(x => x.ParentId == id);
            return WithReadLockedMediaTypes(repository => repository.Count(query) > 0);
        }

        #endregion

        #region Media - Save, Delete

        /// <summary>
        /// Saves a single <see cref="IMediaType"/> object
        /// </summary>
        /// <param name="mediaType"><see cref="IMediaType"/> to save</param>
        /// <param name="userId">Optional Id of the user saving the MediaType</param>
        public void Save(IMediaType mediaType, int userId = 0)
        {
	        if (SavingMediaType.IsRaisedEventCancelled(new SaveEventArgs<IMediaType>(mediaType), this)) 
				return;

            WithWriteLockedMediaAndMediaTypes(repository =>
            {
                // validate the DAG transform, within the lock
                ValidateLocked(mediaType); // throws if invalid

                mediaType.CreatorId = userId;
                repository.AddOrUpdate(mediaType); // also updates contents
                repository.UnitOfWork.Commit(); // commits the UOW but NOT the transaction

                // figure out content types impacted by the changes through composition
                var changedTypes = ComposeContentTypeChangesForTransactionEvent(mediaType).ToArray();
                OnTransactionRefreshedEntity(repository.UnitOfWork, changedTypes);
            });

            // fixme - raise distributed events
            // raise event fot that type only, because it's a distributed event,
            // so impacted types (through composition) will be determined locally
            // fixme - can we do it OR do we need extra infos eg RefreshTypeLocally, RefreshTypeComposition
            //Changed.RaiseEvent(new ChangeEventArgs(mediaType), this);
            //ApplyChangesToContent(changedTypes);

            SavedMediaType.RaiseEvent(new SaveEventArgs<IMediaType>(mediaType, false), this);
	        Audit(AuditType.Save, string.Format("Save MediaType performed by user"), userId, mediaType.Id);
        }

        /// <summary>
        /// Saves a collection of <see cref="IMediaType"/> objects
        /// </summary>
        /// <param name="mediaTypes">Collection of <see cref="IMediaType"/> to save</param>
        /// <param name="userId">Optional Id of the user savging the MediaTypes</param>
        public void Save(IEnumerable<IMediaType> mediaTypes, int userId = 0)
        {
            var mediaTypesA = mediaTypes.ToArray();

            if (SavingMediaType.IsRaisedEventCancelled(new SaveEventArgs<IMediaType>(mediaTypesA), this))
				return;

            WithWriteLockedMediaAndMediaTypes(repository =>
            {
                // all-or-nothing, validate the DAG transforms, within the lock
                foreach (var mediaType in mediaTypesA)
                    ValidateLocked(mediaType); // throws if invalid

                foreach (var mediaType in mediaTypesA)
                {
                    mediaType.CreatorId = userId;
                    repository.AddOrUpdate(mediaType); // also updates contents
                }

                repository.UnitOfWork.Commit(); // commits the UOW but NOT the transaction

                // figure out content types impacted by the changes through composition
                var changedTypes = ComposeContentTypeChangesForTransactionEvent(mediaTypesA).ToArray();
                OnTransactionRefreshedEntity(repository.UnitOfWork, changedTypes);
            });

            // fixme - raise distributed events
            // raise event fot that type only, because it's a distributed event,
            // so impacted types (through composition) will be determined locally
            // fixme - can we do it OR do we need extra infos eg RefreshTypeLocally, RefreshTypeComposition
            //Changed.RaiseEvent(new ChangeEventArgs(contentTypesA), this);

            SavedMediaType.RaiseEvent(new SaveEventArgs<IMediaType>(mediaTypesA, false), this);
			Audit(AuditType.Save, string.Format("Save MediaTypes performed by user"), userId, -1);
        }

        /// <summary>
        /// Deletes a single <see cref="IMediaType"/> object
        /// </summary>
        /// <param name="mediaType"><see cref="IMediaType"/> to delete</param>
        /// <param name="userId">Optional Id of the user deleting the MediaType</param>
        /// <remarks>Deleting a <see cref="IMediaType"/> will delete all the <see cref="IMedia"/> objects based on this <see cref="IMediaType"/></remarks>
        public void Delete(IMediaType mediaType, int userId = 0)
        {
	        if (DeletingMediaType.IsRaisedEventCancelled(new DeleteEventArgs<IMediaType>(mediaType), this)) 
				return;

            WithWriteLockedMediaAndMediaTypes(repository =>
            {
                // all descendants are going to be deleted
                var descendantsAndSelf = mediaType.DescendantsAndSelf().ToArray();

                // all impacted (through composition) probably lose some properties
                var impacted = descendantsAndSelf.SelectMany(x => x.ComposedOf())
                    .Distinct()
                    .Except(descendantsAndSelf) // will be deleted anyway
                    .ToArray();

                // delete content
                foreach (var d in descendantsAndSelf)
                    _mediaService.DeleteMediaOfType(d.Id);

                // finally delete the content type
                // - recursively deletes all descendants
                // - deletes all associated property data
                //  (contents of any descendant type have been deleted but
                //   contents of any composed (impacted) type remain but
                //   need to have their property data cleared)
                repository.Delete(mediaType);

                repository.UnitOfWork.Commit(); // commits the UOW but NOT the transaction

                // no need for 'transaction event' for deleted content
                // because deleting content does trigger its own event
                //
                // need 'transaction event' for content that have changed
                // ie those that were composed of the deleted content OR
                // of any of its descendants

                OnTransactionRefreshedEntity(repository.UnitOfWork, impacted);
            });

            // fixme - raise distributed events

            DeletedMediaType.RaiseEvent(new DeleteEventArgs<IMediaType>(mediaType, false), this);
            Audit(AuditType.Delete, string.Format("Delete MediaType performed by user"), userId, mediaType.Id);
        }

        /// <summary>
        /// Deletes a collection of <see cref="IMediaType"/> objects
        /// </summary>
        /// <param name="mediaTypes">Collection of <see cref="IMediaType"/> to delete</param>
        /// <param name="userId"></param>
        /// <remarks>Deleting a <see cref="IMediaType"/> will delete all the <see cref="IMedia"/> objects based on this <see cref="IMediaType"/></remarks>
        public void Delete(IEnumerable<IMediaType> mediaTypes, int userId = 0)
        {
            var mediaTypesA = mediaTypes.ToArray();

            if (DeletingMediaType.IsRaisedEventCancelled(new DeleteEventArgs<IMediaType>(mediaTypesA), this)) 
				return;

            WithWriteLockedMediaAndMediaTypes(repository =>
            {
                // all descendants are going to be deleted
                var allDescendantsAndSelf = mediaTypesA.SelectMany(x => x.DescendantsAndSelf())
                    .Distinct()
                    .ToArray();

                // all impacted (through composition) probably lose some properties
                var impacted = allDescendantsAndSelf.SelectMany(x => x.ComposedOf())
                    .Distinct()
                    .Except(allDescendantsAndSelf) // will be deleted anyway
                    .ToArray();

                // delete content
                foreach (var d in allDescendantsAndSelf)
                    _mediaService.DeleteMediaOfType(d.Id);

                // finally delete the content types
                // (see notes in overload)
                foreach (var mediaType in mediaTypesA)
                    repository.Delete(mediaType);

                repository.UnitOfWork.Commit(); // commits the UOW but NOT the transaction

                // (see notes in overload)
                OnTransactionRefreshedEntity(repository.UnitOfWork, impacted);
            });

            // fixme - raise distributed events
            // allDescendantsAndSelf: report REMOVE
            // impacted: report REFRESH
            // BUT async => just report WTF?!

            DeletedMediaType.RaiseEvent(new DeleteEventArgs<IMediaType>(mediaTypesA, false), this);
            Audit(AuditType.Delete, string.Format("Delete MediaTypes performed by user"), userId, -1);
        }

        #endregion

        #region Media - Move

        // not implemented
        // (see content)

        #endregion

        private void Audit(AuditType type, string message, int userId, int objectId)
        {
            var uow = UowProvider.GetUnitOfWork();
            using (var auditRepo = RepositoryFactory.CreateAuditRepository(uow))
            {
                auditRepo.AddOrUpdate(new AuditItem(objectId, message, type, userId));
                uow.Commit();
            }
        }
        
        #region Event Handlers

		/// <summary>
		/// Occurs before Delete
		/// </summary>
		public static event TypedEventHandler<IContentTypeService, DeleteEventArgs<IContentType>> DeletingContentType;

		/// <summary>
		/// Occurs after Delete
		/// </summary>
		public static event TypedEventHandler<IContentTypeService, DeleteEventArgs<IContentType>> DeletedContentType;
		
		/// <summary>
		/// Occurs before Delete
		/// </summary>
		public static event TypedEventHandler<IContentTypeService, DeleteEventArgs<IMediaType>> DeletingMediaType;

		/// <summary>
		/// Occurs after Delete
		/// </summary>
		public static event TypedEventHandler<IContentTypeService, DeleteEventArgs<IMediaType>> DeletedMediaType;
		
        /// <summary>
        /// Occurs before Save
        /// </summary>
		public static event TypedEventHandler<IContentTypeService, SaveEventArgs<IContentType>> SavingContentType;

        /// <summary>
        /// Occurs after Save
        /// </summary>
		public static event TypedEventHandler<IContentTypeService, SaveEventArgs<IContentType>> SavedContentType;

		/// <summary>
		/// Occurs before Save
		/// </summary>
		public static event TypedEventHandler<IContentTypeService, SaveEventArgs<IMediaType>> SavingMediaType;

		/// <summary>
		/// Occurs after Save
		/// </summary>
		public static event TypedEventHandler<IContentTypeService, SaveEventArgs<IMediaType>> SavedMediaType;

        /// <summary>
        /// Occurs after change.
        /// </summary>
        internal static event TypedEventHandler<IContentTypeService, TreeChange<IContentTypeBase>.EventArgs> TreeChanged;
        
        #endregion
    }
}