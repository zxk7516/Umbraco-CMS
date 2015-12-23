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
    internal class ContentTypeService : ContentTypeServiceBase<ContentTypeRepository, IContentType>, IContentTypeService
    {
	    private IContentService _contentService;

        #region Constructor

        public ContentTypeService(IDatabaseUnitOfWorkProvider provider, RepositoryFactory repositoryFactory, ILogger logger, IEventMessagesFactory eventMessagesFactory)
            : base(provider, repositoryFactory, logger, eventMessagesFactory,
                new LockingRepository<ContentTypeRepository>(provider,
                    uow => repositoryFactory.CreateContentTypeRepository(uow) as ContentTypeRepository,
                    LockingRepositoryReadLockIds, LockingRepositoryWriteLockIds))
            InitializeEventsRelay();
        }

#error the whole mess below wants to be refactored
        #region Containers

        public Attempt<int> CreateContentTypeContainer(int parentId, string name, int userId = 0)
        {
            var uow = UowProvider.GetUnitOfWork();
            using (var repo = RepositoryFactory.CreateEntityContainerRepository(uow))
            {
                try
                {
                    var container = new EntityContainer(Constants.ObjectTypes.DocumentTypeGuid)
                    {
                        Name = name,
                        ParentId = parentId,
                        CreatorId = userId
                    };
                    repo.AddOrUpdate(container);
                    uow.Commit();
                    return Attempt.Succeed(container.Id);
                }
                catch (Exception ex)
                {
                    return Attempt<int>.Fail(ex);
                }
                //TODO: Audit trail ?
            }
        }

        public Attempt<int> CreateMediaTypeContainer(int parentId, string name, int userId = 0)
        {
            var uow = UowProvider.GetUnitOfWork();
            using (var repo = RepositoryFactory.CreateEntityContainerRepository(uow))
            {
                try
                {
                    var container = new EntityContainer(Constants.ObjectTypes.MediaTypeGuid)
                    {
                        Name = name,
                        ParentId = parentId,
                        CreatorId = userId
                    };
                    repo.AddOrUpdate(container);
                    uow.Commit();
                    return Attempt.Succeed(container.Id);
                }
                catch (Exception ex)
                {
                    return Attempt<int>.Fail(ex);
                }
                //TODO: Audit trail ?
            }
        }

        public void SaveContentTypeContainer(EntityContainer container, int userId = 0)
        {
            SaveContainer(container, Constants.ObjectTypes.DocumentTypeGuid, "document type", userId);
        }

        public void SaveMediaTypeContainer(EntityContainer container, int userId = 0)
        {
            SaveContainer(container, Constants.ObjectTypes.MediaTypeGuid, "media type", userId);
        }

        private void SaveContainer(EntityContainer container, Guid containedObjectType, string objectTypeName, int userId)
        {
            if (container.ContainedObjectType != containedObjectType)
                throw new InvalidOperationException("Not a " + objectTypeName + " container.");
            if (container.HasIdentity && container.IsPropertyDirty("ParentId"))
                throw new InvalidOperationException("Cannot save a container with a modified parent, move the container instead.");

            var uow = UowProvider.GetUnitOfWork();
            using (var repo = RepositoryFactory.CreateEntityContainerRepository(uow))
            {
                repo.AddOrUpdate(container);
                uow.Commit();
                //TODO: Audit trail ?
            }
        }

        public EntityContainer GetContentTypeContainer(int containerId)
        {
            return GetContainer(containerId, Constants.ObjectTypes.DocumentTypeGuid);
        }

        public EntityContainer GetMediaTypeContainer(int containerId)
        {
            return GetContainer(containerId, Constants.ObjectTypes.MediaTypeGuid);
        }

        private EntityContainer GetContainer(int containerId, Guid containedObjectType)
        {
            var uow = UowProvider.GetUnitOfWork();
            using (var repo = RepositoryFactory.CreateEntityContainerRepository(uow))
            {
                var container = repo.Get(containerId);
                return container != null && container.ContainedObjectType == containedObjectType
                    ? container
                    : null;
            }
        }

        public EntityContainer GetContentTypeContainer(Guid containerId)
        {
            return GetContainer(containerId, Constants.ObjectTypes.DocumentTypeGuid);
        }

        public EntityContainer GetMediaTypeContainer(Guid containerId)
        {
            return GetContainer(containerId, Constants.ObjectTypes.MediaTypeGuid);
        }

        private EntityContainer GetContainer(Guid containerId, Guid containedObjectType)
        {
            var uow = UowProvider.GetUnitOfWork();
            using (var repo = RepositoryFactory.CreateEntityContainerRepository(uow))
            {
                var container = repo.Get(containerId);
                return container != null && container.ContainedObjectType == containedObjectType
                    ? container
                    : null;
            }
        }

        public void DeleteContentTypeContainer(int containerId, int userId = 0)
        {
            var uow = UowProvider.GetUnitOfWork();
            using (var repo = RepositoryFactory.CreateEntityContainerRepository(uow))
            {
                var container = repo.Get(containerId);
                if (container == null) return;
                if (container.ContainedObjectType != Constants.ObjectTypes.DocumentTypeGuid) return;
                repo.Delete(container);
                uow.Commit();
                //TODO: Audit trail ?
            }
        }

        public void DeleteMediaTypeContainer(int containerId, int userId = 0)
        {
            var uow = UowProvider.GetUnitOfWork();
            using (var repo = RepositoryFactory.CreateEntityContainerRepository(uow))
            {
                var container = repo.Get(containerId);
                if (container == null) return;
                if (container.ContainedObjectType != Constants.ObjectTypes.MediaTypeGuid) return;
                repo.Delete(container);
                uow.Commit();
                //TODO: Audit trail ?
            }
        }

        #endregion

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
//fixme wtf error

        internal IContentService ContentService
        {
            get
            {
                if (_contentService == null)
                    throw new InvalidOperationException("ContentTypeService.ContentService has not been initialized.");
                return _contentService;
            }
            set { _contentService = value; }
        }

        #endregion

        // constants
        private static readonly int[] LockingRepositoryReadLockIds = { Constants.System.ContentTypesLock };
        private static readonly int[] LockingRepositoryWriteLockIds = { Constants.System.ContentTreeLock, Constants.System.ContentTypesLock };

        protected override void DeleteItemsOfTypes(IEnumerable<int> typeIds)
        {
            foreach (var typeId in typeIds)
                ContentService.DeleteContentOfType(typeId);
        }

        public IEnumerable<string> GetAllPropertyTypeAliases()
        {
            return LRepo.WithReadLocked(xr => xr.Repository.GetAllPropertyTypeAliases());
        }

        #region Legacy Events

        // NOTE
        // these are temporary events that are here only during a transition phase
        // so that it is not too difficult to test-run version 7 of NuCache 
        // TO BE REMOVED with version 8

        private void InitializeEventsRelay()
        {
#pragma warning disable 618 // obsolete
#error refactor moves! to base!
        public Attempt<OperationStatus<MoveOperationStatusType>> MoveMediaType(IMediaType toMove, int containerId)
        {
            var evtMsgs = EventMessagesFactory.Get();
            
            if (MovingMediaType.IsRaisedEventCancelled(
                  new MoveEventArgs<IMediaType>(evtMsgs, new MoveEventInfo<IMediaType>(toMove, toMove.Path, containerId)),
                  this))
            {
                return Attempt.Fail(
                    new OperationStatus<MoveOperationStatusType>(
                        MoveOperationStatusType.FailedCancelledByEvent, evtMsgs));
            }

            var moveInfo = new List<MoveEventInfo<IMediaType>>();
            var uow = UowProvider.GetUnitOfWork();
            using (var containerRepository = RepositoryFactory.CreateEntityContainerRepository(uow))
            using (var repository = RepositoryFactory.CreateMediaTypeRepository(uow))
            {
                try
                {
                    EntityContainer container = null;
                    if (containerId > 0)
                    {
                        container = containerRepository.Get(containerId);
                        if (container == null || container.ContainedObjectType != Constants.ObjectTypes.MediaTypeGuid)
                            throw new DataOperationException<MoveOperationStatusType>(MoveOperationStatusType.FailedParentNotFound);
                    }
                    moveInfo.AddRange(repository.Move(toMove, container));
                }
                catch (DataOperationException<MoveOperationStatusType> ex)
                {
                    return Attempt.Fail(
                        new OperationStatus<MoveOperationStatusType>(ex.Operation, evtMsgs));
                }
                uow.Commit();
            }

            MovedMediaType.RaiseEvent(new MoveEventArgs<IMediaType>(false, evtMsgs, moveInfo.ToArray()), this);

            return Attempt.Succeed(
                new OperationStatus<MoveOperationStatusType>(MoveOperationStatusType.Success, evtMsgs));
        }

        public Attempt<OperationStatus<MoveOperationStatusType>> MoveContentType(IContentType toMove, int containerId)
        {
            var evtMsgs = EventMessagesFactory.Get();

            if (MovingContentType.IsRaisedEventCancelled(
                  new MoveEventArgs<IContentType>(evtMsgs, new MoveEventInfo<IContentType>(toMove, toMove.Path, containerId)),
                  this))
            {
                return Attempt.Fail(
                    new OperationStatus<MoveOperationStatusType>(
                        MoveOperationStatusType.FailedCancelledByEvent, evtMsgs));
            }

            var moveInfo = new List<MoveEventInfo<IContentType>>();
            var uow = UowProvider.GetUnitOfWork();
            using (var containerRepository = RepositoryFactory.CreateEntityContainerRepository(uow)) 
            using (var repository = RepositoryFactory.CreateContentTypeRepository(uow))
            {
                try
                {
                    EntityContainer container = null;
                    if (containerId > 0)
                    {
                        container = containerRepository.Get(containerId);
                        if (container == null || container.ContainedObjectType != Constants.ObjectTypes.DocumentTypeGuid)
                            throw new DataOperationException<MoveOperationStatusType>(MoveOperationStatusType.FailedParentNotFound);
                    }
                    moveInfo.AddRange(repository.Move(toMove, container));
                }
                catch (DataOperationException<MoveOperationStatusType> ex)
                {
                    return Attempt.Fail(
                        new OperationStatus<MoveOperationStatusType>(ex.Operation, evtMsgs));
                }
                uow.Commit();
            }

            MovedContentType.RaiseEvent(new MoveEventArgs<IContentType>(false, evtMsgs, moveInfo.ToArray()), this);

            return Attempt.Succeed(
                new OperationStatus<MoveOperationStatusType>(MoveOperationStatusType.Success, evtMsgs));
        }

            Saving += (sender, args) => SavingContentType.RaiseEvent(args, (IContentTypeService) sender);
            Saved += (sender, args) => SavedContentType.RaiseEvent(args, (IContentTypeService) sender);
            Deleting += (sender, args) => DeletingContentType.RaiseEvent(args, (IContentTypeService) sender);
            Deleted += (sender, args) => DeletedContentType.RaiseEvent(args, (IContentTypeService) sender);

            // cannot use 'sender' (MediaTypeService) so using 'this'
            MediaTypeService.Saving += (sender, args) => SavingMediaType.RaiseEvent(args, this /*(IMediaTypeService) sender*/);
            MediaTypeService.Saved += (sender, args) => SavedMediaType.RaiseEvent(args, this /*(IMediaTypeService) sender*/);
            MediaTypeService.Deleting += (sender, args) => DeletingMediaType.RaiseEvent(args, this /*(IMediaTypeService) sender*/);
            MediaTypeService.Deleted += (sender, args) => DeletedMediaType.RaiseEvent(args, this /*(IMediaTypeService) sender*/);
#pragma warning restore 618
        }

        [Obsolete("Use the ContentTypeService.Saving event.", false)]
        public static event TypedEventHandler<IContentTypeService, SaveEventArgs<IContentType>> SavingContentType;
        [Obsolete("Use the ContentTypeService.Saved event.", false)]
        public static event TypedEventHandler<IContentTypeService, SaveEventArgs<IContentType>> SavedContentType;
        [Obsolete("Use the ContentTypeService.Deleting event.", false)]
        public static event TypedEventHandler<IContentTypeService, DeleteEventArgs<IContentType>> DeletingContentType;
        [Obsolete("Use the ContentTypeService.Deleted event.", false)]
        public static event TypedEventHandler<IContentTypeService, DeleteEventArgs<IContentType>> DeletedContentType;

        [Obsolete("Use the MediaTypeService.Saving event.", false)]
        public static event TypedEventHandler<IContentTypeService, SaveEventArgs<IMediaType>> SavingMediaType;
        [Obsolete("Use the MediaTypeService.Saved event.", false)]
        public static event TypedEventHandler<IContentTypeService, SaveEventArgs<IMediaType>> SavedMediaType;
        [Obsolete("Use the MediaTypeService.Deleting event.", false)]
        public static event TypedEventHandler<IContentTypeService, DeleteEventArgs<IMediaType>> DeletingMediaType;
        [Obsolete("Use the MediaTypeService.Deleted event.", false)]
        public static event TypedEventHandler<IContentTypeService, DeleteEventArgs<IMediaType>> DeletedMediaType;

#error refactor!
        /// <summary>
        /// Occurs before Move
        /// </summary>
        public static event TypedEventHandler<IContentTypeService, MoveEventArgs<IMediaType>> MovingMediaType;

        /// <summary>
        /// Occurs after Move
        /// </summary>
        public static event TypedEventHandler<IContentTypeService, MoveEventArgs<IMediaType>> MovedMediaType;

        /// <summary>
        /// Occurs before Move
        /// </summary>
        public static event TypedEventHandler<IContentTypeService, MoveEventArgs<IContentType>> MovingContentType;

        /// <summary>
        /// Occurs after Move
        /// </summary>
        public static event TypedEventHandler<IContentTypeService, MoveEventArgs<IContentType>> MovedContentType;

        #endregion
    }
}