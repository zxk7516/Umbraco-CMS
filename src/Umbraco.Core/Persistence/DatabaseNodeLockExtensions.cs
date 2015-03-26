using System;
using System.Data;

namespace Umbraco.Core.Persistence
{
    internal static class DatabaseNodeLockExtensions
    {
        public static void AcquireLockNodeWriteLock(this UmbracoDatabase database, int nodeId)
        {
            if (database.CurrentTransactionIsolationLevel < IsolationLevel.RepeatableRead)
                throw new InvalidOperationException("A transaction with minimum RepeatableRead isolation level is required.");

            database.Execute("UPDATE umbracoNode SET sortOrder = (CASE WHEN (sortOrder=1) THEN -1 ELSE 1 END) WHERE id=@id",
                new { @id = nodeId });
        }

        public static void AcquireLockNodeReadLock(this UmbracoDatabase database, int nodeId)
        {
            if (database.CurrentTransactionIsolationLevel < IsolationLevel.RepeatableRead)
                throw new InvalidOperationException("A transaction with minimum RepeatableRead isolation level is required.");

            database.ExecuteScalar<int>("SELECT sortOrder FROM umbracoNode WHERE id=@id",
                new { @id = nodeId });
        }
    }
}
