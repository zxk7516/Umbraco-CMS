using System;
using Umbraco.Core.Persistence;
using Umbraco.Core.Persistence.SqlSyntax;

namespace Umbraco.Tests.TestHelpers
{
    public abstract class TestDatabase
    {
        private static bool? _localDbIsAvailable;

        public static TestDatabase Get(string filesPath, SupportedTestDatabase database = SupportedTestDatabase.Unknown)
        {
            LocalDb localDb;
            switch (database)
            {
                case SupportedTestDatabase.SqlCe:
                    return new SqlCeTestDatabase(filesPath);
                case SupportedTestDatabase.LocalDb:
                    localDb = new LocalDb();
                    if (localDb.IsAvailable == false)
                        throw new ArgumentException("LocalDB is not available.", "database");
                    return new LocalDbTestDatabase(localDb, filesPath);
                case SupportedTestDatabase.Unknown:
                    localDb = new LocalDb();
                    return localDb.IsAvailable == false
                        ? (TestDatabase) new SqlCeTestDatabase(filesPath)
                        : new LocalDbTestDatabase(localDb, filesPath);
                default:
                    throw new ArgumentOutOfRangeException("database");
            }
        }

        public static bool LocalDbIsAvailable
        {
            get
            {
                return _localDbIsAvailable ?? (_localDbIsAvailable = new LocalDb().IsAvailable).Value;
            }
        }

        public abstract string ProviderName { get; }

        public abstract string GetConnectionString(bool withSchema);

        public abstract ISqlSyntaxProvider SqlSyntaxProvider { get; }

        public abstract bool HasEmpty { get; }

        public abstract bool HasSchema { get; }

        public abstract void Create();

        public abstract void CaptureEmpty();

        public abstract void CaptureSchema();

        public abstract void AttachEmpty();

        public abstract void AttachSchema();

        public abstract void Drop();

        public abstract void Clear();
    }
}