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

        public ContentTypeService(IDatabaseUnitOfWorkProvider provider, RepositoryFactory repositoryFactory, ILogger logger)
            : base(provider, repositoryFactory, logger,
                new LockingRepository<ContentTypeRepository>(provider,
                    uow => repositoryFactory.CreateContentTypeRepository(uow) as ContentTypeRepository,
                    LockingRepositoryReadLockIds, LockingRepositoryWriteLockIds))
        { }

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
    }
}