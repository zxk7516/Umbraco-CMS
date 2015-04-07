using System;
using System.Collections.Generic;
using System.Linq;
using Umbraco.Core.Logging;
using Umbraco.Core.Models;
using Umbraco.Core.Persistence;
using Umbraco.Core.Persistence.Repositories;
using Umbraco.Core.Persistence.UnitOfWork;

namespace Umbraco.Core.Services
{
    internal class MemberTypeService : ContentTypeServiceBase<MemberTypeRepository, IMemberType>, IMemberTypeService
    {
        private IMemberService _memberService;

        #region Constructor

        public MemberTypeService(IDatabaseUnitOfWorkProvider provider, RepositoryFactory repositoryFactory, ILogger logger)
            : base(provider, repositoryFactory, logger,
                new LockingRepository<MemberTypeRepository>(provider,
                    uow => repositoryFactory.CreateMemberTypeRepository(uow) as MemberTypeRepository,
                    LockingRepositoryReadLockIds, LockingRepositoryWriteLockIds))
        { }

        internal IMemberService MemberService
        {
            get
            {
                if (_memberService == null)
                    throw new InvalidOperationException("MemberTypeService.MemberService has not been initialized.");
                return _memberService;
            }
            set { _memberService = value; }
        }

        #endregion

        // constants
        private static readonly int[] LockingRepositoryReadLockIds = { Constants.System.MemberTypesLock };
        private static readonly int[] LockingRepositoryWriteLockIds = { Constants.System.MemberTreeLock, Constants.System.MemberTypesLock };

        protected override void DeleteItemsOfTypes(IEnumerable<int> typeIds)
        {
            foreach (var typeId in typeIds)
                MemberService.DeleteMembersOfType(typeId);
        }

        public string GetDefault()
        {
            return LRepo.WithReadLocked(xr =>
            {
                var types = xr.Repository.GetAll().Select(x => x.Alias).ToArray();

                if (types.Any() == false)
                    throw new InvalidOperationException("No member types could be resolved");

                return types.FirstOrDefault(x => x.InvariantEquals("Member")) ?? types[0];
            });
        }
    }
}