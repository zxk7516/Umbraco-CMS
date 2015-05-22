using System;
using System.Collections.Generic;
using System.Data;
using System.Runtime.CompilerServices;

namespace Umbraco.Core.Persistence
{
    internal static class DatabaseNodeLockExtensions
    {
        private readonly static HashSet<int> DeniedLockNodeIds = new HashSet<int>();

        // denying access to a lock for the current app domain ensures that the app domain
        // cannot have access to the protected resources anymore, at all, ever. this is used
        // when the app domain shut downs and we want to make sure it cannot do some specific
        // operations.
        // eg. when we terminate the Xml file writer, we want to make sure that no other thread
        // can edit content, because that would never be written to the Xml file.
        // denying access to a lock should ALWAYS be done while holding an exclusive write-access
        // to that lock, thus ensuring that no other thread is currently using it
        public static void DenyCurrentAppDomainAccessToLockNode(this UmbracoDatabase database, int nodeId)
        {
            DeniedLockNodeIds.Add(nodeId);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void ValidateDatabase(UmbracoDatabase database)
        {
            if (database == null)
                throw new ArgumentNullException("database");
            if (database.CurrentTransactionIsolationLevel < IsolationLevel.RepeatableRead)
                throw new InvalidOperationException("A transaction with minimum RepeatableRead isolation level is required.");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void ValidateNodeLock(int nodeId)
        {
            if (DeniedLockNodeIds.Contains(nodeId))
                throw new InvalidOperationException("The current AppDomain cannot access LockNode ID:" + nodeId + " anymore.");
        }

        // updating a record within a repeatable-read transaction gets an exclusive lock on
        // that record which will be kept until the transaction is ended, effectively locking
        // out all other accesses to that record - thus obtaining an exclusive lock over the
        // protected resources.
        public static void AcquireLockNodeWriteLock(this UmbracoDatabase database, int nodeId)
        {
            ValidateDatabase(database);
            ValidateNodeLock(nodeId);

            database.Execute("UPDATE umbracoNode SET sortOrder = (CASE WHEN (sortOrder=1) THEN -1 ELSE 1 END) WHERE id=@id",
                new { @id = nodeId });
        }

        // reading a record within a repeatable-read transaction gets a shared lock on
        // that record which will be kept until the transaction is ended, effectively preventing
        // other write accesses to that record - thus obtaining a shared lock over the protected
        // resources.
        public static void AcquireLockNodeReadLock(this UmbracoDatabase database, int nodeId)
        {
            ValidateDatabase(database);
            ValidateNodeLock(nodeId);

            database.ExecuteScalar<int>("SELECT sortOrder FROM umbracoNode WHERE id=@id",
                new { @id = nodeId });
        }
    }
}
