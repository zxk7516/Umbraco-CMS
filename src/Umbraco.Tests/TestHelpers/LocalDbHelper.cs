using System;
using System.Data.SqlClient;
using System.IO;
using Umbraco.Core;
using Umbraco.Core.Logging;
using Umbraco.Core.Persistence;
using Umbraco.Core.Persistence.SqlSyntax;

namespace Umbraco.Tests.TestHelpers
{
    /// <summary>
    /// A utility for working with LocalDb and unit tests
    /// </summary>
    /// <remarks>
    /// We perform an initialization trick (just like we do with SQLCE) and on the first test run we'll take a binary snapshot of the database
    /// file and then use that for each subsequent test. 
    /// Doing this involves detaching the database which is ok but the tricky thing here is that when you connect to the database file with AttachDbFilename
    /// the actual database file name changes to the full path instead of the original name! 
    /// </remarks>
    public class LocalDbHelper : IDbTestHelper
    {
        private readonly string _dbFilePath;
        private readonly ProfilingLogger _profilingLogger;
        public const string DefaultConnectionString = @"Data Source=(LocalDB)\MSSQLLocalDB;AttachDbFilename=|DataDirectory|\UmbracoPetaPocoTests.mdf;Integrated Security=True";
        private const string MasterCatalogConnectionString = @"server=(localdb)\MSSQLLocalDB;Initial Catalog=master;";
        public const string ProviderName = "System.Data.SqlClient";
        public const string FileName = "UmbracoPetaPocoTests.mdf";
        private const string DbName = "UmbracoPetaPocoTests";

        private static readonly object Locker = new object();
        private static byte[] _dbBytes;

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
                if (path.ContainsAny(Path.GetInvalidPathChars()))
                    continue;
                try
                {
                    var fullPath = Path.Combine(path, fileName);
                    if (File.Exists(fullPath))
                        return fullPath;
                }
                catch (ArgumentException)
                {
                    continue;
                }
            }
            return null;
        }

        /// <summary>
        /// Terminates all db connections
        /// </summary>
        /// <param name="useFilePath">
        /// This will be true if the database has been connected to via the connection string using AttachDbFilename which will be in most
        /// cases apart from the first run
        /// </param>
        private void ForciblyCloseAllConnections(bool useFilePath)
        {
            var dbName = useFilePath ? _dbFilePath : DbName;

            using (var connection = new SqlConnection(MasterCatalogConnectionString))
            {
                connection.Open();

                //taken from https://stackoverflow.com/a/7469167/694494 which seems to be the only thing that i can get to work consistently
                //to kill anything that is connected to this db.
                var sql = string.Format(@"USE [master];
DECLARE @DatabaseName nvarchar(1000);
SET @DatabaseName = N'{0}';
DECLARE @SQL varchar(max);
SELECT @SQL = COALESCE(@SQL,'') + 'Kill ' + Convert(varchar, SPId) + ';'
FROM MASTER..SysProcesses
WHERE DBId = DB_ID(@DatabaseName) AND SPId <> @@SPId;
EXEC(@SQL);", dbName);

                using (var command = new SqlCommand(sql, connection))
                {
                    command.ExecuteNonQuery();
                }
            }
        }

        /// <summary>
        /// This will detach the database so we can capture the database file
        /// </summary>
        /// <remarks>
        /// This is ONLY used when we want to capture the file bytes on first run
        /// </remarks>
        private void DetachDatabase()
        {
            using (var connection = new SqlConnection(MasterCatalogConnectionString))
            {
                connection.Open();

                var sql = string.Format(@"exec sp_detach_db '{0}';", DbName);
                
                using (var command = new SqlCommand(sql, connection))
                {
                    command.ExecuteNonQuery();
                }
            }
        }

        /// <summary>
        /// Drops the database
        /// </summary>
        /// <param name="useFilePath">
        /// This will be true if the database has been connected to via the connection string using AttachDbFilename which will be in most
        /// cases apart from the first run
        /// </param>
        private void DropDatabase(bool useFilePath)
        {
            var dbName = useFilePath ? _dbFilePath : DbName;

            using (var connection = new SqlConnection(MasterCatalogConnectionString))
            {
                connection.Open();
                var sql = string.Format(@"IF EXISTS(select * from sys.databases where name='{0}') DROP DATABASE [{0}];", dbName);
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
                if (testBehavior != DatabaseBehavior.EmptyDbFilePerTest && _dbBytes != null)
                {
                    File.WriteAllBytes(_dbFilePath, _dbBytes);
                }
                else
                {
                    var baseDir = Path.GetDirectoryName(_dbFilePath);

                    using (var connection = new SqlConnection(MasterCatalogConnectionString))
                    {
                        connection.Open();

                        var sql = string.Format(@"
                                    IF NOT EXISTS(select * from sys.databases where name='{0}')
                                        CREATE DATABASE [{0}]
                                        ON PRIMARY (NAME='{0}', FILENAME = '{1}')
                                        LOG ON (NAME='{0}_log', FILENAME = '{2}\{0}.ldf')",
                            DbName,
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
        }

        private void DropTables(ApplicationContext applicationContext)
        {
            using (_profilingLogger.TraceDuration<LocalDbHelper>("Dropping database tables if they exist"))
            {
                if (DbExists(useFilePath:true))
                {
                    var schemaHelper = new DatabaseSchemaHelper(
                        applicationContext.DatabaseContext.Database,
                        _profilingLogger.Logger,
                        applicationContext.DatabaseContext.SqlSyntax);
                    schemaHelper.UninstallDatabaseSchema();
                }
            }
        }

        /// <summary>
        /// This is called at the end of a unit test session
        /// </summary>
        public void DeleteDatabase()
        {
            using (_profilingLogger.TraceDuration<LocalDbHelper>("Deleting database"))
            {
                ForciblyCloseAllConnections(useFilePath:true);
                DropDatabase(useFilePath: true);
                try
                {
                    if (File.Exists(_dbFilePath))
                    {
                        File.Delete(_dbFilePath);
                    }
                    var baseDir = Path.GetDirectoryName(_dbFilePath);
                    var logFilePath = Path.Combine(baseDir, string.Format("{0}.ldf", DbName));
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

        /// <summary>
        /// This is called at the end of a unit test
        /// </summary>
        /// <param name="applicationContext"></param>
        public void ClearDatabase(ApplicationContext applicationContext)
        {
            DeleteDatabase();
            //DropTables(applicationContext);
        }

        public void ConfigureForFirstRun(ApplicationContext applicationContext)
        {
            lock (Locker)
            {
                if (_dbBytes == null)
                {
                    //on first run, the database will have just been created which means we are not connecting to it via the
                    //connection string with AttachDbFilename which means the database name will be the normal string and not the full path
                    ForciblyCloseAllConnections(useFilePath:false);

                    //once we detach, the entry for the newly created UmbracoPetaPocoTests database in the sys.databases table will be gone.
                    //when we reconnect using the connection string with AttachDbFilename this entry will be re-created but the database name will
                    //be the full path.
                    DetachDatabase();
                    
                    
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
            get { return _syntaxProvider ?? (_syntaxProvider = new SqlServerSyntaxProvider()); }
        }

        /// <summary>
        /// Checks with the master server if this db is registered
        /// </summary>
        /// <param name="useFilePath">
        /// This will be true if the database has been connected to via the connection string using AttachDbFilename which will be in most
        /// cases apart from the first run
        /// </param>
        /// <returns></returns>
        private bool DbExists(bool useFilePath)
        {
            var dbName = useFilePath ? _dbFilePath : DbName;

            var exists = false;
            using (var connection = new SqlConnection(MasterCatalogConnectionString))
            {
                connection.Open();
                var sql = string.Format(@"SELECT COUNT(*) from sys.databases where name='{0}'", dbName);
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