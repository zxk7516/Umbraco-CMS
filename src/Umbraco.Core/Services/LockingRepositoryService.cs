using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using Umbraco.Core.Logging;
using Umbraco.Core.Persistence;
using Umbraco.Core.Persistence.UnitOfWork;

namespace Umbraco.Core.Services
{
    // FIXME the issue here is that if the service is public, the repository needs to be public too?!
    // so either we fix what's public and what's not (eg ContentService) or we cannot do this with a base class
    // and have to do it with some sort of helper... sort-of
    // private static readonly LockingRepository<ContentRepository> _lockingRepo = new LockingRepository<ContentRepository>(...)

    internal class LockingRepository<TRepository>
        where TRepository : IDisposable
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

        public void WithReadLocked(Action<LockedRepository> action, bool autoCommit = true)
        {
            var uow = _uowProvider.GetUnitOfWork();
            using (var transaction = uow.Database.GetTransaction(IsolationLevel.RepeatableRead))
            {
                foreach (var lockId in _readLockIds)
                    uow.Database.AcquireLockNodeReadLock(lockId);

                using (var repository = _repositoryFactory(uow))
                {
                    action(new LockedRepository(transaction, uow, repository));
                    if (autoCommit == false) return;
                    uow.Commit();
                    transaction.Complete();
                }
            }
        }

        public TResult WithReadLocked<TResult>(Func<LockedRepository, TResult> func, bool autoCommit = true)
        {
            var uow = _uowProvider.GetUnitOfWork();
            using (var transaction = uow.Database.GetTransaction(IsolationLevel.RepeatableRead))
            {
                foreach (var lockId in _readLockIds)
                    uow.Database.AcquireLockNodeReadLock(lockId);

                using (var repository = _repositoryFactory(uow))
                {
                    var ret = func(new LockedRepository(transaction, uow, repository));
                    if (autoCommit == false) return ret;
                    uow.Commit();
                    transaction.Complete();
                    return ret;
                }
            }
        }

        public void WithWriteLocked(Action<LockedRepository> action, bool autoCommit = true)
        {
            var uow = _uowProvider.GetUnitOfWork();
            using (var transaction = uow.Database.GetTransaction(IsolationLevel.RepeatableRead))
            {
                foreach (var lockId in _writeLockIds)
                    uow.Database.AcquireLockNodeWriteLock(lockId);

                using (var repository = _repositoryFactory(uow))
                {
                    action(new LockedRepository(transaction, uow, repository));
                    if (autoCommit == false) return;
                    uow.Commit();
                    transaction.Complete();
                }
            }
        }

        public TResult WithWriteLocked<TResult>(Func<LockedRepository, TResult> func, bool autoCommit = true)
        {
            var uow = _uowProvider.GetUnitOfWork();
            using (var transaction = uow.Database.GetTransaction(IsolationLevel.RepeatableRead))
            {
                foreach (var lockId in _writeLockIds)
                    uow.Database.AcquireLockNodeReadLock(lockId);

                using (var repository = _repositoryFactory(uow))
                {
                    var ret = func(new LockedRepository(transaction, uow, repository));
                    if (autoCommit == false) return ret;
                    uow.Commit();
                    transaction.Complete();
                    return ret;
                }
            }
        }

        public class LockedRepository
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
    }

    public abstract class LockingRepositoryService<TRepository> : RepositoryService
        where TRepository : IDisposable
    {
        protected LockingRepositoryService(IDatabaseUnitOfWorkProvider provider, RepositoryFactory repositoryFactory, ILogger logger)
            : base(provider, repositoryFactory, logger)
        { }

        protected abstract TRepository CreateRepository(IDatabaseUnitOfWork unitOfWork);

        protected void WithReadLocked(int[] lockIds, Action<LockedRepository> action, bool autoCommit = true)
        {
            var uow = UowProvider.GetUnitOfWork();
            using (var transaction = uow.Database.GetTransaction(IsolationLevel.RepeatableRead))
            {
                foreach (var lockId in lockIds)
                    uow.Database.AcquireLockNodeReadLock(lockId);

                using (var repository = CreateRepository(uow))
                {
                    action(new LockedRepository(transaction, uow, repository));
                    if (autoCommit == false) return;
                    uow.Commit();
                    transaction.Complete();
                }
            }
        }

        protected TResult WithReadLocked<TResult>(int[] lockIds, Func<LockedRepository, TResult> func, bool autoCommit = true)
        {
            var uow = UowProvider.GetUnitOfWork();
            using (var transaction = uow.Database.GetTransaction(IsolationLevel.RepeatableRead))
            {
                foreach (var lockId in lockIds)
                    uow.Database.AcquireLockNodeReadLock(lockId);

                using (var repository = CreateRepository(uow))
                {
                    var ret = func(new LockedRepository(transaction, uow, repository));
                    if (autoCommit == false) return ret;
                    uow.Commit();
                    transaction.Complete();
                    return ret;
                }
            }
        }

        protected void WithWriteLocked(int[] lockIds, Action<LockedRepository> action, bool autoCommit = true)
        {
            var uow = UowProvider.GetUnitOfWork();
            using (var transaction = uow.Database.GetTransaction(IsolationLevel.RepeatableRead))
            {
                foreach (var lockId in lockIds)
                    uow.Database.AcquireLockNodeReadLock(lockId);

                using (var repository = CreateRepository(uow))
                {
                    action(new LockedRepository(transaction, uow, repository));
                    if (autoCommit == false) return;
                    uow.Commit();
                    transaction.Complete();
                }
            }
        }

        protected TResult WithWriteLocked<TResult>(int[] lockIds, Func<LockedRepository, TResult> func, bool autoCommit = true)
        {
            var uow = UowProvider.GetUnitOfWork();
            using (var transaction = uow.Database.GetTransaction(IsolationLevel.RepeatableRead))
            {
                foreach (var lockId in lockIds)
                    uow.Database.AcquireLockNodeReadLock(lockId);

                using (var repository = CreateRepository(uow))
                {
                    var ret = func(new LockedRepository(transaction, uow, repository));
                    if (autoCommit == false) return ret;
                    uow.Commit();
                    transaction.Complete();
                    return ret;
                }
            }
        }

        protected class LockedRepository
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
    }
}
