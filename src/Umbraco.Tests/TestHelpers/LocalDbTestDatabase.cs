using System;
using System.Collections.Concurrent;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Threading;
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
        private static LocalDb.Instance _instance;
        private static string _filesPath;
        private ISqlSyntaxProvider _syntaxProvider;
        private UmbracoDatabase.CommandInfo[] _dbCommands;
        private string _currentCstr;
        private static DatabasePool _emptyPool;
        private static DatabasePool _schemaPool;
        private DatabasePool _currentPool;

        public LocalDbTestDatabase(LocalDb localDb, string filesPath)
        {
            _localDb = localDb;
            _filesPath = filesPath;

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
            get { return _currentCstr ?? _instance.GetAttachedConnectionString("XXXXXX", _filesPath); }
        }

        public override ISqlSyntaxProvider SqlSyntaxProvider
        {
            get { return _syntaxProvider ?? (_syntaxProvider = new SqlServerSyntaxProvider()); }
        }

        private void Create()
        {
            var tempName = Guid.NewGuid().ToString("N");
            _instance.CreateDatabase(tempName, _filesPath);
            _instance.DetachDatabase(tempName);

            // there's probably a sweet spot to be found for size / parallel...

            var s = ConfigurationManager.AppSettings["Umbraco.Tests.LocalDbTestDatabase.EmptyPoolSize"];
            var emptySize = s == null ? 2 : int.Parse(s);
            s = ConfigurationManager.AppSettings["Umbraco.Tests.LocalDbTestDatabase.EmptyPoolThreadCount"];
            var emptyParallel = s == null ? 1 : int.Parse(s);
            s = ConfigurationManager.AppSettings["Umbraco.Tests.LocalDbTestDatabase.SchemaPoolSize"];
            var schemaSize = s == null ? 2 : int.Parse(s);
            s = ConfigurationManager.AppSettings["Umbraco.Tests.LocalDbTestDatabase.SchemaPoolThreadCount"];
            var schemaParallel = s == null ? 1 : int.Parse(s);

            _emptyPool = new DatabasePool(_localDb, _instance, DatabaseName + "-Empty", tempName, _filesPath, emptySize, emptyParallel);
            _schemaPool = new DatabasePool(_localDb, _instance, DatabaseName + "-Schema", tempName, _filesPath, schemaSize, schemaParallel, delete: true, prepare: RebuildSchema);
        }

        public override void AttachEmpty()
        {
            if (_emptyPool == null)
                Create();

            _currentCstr = _emptyPool.AttachDatabase();
            _currentPool = _emptyPool;
        }

        public override void AttachSchema()
        {
            if (_schemaPool == null)
                Create();

            _currentCstr = _schemaPool.AttachDatabase();
            _currentPool = _schemaPool;
        }

        public override void Detach()
        {
            _currentPool.DetachDatabase();
        }

        private void RebuildSchema(IDbConnection conn, IDbCommand cmd)
        {
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

            var applicationContext = ApplicationContext.Current;
            using (var database = new UmbracoDatabase(conn, applicationContext.ProfilingLogger.Logger))
            {
                database.LogCommands = true;
                var schemaHelper = new DatabaseSchemaHelper(database, applicationContext.ProfilingLogger.Logger, SqlSyntaxProvider);
                schemaHelper.CreateDatabaseSchema(false, applicationContext);
                _dbCommands = database.Commands.ToArray();
            }
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

        public override void Clear()
        {
            var filename = Path.Combine(_filesPath, DatabaseName).ToUpper();

            foreach (var database in _instance.GetDatabases())
            {
                if (database.StartsWith(filename))
                    _instance.DropDatabase(database);
            }

            foreach (var file in Directory.EnumerateFiles(_filesPath))
            {
                if (file.EndsWith(".mdf") == false && file.EndsWith(".ldf") == false) continue;
                File.Delete(file);
            }
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

        public static void KillLocalDb()
        {
            if (_emptyPool != null)
                _emptyPool.Stop();
            if (_schemaPool != null)
                _schemaPool.Stop();

            var filename = Path.Combine(_filesPath, DatabaseName).ToUpper();

            foreach (var database in _instance.GetDatabases())
            {
                if (database.StartsWith(filename))
                    _instance.DropDatabase(database);
            }

            foreach (var file in Directory.EnumerateFiles(_filesPath))
            {
                if (file.EndsWith(".mdf") == false && file.EndsWith(".ldf") == false) continue;
                File.Delete(file);
            }
        }

        private class DatabasePool
        {
            private readonly LocalDb _localDb;
            private readonly LocalDb.Instance _instance;
            private readonly string _filesPath;
            private readonly string _name;
            private readonly int _size;
            private readonly string[] _cstrs;
            private readonly BlockingCollection<int> _prepareQueue, _readyQueue;
            private readonly Action<IDbConnection, IDbCommand> _prepare;
            private int _current;

            public DatabasePool(LocalDb localDb, LocalDb.Instance instance, string name, string tempName, string filesPath, int size, int parallel = 1, Action<IDbConnection, IDbCommand> prepare = null, bool delete = false)
            {
                _localDb = localDb;
                _instance = instance;
                _filesPath = filesPath;
                _name = name;
                _size = size;
                _prepare = prepare;
                _prepareQueue = new BlockingCollection<int>();
                _readyQueue = new BlockingCollection<int>();
                _cstrs = new string[_size];

                for (var i = 0; i < size; i++)
                    localDb.CopyDatabaseFiles(tempName, filesPath, targetDatabaseName: name + "-" + i, overwrite: true, delete: delete && i == size - 1);

                if (prepare == null)
                {
                    for (var i = 0; i < size; i++)
                        _readyQueue.Add(i);
                }
                else
                {
                    for (var i = 0; i < size; i++)
                        _prepareQueue.Add(i);
                }

                for (var i = 0; i < parallel; i++)
                {
                    var thread = new Thread(PrepareThread);
                    thread.Start();
                }
            }

            public string AttachDatabase()
            {
                try
                {
                    _current = _readyQueue.Take();
                }
                catch (InvalidOperationException)
                {
                    _current = 0;
                    return null;
                }
                return ConnectionString(_current);
            }

            public void DetachDatabase()
            {
                _prepareQueue.Add(_current);
            }

            private string ConnectionString(int i)
            {
                return _cstrs[i] ?? (_cstrs[i] = _instance.GetAttachedConnectionString(_name + "-" + i, _filesPath));
            }

            private void PrepareThread()
            {
                while (_prepareQueue.IsCompleted == false)
                {
                    int i;
                    try
                    {
                        i = _prepareQueue.Take();
                    }
                    catch (InvalidOperationException)
                    {
                        continue;
                    }
                    using (var conn = new SqlConnection(ConnectionString(i)))
                    using (var cmd = conn.CreateCommand())
                    {
                        conn.Open();
                        ResetLocalDb(cmd);
                        if (_prepare != null) _prepare(conn, cmd);
                    }
                    _readyQueue.Add(i);
                }
            }

            public void Stop()
            {
                int i;
                _prepareQueue.CompleteAdding();
                while (_prepareQueue.TryTake(out i)) { }
                _readyQueue.CompleteAdding();
                while (_readyQueue.TryTake(out i)) { }
            }
        }
    }
}