using System;
using System.IO;
using SQLCE4Umbraco;
using Umbraco.Core;
using Umbraco.Core.Logging;
using Umbraco.Core.Persistence.SqlSyntax;

namespace Umbraco.Tests.TestHelpers
{
    public class DatabaseTestHelper : DisposableObject
    {
        private readonly string _baseDir;
        private readonly IDbTestHelper _dbTestHelper;

        public DatabaseTestHelper(string dbPath, ProfilingLogger profilingLogger)
        {
            _baseDir = dbPath;
            switch (DatabaseType)
            {
                case SupportedTestDatabase.SqlCe:
                    _dbTestHelper = new SqlCeDbTestHelper(GetDbPath(), profilingLogger);
                    break;
                case SupportedTestDatabase.LocalDb:
                    _dbTestHelper = new LocalDbHelper(GetDbPath(), profilingLogger);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public SupportedTestDatabase DatabaseType
        {
            get { return LocalDbHelper.IsLocalDbInstalled ? SupportedTestDatabase.LocalDb : SupportedTestDatabase.SqlCe; }
        }

        public bool DbExists
        {
            get { return File.Exists(GetDbPath()); }           
        }        

        public string GetDbPath()
        {
            switch (DatabaseType)
            {
                case SupportedTestDatabase.SqlCe:
                    return Path.Combine(_baseDir, SqlCeDbTestHelper.FileName);
                case SupportedTestDatabase.LocalDb:
                    return Path.Combine(_baseDir, LocalDbHelper.FileName);
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public string GetDbProviderName()
        {
            return _dbTestHelper.DbProviderName;
        }

        public string GetDbConnectionString()
        {
            return _dbTestHelper.DbConnectionString;
        }

        public ISqlSyntaxProvider GetSyntaxProvider()
        {
            return _dbTestHelper.SqlSyntaxProvider;
        }

        /// <summary>
        /// Called after the database is created and the schema is populated and can be used for performance improvements for db initialization 
        /// in tests after the first run.
        /// </summary>
        /// <param name="applicationContext"></param>
        public void ConfigureForFirstRun(ApplicationContext applicationContext)
        {
            CloseDbConnections(applicationContext);
            _dbTestHelper.ConfigureForFirstRun(applicationContext);            
        }

        public void CreateNewDb(ApplicationContext applicationContext, DatabaseBehavior testBehavior)
        {
            CloseDbConnections(applicationContext);
            _dbTestHelper.CreateNewDb(applicationContext, testBehavior);
        }

        public void TestTearDown(ApplicationContext applicationContext, DatabaseBehavior testBehavior)
        {
            CloseDbConnections(applicationContext);            
            if (testBehavior == DatabaseBehavior.NewDbFileAndSchemaPerTest)
            {
                _dbTestHelper.ClearDatabase(applicationContext);
            }
        }
        
        /// <summary>
        /// Called when all tests are done
        /// </summary>
        protected override void DisposeResources()
        {
            _dbTestHelper.DeleteDatabase();
        }

        private void CloseDbConnections(ApplicationContext applicationContext)
        {
            // just to be sure, although it's also done in TearDown
            if (applicationContext != null
                && applicationContext.DatabaseContext != null
                && applicationContext.DatabaseContext.ScopeProvider != null)
            {
                applicationContext.DatabaseContext.ScopeProvider.Reset();
            }

            SqlCeContextGuardian.CloseBackgroundConnection();
        }


        
    }
}