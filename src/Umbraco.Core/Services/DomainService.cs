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
    public class DomainService : RepositoryService, IDomainService
    {
        #region Constructors

        public DomainService(IDatabaseUnitOfWorkProvider provider, RepositoryFactory repositoryFactory, ILogger logger, IEventMessagesFactory eventMessagesFactory)
            : base(provider, repositoryFactory, logger, eventMessagesFactory)
        {
            _lrepo = new LockingRepository<DomainRepository>(UowProvider,
                uow => RepositoryFactory.CreateDomainRepository(uow) as DomainRepository,
                LockingRepositoryLockIds, LockingRepositoryLockIds);
        }

        #endregion

        #region Locking

        // constant
        private static readonly int[] LockingRepositoryLockIds = { Constants.System.DomainsLock };

        internal void WithReadLocked(Action<DomainRepository> action, bool autoCommit = true)
        {
            _lrepo.WithReadLocked(xr => action(xr.Repository), autoCommit);
        }

        private readonly LockingRepository<DomainRepository> _lrepo;

        #endregion

        #region Service

        public bool Exists(string domainName)
        {
            return _lrepo.WithReadLocked(lr => lr.Repository.Exists(domainName));
        }

        public Attempt<OperationStatus> Delete(IDomain domain)
        {
            var evtMsgs = EventMessagesFactory.Get();
            if (Deleting.IsRaisedEventCancelled(
                   new DeleteEventArgs<IDomain>(domain, evtMsgs),
                   this))
            {
                return Attempt.Fail(OperationStatus.Cancelled(evtMsgs));
            }

            _lrepo.WithWriteLocked(lr => lr.Repository.Delete(domain));

            var args = new DeleteEventArgs<IDomain>(domain, false, evtMsgs);
            Deleted.RaiseEvent(args, this);
            return Attempt.Succeed(OperationStatus.Success(evtMsgs));
        }

        public IDomain GetByName(string name)
        {
            return _lrepo.WithReadLocked(lr => lr.Repository.GetByName(name));
        }

        public IDomain GetById(int id)
        {
            return _lrepo.WithReadLocked(lr => lr.Repository.Get(id));
        }

        public IEnumerable<IDomain> GetAll(bool includeWildcards)
        {
            return _lrepo.WithReadLocked(lr => lr.Repository.GetAll(includeWildcards));
        }

        public IEnumerable<IDomain> GetAssignedDomains(int contentId, bool includeWildcards)
        {
            return _lrepo.WithReadLocked(lr => lr.Repository.GetAssignedDomains(contentId, includeWildcards));
        }

        public Attempt<OperationStatus> Save(IDomain domainEntity)
        {
            var evtMsgs = EventMessagesFactory.Get();
            if (Saving.IsRaisedEventCancelled(
                    new SaveEventArgs<IDomain>(domainEntity, evtMsgs),
                    this))
            {
                return Attempt.Fail(OperationStatus.Cancelled(evtMsgs));
            }

            _lrepo.WithWriteLocked(lr => lr.Repository.AddOrUpdate(domainEntity));

            Saved.RaiseEvent(new SaveEventArgs<IDomain>(domainEntity, false, evtMsgs), this);
            return Attempt.Succeed(OperationStatus.Success(evtMsgs));
        }

        #endregion

        #region Event Handlers

        /// <summary>
        /// Occurs before Delete
        /// </summary>		
        public static event TypedEventHandler<IDomainService, DeleteEventArgs<IDomain>> Deleting;

        /// <summary>
        /// Occurs after Delete
        /// </summary>
        public static event TypedEventHandler<IDomainService, DeleteEventArgs<IDomain>> Deleted;
      
        /// <summary>
        /// Occurs before Save
        /// </summary>
        public static event TypedEventHandler<IDomainService, SaveEventArgs<IDomain>> Saving;

        /// <summary>
        /// Occurs after Save
        /// </summary>
        public static event TypedEventHandler<IDomainService, SaveEventArgs<IDomain>> Saved;
      
        #endregion
    }
}