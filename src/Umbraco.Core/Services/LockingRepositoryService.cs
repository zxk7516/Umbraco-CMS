using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using Umbraco.Core.Logging;
using Umbraco.Core.Persistence;
using Umbraco.Core.Persistence.Repositories;
using Umbraco.Core.Persistence.UnitOfWork;

namespace Umbraco.Core.Services
{
    internal class LockingRepository<TRepository>
        where TRepository : IDisposable, IRepository
    {
        private readonly IDatabaseUnitOfWorkProvider _uowProvider;
        private readonly Func<IDatabaseUnitOfWork, TRepository> _repositoryFactory;
        private readonly int[] _readLockIds, _writeLockIds;

        public LockingRepository(IDatabaseUnitOfWorkProvider uowProvider, Func<IDatabaseUnitOfWork, TRepository> repositoryFactory,
            IEnumerable<int> readLockIds, IEnumerable<int> writeLockIds)
        {
            Mandate.ParameterNotNull(uowProvider, "uowProvider");
            Mandate.ParameterNotNull(repositoryFactory, "repositoryFactory");

            _uowProvider = uowProvider;
            _repositoryFactory = repositoryFactory;
            _readLockIds = readLockIds == null ? new int[0] : readLockIds.ToArray();
            _writeLockIds = writeLockIds == null ? new int[0] : writeLockIds.ToArray();
        }

        public void WithReadLocked(Action<LockedRepository<TRepository>> action, bool autoCommit = true)
        {
            var uow = _uowProvider.GetUnitOfWork();
            using (var transaction = uow.Database.GetTransaction(IsolationLevel.RepeatableRead))
            {
                foreach (var lockId in _readLockIds)
                    uow.Database.AcquireLockNodeReadLock(lockId);

                using (var repository = _repositoryFactory(uow))
                {
                    action(new LockedRepository<TRepository>(transaction, uow, repository));
                    if (autoCommit == false) return;
                    uow.Commit();
                    transaction.Complete();
                }
            }
        }

        public TResult WithReadLocked<TResult>(Func<LockedRepository<TRepository>, TResult> func, bool autoCommit = true)
        {
            var uow = _uowProvider.GetUnitOfWork();
            using (var transaction = uow.Database.GetTransaction(IsolationLevel.RepeatableRead))
            {
                foreach (var lockId in _readLockIds)
                    uow.Database.AcquireLockNodeReadLock(lockId);

                using (var repository = _repositoryFactory(uow))
                {
                    var ret = func(new LockedRepository<TRepository>(transaction, uow, repository));
                    if (autoCommit == false) return ret;
                    uow.Commit();
                    transaction.Complete();
                    return ret;
                }
            }
        }

        public void WithWriteLocked(Action<LockedRepository<TRepository>> action, bool autoCommit = true)
        {
            var uow = _uowProvider.GetUnitOfWork();
            using (var transaction = uow.Database.GetTransaction(IsolationLevel.RepeatableRead))
            {
                foreach (var lockId in _writeLockIds)
                    uow.Database.AcquireLockNodeWriteLock(lockId);

                using (var repository = _repositoryFactory(uow))
                {
                    action(new LockedRepository<TRepository>(transaction, uow, repository));
                    if (autoCommit == false) return;
                    uow.Commit();
                    transaction.Complete();
                }
            }
        }

        public TResult WithWriteLocked<TResult>(Func<LockedRepository<TRepository>, TResult> func, bool autoCommit = true)
        {
            var uow = _uowProvider.GetUnitOfWork();
            using (var transaction = uow.Database.GetTransaction(IsolationLevel.RepeatableRead))
            {
                foreach (var lockId in _writeLockIds)
                    uow.Database.AcquireLockNodeReadLock(lockId);

                using (var repository = _repositoryFactory(uow))
                {
                    var ret = func(new LockedRepository<TRepository>(transaction, uow, repository));
                    if (autoCommit == false) return ret;
                    uow.Commit();
                    transaction.Complete();
                    return ret;
                }
            }
        }
    }

    internal class LockedRepository<TRepository>
        where TRepository : IDisposable, IRepository
    {
        public LockedRepository(Transaction transaction, IDatabaseUnitOfWork unitOfWork, TRepository repository)
        {
            Transaction = transaction;
            UnitOfWork = unitOfWork;
            Repository = repository;
        }

        public Transaction Transaction { get; private set; }
        public IDatabaseUnitOfWork UnitOfWork { get; private set; }
        public TRepository Repository { get; private set; }

        public void Commit()
        {
            UnitOfWork.Commit();
            Transaction.Complete();
        }
    }

    // this is how we'd implement LockingRepositoryService and it initially sounded like a great
    // idea, but afterwards I think what we have at the moment ie explicitely creating the lrepo
    // in the service, is easier to understand...
    //
    // also requires that LockedRepository<TRepository> is made public

    /*
    public abstract class LockingRepositoryService<TRepository> : RepositoryService
        where TRepository : IDisposable, IRepository
    {
        private readonly LockingRepository<TRepository> _lrepo;

        protected LockingRepositoryService(IDatabaseUnitOfWorkProvider provider, RepositoryFactory repositoryFactory,
            ILogger logger)
            : base(provider, repositoryFactory, logger)
        {
            // properties would need to return constants, because the
            // inheriting classes constructors have not run yet, only
            // their initializers

            // ReSharper disable DoNotCallOverridableMethodsInConstructor
            var readLockIds = LockingRepositoryReadLockIds;
            var writeLockIds = LockingRepositoryWriteLockIds;
            // ReSharper restore DoNotCallOverridableMethodsInConstructor

            _lrepo = new LockingRepository<TRepository>(provider, CreateRepository, readLockIds, writeLockIds);
        }

        protected abstract TRepository CreateRepository(IDatabaseUnitOfWork unitOfWork);
        protected abstract IEnumerable<int> LockingRepositoryReadLockIds { get; }
        protected abstract IEnumerable<int> LockingRepositoryWriteLockIds { get; } 

        protected void WithReadLocked(int[] lockIds, Action<LockedRepository<TRepository>> action, bool autoCommit = true)
        {
            _lrepo.WithReadLocked(action, autoCommit);
        }

        protected TResult WithReadLocked<TResult>(int[] lockIds, Func<LockedRepository<TRepository>, TResult> func, bool autoCommit = true)
        {
            return _lrepo.WithReadLocked(func, autoCommit);
        }

        protected void WithWriteLocked(int[] lockIds, Action<LockedRepository<TRepository>> action, bool autoCommit = true)
        {
            _lrepo.WithWriteLocked(action, autoCommit);
        }

        protected TResult WithWriteLocked<TResult>(int[] lockIds, Func<LockedRepository<TRepository>, TResult> func, bool autoCommit = true)
        {
            return _lrepo.WithWriteLocked(func, autoCommit);
        }
    }
    */
}
