using System;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Reflection;
using System.Threading;
using Umbraco.Core;
using Umbraco.Core.Persistence;
using Umbraco.Core.Persistence.SqlSyntax;
using Umbraco.Core.Scoping;

namespace Umbraco.Tests.TestHelpers
{
    public class LocalDbTestDatabase : TestDatabase
    {
        private const string InstanceName = "UmbracoTests";
        private const string DatabaseName = "UmbracoTests";

        private readonly LocalDb _localDb;
        private readonly LocalDb.Instance _instance;
        private readonly string _filesPath;
        private readonly string _databaseFullName;
        private ISqlSyntaxProvider _syntaxProvider;
        private bool _hasDb;
        private UmbracoDatabase.CommandInfo[] _dbCommands;
        private Thread _thread;
        private int _currentEmpty;
        private int _currentSchema;
        private string _currentCstr;

        public LocalDbTestDatabase(LocalDb localDb, string filesPath)
        {
            _localDb = localDb;
            _filesPath = filesPath;
            _databaseFullName = Path.Combine(_filesPath, DatabaseName + ".mdf").ToUpper();

            _instance = _localDb.GetInstance(InstanceName);
            if (_instance != null) return;

            if (_localDb.CreateInstance(InstanceName) == false)
                throw new Exception("Failed to create a LocalDb instance.");
            _instance = _localDb.GetInstance(InstanceName);
        }

        public override string ProviderName
        {
            get { return "System.Data.SqlClient"; }
        }

        public override string GetConnectionString(bool withSchema)
        {
            return _currentCstr ?? GetConnectionString(false, 0);
        }

        private string GetConnectionString(bool withSchema, int current)
        {
            var name = DatabaseName + (withSchema ? "Schema" : "Empty") + current;
            return _instance.GetAttachedConnectionString(name, _filesPath);
        }

        public override ISqlSyntaxProvider SqlSyntaxProvider
        {
            get { return _syntaxProvider ?? (_syntaxProvider = new SqlServerSyntaxProvider()); }
        }

        public override bool HasEmpty
        {
            get { return _hasDb; }
        }

        public override bool HasSchema
        {
            get { return _hasDb; }
        }

        public override void Create()
        {
            var tempName = Guid.NewGuid().ToString("N");
            _instance.CreateDatabase(tempName, _filesPath);
            _instance.DetachDatabase(tempName);

            _localDb.CopyDatabaseFiles(tempName, _filesPath, targetDatabaseName: DatabaseName + "Empty0", overwrite: true);
            _localDb.CopyDatabaseFiles(tempName, _filesPath, targetDatabaseName: DatabaseName + "Empty1", overwrite: true);
            _localDb.CopyDatabaseFiles(tempName, _filesPath, targetDatabaseName: DatabaseName + "Schema0", overwrite: true);
            _localDb.CopyDatabaseFiles(tempName, _filesPath, targetDatabaseName: DatabaseName + "Schema1", delete: true, overwrite: true);

            _currentSchema = _currentEmpty = 0;
            _hasDb = true;

            _thread = RebuildSchema(GetConnectionString(true, 1));
            _thread.Start();
        }

        public override void CaptureEmpty()
        {
            // we don't
        }

        public override void CaptureSchema()
        {
            // we don't
        }

        public override void AttachEmpty()
        {
            if (_hasDb == false)
                throw new InvalidOperationException();

            _thread.Join();

            var other = _currentEmpty;
            _currentEmpty = _currentEmpty == 0 ? 1 : 0;

            SetConnectionString(_currentCstr = GetConnectionString(false, _currentEmpty));

            _thread = RebuildEmpty(GetConnectionString(false, other));
            _thread.Start();
        }

        private void SetConnectionString(string cstr)
        {
            var applicationContext = ApplicationContext.Current;
            typeof(DatabaseContext)
                .GetField("_connectionString", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(applicationContext.DatabaseContext, cstr);
            var factory = ((ScopeProvider)applicationContext.ScopeProvider).DatabaseFactory;
            typeof(DefaultDatabaseFactory)
                .GetProperty("ConnectionString", BindingFlags.Instance | BindingFlags.Public)
                .SetValue(factory, cstr);
        }

        private static Thread RebuildEmpty(string cstr)
        {
            return new Thread(() =>
            {
                using (var conn = new SqlConnection(cstr))
                using (var cmd = conn.CreateCommand())
                {
                    conn.Open();
                    ResetLocalDb(cmd);
                }
            });
        }

        public override void AttachSchema()
        {
            if (_hasDb == false)
                throw new InvalidOperationException();

            _thread.Join();

            var other = _currentSchema;
            _currentSchema = _currentSchema == 0 ? 1 : 0;

            SetConnectionString(_currentCstr = GetConnectionString(true, _currentSchema));

            _thread = RebuildSchema(GetConnectionString(true, other));
            _thread.Start();
        }

        private Thread RebuildSchema(string cstr)
        {
            return new Thread(() =>
            {
                using (var conn = new SqlConnection(cstr))
                {
                    using (var cmd = conn.CreateCommand())
                    {
                        conn.Open();
                        ResetLocalDb(cmd);

                        if (_dbCommands != null)
                        {
                            foreach (var dbCommand in _dbCommands)
                            {
                                if (dbCommand.Text.StartsWith("SELECT ")) continue;

                                cmd.CommandText = dbCommand.Text;
                                cmd.Parameters.Clear();
                                foreach (var parameterInfo in dbCommand.Parameters)
                                    AddParameter(cmd, parameterInfo);
                                cmd.ExecuteNonQuery();
                            }
                            return;
                        }
                    }

                    var applicationContext = ApplicationContext.Current;
                    using (var database = new UmbracoDatabase(conn, applicationContext.ProfilingLogger.Logger))
                    {
                        database.LogCommands = true;
                        var schemaHelper = new DatabaseSchemaHelper(database, applicationContext.ProfilingLogger.Logger, SqlSyntaxProvider);
                        schemaHelper.CreateDatabaseSchema(false, applicationContext);
                        _dbCommands = database.Commands.ToArray();
                    }
                }
            });
        }

        private static void AddParameter(IDbCommand cmd, UmbracoDatabase.ParameterInfo parameterInfo)
        {
            var p = cmd.CreateParameter();
            p.ParameterName = parameterInfo.Name;
            p.Value = parameterInfo.Value;
            p.DbType = parameterInfo.DbType;
            p.Size = parameterInfo.Size;
            cmd.Parameters.Add(p);
        }

        public override void Drop()
        {
            // we don't
        }

        public override void Clear()
        {
            if (_instance.DatabaseExists(_databaseFullName))
                _instance.DropDatabase(_databaseFullName);

            _localDb.CopyDatabaseFiles(DatabaseName + "Empty0", _filesPath, delete: true);
            _localDb.CopyDatabaseFiles(DatabaseName + "Empty1", _filesPath, delete: true);
            _localDb.CopyDatabaseFiles(DatabaseName + "Schema0", _filesPath, delete: true);
            _localDb.CopyDatabaseFiles(DatabaseName + "Schema1", _filesPath, delete: true);
        }

        private static void ResetLocalDb(IDbCommand cmd)
        {
            // https://stackoverflow.com/questions/536350

            cmd.CommandType = CommandType.Text;
            cmd.CommandText = @"
                declare @n char(1);
                set @n = char(10);

                declare @stmt nvarchar(max);

                -- check constraints
                select @stmt = isnull( @stmt + @n, '' ) +
                    'alter table [' + schema_name(schema_id) + '].[' + object_name( parent_object_id ) + '] drop constraint [' + name + ']'
                from sys.check_constraints;

                -- foreign keys
                select @stmt = isnull( @stmt + @n, '' ) +
                    'alter table [' + schema_name(schema_id) + '].[' + object_name( parent_object_id ) + '] drop constraint [' + name + ']'
                from sys.foreign_keys;

                -- tables
                select @stmt = isnull( @stmt + @n, '' ) +
                    'drop table [' + schema_name(schema_id) + '].[' + name + ']'
                from sys.tables;

                exec sp_executesql @stmt;
            ";
            cmd.ExecuteNonQuery();
        }
    }
}