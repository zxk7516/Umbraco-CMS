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
        {
            InitializeEventsRelay();
        }

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

        #endregion
    }
}