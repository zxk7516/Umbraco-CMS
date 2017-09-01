using System;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using Umbraco.Core;
using Umbraco.Core.Persistence;
using Umbraco.Core.Persistence.SqlSyntax;

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

        public override string ConnectionString
        {
            get { return _instance.GetAttachedConnectionString(DatabaseName, _filesPath); }
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
            _localDb.CopyDatabaseFiles(tempName, _filesPath, targetDatabaseName: DatabaseName, delete: true, overwrite: true);
            // cannot attach this way (full name contains path), let it auto-attach
            //_instance.AttachDatabase(_databaseFullName, _filesPath);
        }

        public override void CaptureEmpty()
        {
            // we don't - just remember we have a db
            _hasDb = true;
        }

        public override void CaptureSchema()
        {
            // we don't - just remember we have a db
            _hasDb = true;
        }

        public override void AttachEmpty()
        {
            if (_hasDb == false)
                throw new InvalidOperationException();
            using (var conn = new SqlConnection(ConnectionString))
            using (var cmd = conn.CreateCommand())
            {
                conn.Open();
                ResetLocalDb(cmd);
            }
        }

        public override void AttachSchema()
        {
            if (_hasDb == false)
                throw new InvalidOperationException();
            using (var conn = new SqlConnection(ConnectionString))
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
                        foreach (var parameter in dbCommand.Parameters)
                        {
                            var p = cmd.CreateParameter();
                            p.ParameterName = parameter.Name;
                            p.Value = parameter.Value;
                            p.DbType = parameter.DbType;
                            p.Size = parameter.Size;
                            cmd.Parameters.Add(p);
                        }
                        cmd.ExecuteNonQuery();
                    }
                    return;
                }
            }

            var applicationContext = ApplicationContext.Current;
            var schemaHelper = new DatabaseSchemaHelper(applicationContext.DatabaseContext.Database, applicationContext.ProfilingLogger.Logger, SqlSyntaxProvider);
            schemaHelper.LogCommands = true;
            schemaHelper.CreateDatabaseSchema(false, applicationContext);
            _dbCommands = schemaHelper.Commands.ToArray();
        }

        public override void Drop()
        {
            // we don't
            //if (_instance.DatabaseExists(_databaseFullName))
            //    _instance.DropDatabase(_databaseFullName);
        }

        public override void Clear()
        {
            if (_instance.DatabaseExists(_databaseFullName))
                _instance.DropDatabase(_databaseFullName);

            _localDb.CopyDatabaseFiles(DatabaseName, _filesPath, delete: true);
        }

        private void ResetLocalDb(IDbCommand cmd)
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