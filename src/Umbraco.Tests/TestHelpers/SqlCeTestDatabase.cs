using System;
using System.Data.SqlServerCe;
using System.IO;
using System.Threading;
using SQLCE4Umbraco;
using Umbraco.Core.Persistence.SqlSyntax;

namespace Umbraco.Tests.TestHelpers
{
    public class SqlCeTestDatabase : TestDatabase
    {
        private const string DatabaseName = "UmbracoPetaPocoTests";
        private static string DefaultConnectionString = @"Datasource=|DataDirectory|UmbracoPetaPocoTests.sdf;Flush Interval=1;";

        private readonly string _filesPath;
        private readonly string _databaseFullPath;
        private ISqlSyntaxProvider _syntaxProvider;
        private byte[] _emptyDbBytes;
        private byte[] _schemaDbBytes;

        public SqlCeTestDatabase(string filesPath)
        {
            _filesPath = filesPath;
            _databaseFullPath = Path.Combine(_filesPath, DatabaseName + ".sdf");
        }

        public override string ProviderName
        {
            get { return "System.Data.SqlServerCe.4.0"; }
        }

        public override string ConnectionString
        {
            get
            {
                //return string.Format(@"Datasource=|DataDirectory|{0}.sdf;Flush Interval=1;", DatabaseName);
                return string.Format(@"Datasource={0};Flush Interval=1;", _databaseFullPath);
            }
        }

        public override ISqlSyntaxProvider SqlSyntaxProvider
        {
            get { return _syntaxProvider ?? (_syntaxProvider = new SqlCeSyntaxProvider()); }
        }

        public override bool HasEmpty
        {
            get { return _emptyDbBytes != null; }
        }

        public override bool HasSchema
        {
            get { return _schemaDbBytes != null; }
        }

        public override void Create()
        {
            using (var engine = new SqlCeEngine(ConnectionString))
            {
                engine.CreateDatabase();
            }
        }

        public override void CaptureEmpty()
        {
            SqlCeContextGuardian.CloseBackgroundConnection();
            _emptyDbBytes = File.ReadAllBytes(_databaseFullPath);
        }

        public override void CaptureSchema()
        {
            SqlCeContextGuardian.CloseBackgroundConnection();
            _schemaDbBytes = File.ReadAllBytes(_databaseFullPath);
        }

        public override void AttachEmpty()
        {
            if (_emptyDbBytes == null)
                throw new InvalidOperationException();
            SqlCeContextGuardian.CloseBackgroundConnection();
            if (File.Exists(_databaseFullPath))
                FileDelete(_databaseFullPath, TimeSpan.FromSeconds(2));
            File.WriteAllBytes(_databaseFullPath, _emptyDbBytes);
        }

        public override void AttachSchema()
        {
            if (_schemaDbBytes == null)
                throw new InvalidOperationException();
            SqlCeContextGuardian.CloseBackgroundConnection();
            if (File.Exists(_databaseFullPath))
                FileDelete(_databaseFullPath, TimeSpan.FromSeconds(2));
            File.WriteAllBytes(_databaseFullPath, _schemaDbBytes);
        }

        public override void Drop()
        {
            SqlCeContextGuardian.CloseBackgroundConnection();
            if (File.Exists(_databaseFullPath))
                FileDelete(_databaseFullPath, TimeSpan.FromSeconds(2));
        }

        public override void Clear()
        {
            Drop();
        }

        private static void FileDelete(string filename, TimeSpan timeout)
        {
            const int period = 200; // ms
            var max = timeout.TotalMilliseconds / period;
            for (var i = 0; ; i++)
            {
                try
                {
                    File.Delete(filename);
                    break;
                }
                catch (IOException)
                {
                    if (i > max)
                        throw;
                    Thread.Sleep(period);
                }
            }
        }
    }
}
