using System;
using System.Data.SqlClient;
using System.IO;
using Umbraco.Core;
using Umbraco.Core.Logging;
using Umbraco.Core.Persistence;
using Umbraco.Core.Persistence.SqlSyntax;

namespace Umbraco.Tests.TestHelpers
{
    public class LocalDbHelper : IDbTestHelper
    {
        private readonly string _dbFilePath;
        private readonly ProfilingLogger _profilingLogger;
        //public static string DefaultConnectionString = @"Data Source=(LocalDB)\MSSQLLocalDB;AttachDbFilename=|DataDirectory|\UmbracoPetaPocoTests.mdf;Integrated Security=True;MultipleActiveResultSets=True";
        public static string DefaultConnectionString = @"Data Source=(LocalDB)\MSSQLLocalDB;AttachDbFilename=|DataDirectory|\UmbracoPetaPocoTests.mdf;Integrated Security=True";
        public static string ProviderName = "System.Data.SqlClient";
        public static string FileName = "UmbracoPetaPocoTests.mdf";

        public LocalDbHelper(string dbFilePath, ProfilingLogger profilingLogger)
        {
            _dbFilePath = dbFilePath;
            _profilingLogger = profilingLogger;
        }

        private static bool? _isLocalDbInstalled;
        private ISqlSyntaxProvider _syntaxProvider;

        public static bool IsLocalDbInstalled
        {
            get { return (_isLocalDbInstalled ?? (_isLocalDbInstalled = ExistsOnPath("SqlLocalDB.exe"))).Value; }
        }

        private static bool ExistsOnPath(string fileName)
        {
            return GetFullPath(fileName) != null;
        }

        private static string GetFullPath(string fileName)
        {
            if (File.Exists(fileName))
                return Path.GetFullPath(fileName);

            var values = Environment.GetEnvironmentVariable("PATH");
            foreach (var path in values.Split(';'))
            {
                var fullPath = Path.Combine(path, fileName);
                if (File.Exists(fullPath))
                    return fullPath;
            }
            return null;
        }

        private void ForciblyCloseAllConnections()
        {
            using (var connection = new SqlConnection(@"server=(localdb)\MSSQLLocalDB"))
            {
                connection.Open();
                //taken from https://stackoverflow.com/a/7469167/694494 which seems to be the only thing that i can get to work consistently
                var sql = @"USE [master];
DECLARE @DatabaseName nvarchar(50);
SET @DatabaseName = N'UmbracoPetaPocoTests';
DECLARE @SQL varchar(max);
SELECT @SQL = COALESCE(@SQL,'') + 'Kill ' + Convert(varchar, SPId) + ';'
FROM MASTER..SysProcesses
WHERE DBId = DB_ID(@DatabaseName) AND SPId <> @@SPId;
EXEC(@SQL)";
                using (var command = new SqlCommand(sql, connection))
                {
                    command.ExecuteNonQuery();
                }
            }
        }

        private void DropDatabase()
        {
            ForciblyCloseAllConnections();
            using (var connection = new SqlConnection(@"server=(localdb)\MSSQLLocalDB"))
            {
                connection.Open();
                var sql = @"USE [master];
                            IF EXISTS(select * from sys.databases where name='UmbracoPetaPocoTests') DROP DATABASE [UmbracoPetaPocoTests];";
                using (var command = new SqlCommand(sql, connection))
                {
                    command.ExecuteNonQuery();
                }
            }
        }

        public void CreateNewDb(ApplicationContext applicationContext, DatabaseBehavior testBehavior)
        {
            DropTables(applicationContext);
            
            using (_profilingLogger.TraceDuration<LocalDbHelper>("Create new localdb database"))
            {
                var baseDir = Path.GetDirectoryName(_dbFilePath);
                
                using (var connection = new SqlConnection(@"server=(localdb)\MSSQLLocalDB"))
                {
                    connection.Open();
                    var sql = string.Format(@"
                                    IF NOT EXISTS(select * from sys.databases where name='UmbracoPetaPocoTests')
                                        CREATE DATABASE [UmbracoPetaPocoTests]
                                        ON PRIMARY (NAME=Test_data, FILENAME = '{0}')
                                        LOG ON (NAME=Test_log, FILENAME = '{1}\UmbracoPetaPocoTests.ldf')",
                        _dbFilePath,
                        baseDir
                    );
                    using (var command = new SqlCommand(sql, connection))
                    {
                        command.ExecuteNonQuery();
                    }
                }
            }
        }

        private void DropTables(ApplicationContext applicationContext)
        {
            using (_profilingLogger.TraceDuration<LocalDbHelper>("Dropping database tables if they exist"))
            {
                if (DbExists())
                {
                    //This is supposed to work but ....

                    //var connection = new SqlConnection(applicationContext.DatabaseContext.ConnectionString);
                    //using (connection)
                    //{
                    //    connection.Open();
                    //    var sql = @"
                    //                        Use UmbracoPetaPocoTests;
                    //                        EXEC sp_msforeachtable ""ALTER TABLE ? NOCHECK CONSTRAINT all"";
                    //                        EXEC sp_MSforeachtable @command1 = ""DROP TABLE ?"";";
                    //    using (var command = new SqlCommand(sql, connection))
                    //    {
                    //        command.ExecuteNonQuery();
                    //    }
                    //}

                    var schemaHelper = new DatabaseSchemaHelper(
                        applicationContext.DatabaseContext.Database,
                        _profilingLogger.Logger,
                        applicationContext.DatabaseContext.SqlSyntax);
                    schemaHelper.UninstallDatabaseSchema();
                }
            }
        }

        public void DeleteDatabase()
        {
            using (_profilingLogger.TraceDuration<LocalDbHelper>("Deleting database"))
            {
                //ForciblyCloseAllConnections();
                DropDatabase();
                try
                {
                    if (File.Exists(_dbFilePath))
                    {
                        File.Delete(_dbFilePath);
                    }
                    var baseDir = Path.GetDirectoryName(_dbFilePath);
                    var logFilePath = Path.Combine(baseDir, "UmbracoPetaPocoTests.ldf");
                    if (File.Exists(logFilePath))
                    {
                        File.Delete(logFilePath);
                    }
                }
                catch (Exception ex)
                {
                    LogHelper.Error<LocalDbHelper>("Could not remove the old database file", ex);
                    throw;
                }
            }
        }

        public void ClearDatabase(ApplicationContext applicationContext)
        {
            DropTables(applicationContext);
        }

        public void ConfigureForFirstRun(ApplicationContext applicationContext)
        {            
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
            get { return _syntaxProvider ?? (_syntaxProvider = new SqlServerSyntaxProvider()); }
        }

        /// <summary>
        /// Checks with the master server if this db is registered
        /// </summary>
        /// <returns></returns>
        private bool DbExists()
        {
            var exists = false;
            using (var connection = new SqlConnection(@"server=(localdb)\MSSQLLocalDB"))
            {
                connection.Open();
                var sql = @"SELECT COUNT(*) from sys.databases where name='UmbracoPetaPocoTests'";
                using (var command = new SqlCommand(sql, connection))
                {
                    var result = (int)command.ExecuteScalar();
                    exists = result > 0;
                }
            }
            return exists;
        }        
    }
}