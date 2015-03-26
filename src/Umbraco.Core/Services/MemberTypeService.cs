using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using Umbraco.Core.Auditing;
using Umbraco.Core.Events;
using Umbraco.Core.Logging;
using Umbraco.Core.Models;
using Umbraco.Core.Persistence;
using Umbraco.Core.Persistence.Querying;
using Umbraco.Core.Persistence.Repositories;
using Umbraco.Core.Persistence.UnitOfWork;

namespace Umbraco.Core.Services
{
    public class MemberTypeService : ContentTypeServiceBase, IMemberTypeService
    {
        private readonly IMemberService _memberService;

        private static readonly ReaderWriterLockSlim Locker = new ReaderWriterLockSlim();

        [Obsolete("Use the constructors that specify all dependencies instead")]
        public MemberTypeService(IMemberService memberService)
            : this(new PetaPocoUnitOfWorkProvider(), new RepositoryFactory(), memberService)
        {}

        [Obsolete("Use the constructors that specify all dependencies instead")]
        public MemberTypeService(RepositoryFactory repositoryFactory, IMemberService memberService)
            : this(new PetaPocoUnitOfWorkProvider(), repositoryFactory, memberService)
        { }

        [Obsolete("Use the constructors that specify all dependencies instead")]
        public MemberTypeService(IDatabaseUnitOfWorkProvider provider, RepositoryFactory repositoryFactory, IMemberService memberService)
            : this(provider, repositoryFactory, LoggerResolver.Current.Logger, memberService)
        {
        }

        public MemberTypeService(IDatabaseUnitOfWorkProvider provider, RepositoryFactory repositoryFactory, ILogger logger, IMemberService memberService)
            : base(provider, repositoryFactory, logger)
        {
            if (memberService == null) throw new ArgumentNullException("memberService");
            _memberService = memberService;
        }

        #region Lock Helper Methods

        // note: ultimately the two methods below end up locking ALL content types

        private T WithReadLockedMemberTypes<T>(Func<MemberTypeRepository, T> func, bool autoCommit = true)
        {
            var uow = UowProvider.GetUnitOfWork();
            using (var transaction = uow.Database.GetTransaction(IsolationLevel.RepeatableRead))
            using (var irepository = RepositoryFactory.CreateMemberTypeRepository(uow))
            {
                var repository = irepository as MemberTypeRepository;
                if (repository == null) throw new Exception("oops");
                repository.AcquireReadLock();
                var ret = func(repository);
                if (autoCommit == false) return ret;
                repository.UnitOfWork.Commit();
                transaction.Complete();
                return ret;
            }
        }

        private void WithReadLockedMemberTypes(Action<MemberTypeRepository> action, bool autoCommit = true)
        {
            var uow = UowProvider.GetUnitOfWork();
            using (var transaction = uow.Database.GetTransaction(IsolationLevel.RepeatableRead))
            using (var irepository = RepositoryFactory.CreateMemberTypeRepository(uow))
            {
                var repository = irepository as MemberTypeRepository;
                if (repository == null) throw new Exception("oops");
                repository.AcquireReadLock();
                action(repository);
                if (autoCommit == false) return;
                repository.UnitOfWork.Commit();
                transaction.Complete();
            }
        }

        private void WithWriteLockedMemberAndMemberTypes(Action<MemberTypeRepository> action, bool autoCommit = true)
        {
            var uow = UowProvider.GetUnitOfWork();
            using (var transaction = uow.Database.GetTransaction(IsolationLevel.RepeatableRead))
            using (var irepository = RepositoryFactory.CreateMemberTypeRepository(uow))
            using (var iMemberRepository = RepositoryFactory.CreateMemberRepository(uow))
            {
                var repository = irepository as MemberTypeRepository;
                if (repository == null) throw new Exception("oops");

                var memberRepository = iMemberRepository as MemberRepository;
                if (memberRepository == null) throw new Exception("oops");

                // respect order to avoid deadlocks
                memberRepository.AcquireWriteLock();
                repository.AcquireWriteLock();

                action(repository);
                if (autoCommit == false) return;
                repository.UnitOfWork.Commit();
                transaction.Complete();
            }
        }

        #endregion

        #region Member - Get, Has, Is

        public IEnumerable<IMemberType> GetAll(params int[] ids)
        {
            return WithReadLockedMemberTypes(repository => repository.GetAll(ids));
        }

        /// <summary>
        /// Gets an <see cref="IMemberType"/> object by its Id
        /// </summary>
        /// <param name="id">Id of the <see cref="IMemberType"/> to retrieve</param>
        /// <returns><see cref="IMemberType"/></returns>
        public IMemberType Get(int id)
        {
            return WithReadLockedMemberTypes(repository => repository.Get(id));
        }

        /// <summary>
        /// Gets an <see cref="IMemberType"/> object by its Alias
        /// </summary>
        /// <param name="alias">Alias of the <see cref="IMemberType"/> to retrieve</param>
        /// <returns><see cref="IMemberType"/></returns>
        public IMemberType Get(string alias)
        {
            var query = Query<IMemberType>.Builder.Where(x => x.Alias == alias);
            return WithReadLockedMemberTypes(repository => repository.GetByQuery(query).FirstOrDefault());
        }

        #endregion

        #region Member - Save, Delete

        public void Save(IMemberType memberType, int userId = 0)
        {
            if (Saving.IsRaisedEventCancelled(new SaveEventArgs<IMemberType>(memberType), this))
                return;

            WithWriteLockedMemberAndMemberTypes(repository =>
            {
                memberType.CreatorId = userId;
                repository.AddOrUpdate(memberType);
                repository.UnitOfWork.Commit(); // commits the UOW but NOT the transaction

                // figure out content types impacted by the changes through composition
                var changedTypes = ComposeContentTypeChangesForTransactionEvent(memberType).ToArray();
                OnTransactionRefreshedEntity(repository.UnitOfWork, changedTypes);
            });

            //FIXME REFACTOR THIS
            //NotifyMemberServiceOfMemberTypeChanges(memberType);
            Saved.RaiseEvent(new SaveEventArgs<IMemberType>(memberType, false), this);
        }

        public void Save(IEnumerable<IMemberType> memberTypes, int userId = 0)
        {
            var memberTypesA = memberTypes.ToArray();

            if (Saving.IsRaisedEventCancelled(new SaveEventArgs<IMemberType>(memberTypesA), this))
                return;

            WithWriteLockedMemberAndMemberTypes(repository =>
            {
                foreach (var memberType in memberTypesA)
                {
                    memberType.CreatorId = userId;
                    repository.AddOrUpdate(memberType);
                }

                repository.UnitOfWork.Commit(); // commits the UOW but NOT the transaction

                // figure out content types impacted by the changes through composition
                var changedTypes = ComposeContentTypeChangesForTransactionEvent(memberTypesA).ToArray();
                OnTransactionRefreshedEntity(repository.UnitOfWork, changedTypes);
            });

            //FIXME REFACTOR THIS
            //NotifyMemberServiceOfMemberTypeChanges(asArray.Cast<IContentTypeBase>().ToArray());

            Saved.RaiseEvent(new SaveEventArgs<IMemberType>(memberTypesA, false), this);
        }

        // FIXME DELETE NOT RAISING TRANSACTION EVENTS OOPS

        public void Delete(IMemberType memberType, int userId = 0)
        {
            if (Deleting.IsRaisedEventCancelled(new DeleteEventArgs<IMemberType>(memberType), this))
                return;

            WithWriteLockedMemberAndMemberTypes(repository =>
            {
                _memberService.DeleteMembersOfType(memberType.Id);

                repository.Delete(memberType);
            });

            // FIXME EVENTS?!

            Deleted.RaiseEvent(new DeleteEventArgs<IMemberType>(memberType, false), this);
        }

        public void Delete(IEnumerable<IMemberType> memberTypes, int userId = 0)
        {
            var asArray = memberTypes.ToArray();

            if (Deleting.IsRaisedEventCancelled(new DeleteEventArgs<IMemberType>(asArray), this))
                return;

            WithWriteLockedMemberAndMemberTypes(repository =>
            {
                foreach (var contentType in asArray)
                    _memberService.DeleteMembersOfType(contentType.Id);

                foreach (var memberType in asArray)
                    repository.Delete(memberType);
            });

            // FIXME EVENTS?!

            Deleted.RaiseEvent(new DeleteEventArgs<IMemberType>(asArray, false), this);
        }

        #endregion

        #region Event Handlers

        /// <summary>
        /// Occurs before Save
        /// </summary>
        public static event TypedEventHandler<IMemberTypeService, SaveEventArgs<IMemberType>> Saving;

        /// <summary>
        /// Occurs after Save
        /// </summary>
        public static event TypedEventHandler<IMemberTypeService, SaveEventArgs<IMemberType>> Saved;

        /// <summary>
        /// Occurs before Delete
        /// </summary>
        public static event TypedEventHandler<IMemberTypeService, DeleteEventArgs<IMemberType>> Deleting;

        /// <summary>
        /// Occurs after Delete
        /// </summary>
        public static event TypedEventHandler<IMemberTypeService, DeleteEventArgs<IMemberType>> Deleted;

        #endregion
    }
}