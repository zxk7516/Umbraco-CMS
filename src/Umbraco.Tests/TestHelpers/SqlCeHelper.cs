using System;
using System.Data.SqlServerCe;
using System.IO;
using Umbraco.Core;
using Umbraco.Core.Logging;
using Umbraco.Core.Persistence.SqlSyntax;

namespace Umbraco.Tests.TestHelpers
{
    public class SqlCeDbTestHelper : IDbTestHelper
    {
        private readonly string _dbFilePath;
        private readonly ProfilingLogger _profilingLogger;
        public static string DefaultConnectionString = @"Datasource=|DataDirectory|UmbracoPetaPocoTests.sdf;Flush Interval=1;";
        public static string ProviderName = "System.Data.SqlServerCe.4.0";
        public static string FileName = "UmbracoPetaPocoTests.sdf";

        private static object _locker = new object();
        private static byte[] _dbBytes;
        private ISqlSyntaxProvider _syntaxProvider;

        public SqlCeDbTestHelper(string dbFilePath, ProfilingLogger profilingLogger)
        {
            _dbFilePath = dbFilePath;
            _profilingLogger = profilingLogger;
        }

        public void CreateNewDb(ApplicationContext applicationContext, DatabaseBehavior testBehavior)
        {
            DeleteDatabase();
            
            using (_profilingLogger.TraceDuration<SqlCeDbTestHelper>("Create database file"))
            {
                if (testBehavior != DatabaseBehavior.EmptyDbFilePerTest && _dbBytes != null)
                {
                    File.WriteAllBytes(_dbFilePath, _dbBytes);
                }
                else
                {
                    using (var engine = new SqlCeEngine(applicationContext.DatabaseContext.ConnectionString))
                    {
                        engine.CreateDatabase();
                    }
                }
            }
        }

        public void DeleteDatabase()
        {
            using (_profilingLogger.TraceDuration<SqlCeDbTestHelper>("Remove database file if it exists"))
            {
                try
                {
                    if (File.Exists(_dbFilePath))
                    {
                        File.Delete(_dbFilePath);
                    }
                }
                catch (Exception ex)
                {
                    LogHelper.Error<SqlCeDbTestHelper>("Could not remove the old database file", ex);
                    throw ex;
                }
            }            
        }

        public void ClearDatabase(ApplicationContext applicationContext)
        {
            //clear/delete are the same for sqlce
            DeleteDatabase();
        }

        public void ConfigureForFirstRun(ApplicationContext applicationContext)
        {
            lock (_locker)
            {
                if (_dbBytes == null)
                {
                    //we're gonna read this baby in as a byte array
                    //so we don't have to re-initialize the db for each test which is very slow
                    _dbBytes = File.ReadAllBytes(_dbFilePath);
                }
            }
        }

        public string DbProviderName
        {
            get { return ProviderName; }
        }

        public string DbConnectionString
        {
            get { return DefaultConnectionString; }
        }

        public ISqlSyntaxProvider SqlSyntaxProvider
        {
            get { return _syntaxProvider ?? (_syntaxProvider = new SqlCeSyntaxProvider()); }
        }
    }
}