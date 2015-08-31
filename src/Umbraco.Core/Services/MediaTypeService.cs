using System;
using System.Collections.Generic;
using Umbraco.Core.Events;
using Umbraco.Core.Logging;
using Umbraco.Core.Models;
using Umbraco.Core.Persistence;
using Umbraco.Core.Persistence.Repositories;
using Umbraco.Core.Persistence.UnitOfWork;

namespace Umbraco.Core.Services
{
    internal class MediaTypeService : ContentTypeServiceBase<MediaTypeRepository, IMediaType>, IMediaTypeService
    {
        private IMediaService _mediaService;

        #region Constructor

        public MediaTypeService(IDatabaseUnitOfWorkProvider provider, RepositoryFactory repositoryFactory, ILogger logger, IEventMessagesFactory eventMessagesFactory)
            : base(provider, repositoryFactory, logger, eventMessagesFactory,
                new LockingRepository<MediaTypeRepository>(provider,
                    uow => repositoryFactory.CreateMediaTypeRepository(uow) as MediaTypeRepository,
                    LockingRepositoryReadLockIds, LockingRepositoryWriteLockIds))
        { }

        internal IMediaService MediaService
        {
            get
            {
                if (_mediaService == null)
                    throw new InvalidOperationException("MediaTypeService.MediaService has not been initialized.");
                return _mediaService;
            }
            set { _mediaService = value; }
        }

        #endregion

        // constants
        private static readonly int[] LockingRepositoryReadLockIds = { Constants.System.MediaTypesLock };
        private static readonly int[] LockingRepositoryWriteLockIds = { Constants.System.MediaTreeLock, Constants.System.MediaTypesLock };

        protected override void DeleteItemsOfTypes(IEnumerable<int> typeIds)
        {
            foreach (var typeId in typeIds)
                MediaService.DeleteMediaOfType(typeId);
        }
    }
}
