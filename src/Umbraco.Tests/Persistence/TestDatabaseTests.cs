using System;
using System.Data;
using System.Data.SqlClient;
using System.Data.SqlServerCe;
using System.Diagnostics;
//using Microsoft.SqlServer.Management.Smo;
using NUnit.Framework;
using Umbraco.Tests.TestHelpers;

namespace Umbraco.Tests.Persistence
{
    [TestFixture]
    [Explicit("perfs test, manual run only")]
    [DatabaseTestBehavior(DatabaseBehavior.NoDatabasePerFixture)]
    public class TestDatabaseTests : BaseDatabaseFactoryTest
    {
        protected override SupportedTestDatabase SupportedTestDatabase
        {
            get { return SupportedTestDatabase.LocalDb; }
        }

        private IDbConnection GetConnection()
        {
            switch (SupportedTestDatabase)
            {
                case SupportedTestDatabase.LocalDb:
                    return new SqlConnection(GetDbConnectionString());
                case SupportedTestDatabase.SqlCe:
                    return new SqlCeConnection(GetDbConnectionString());
                default:
                    throw new NotSupportedException(SupportedTestDatabase.ToString());
            }
        }

        [Test]
        public void Test()
        {
            // now do the dance

            // LocalDb, Empty
            // ELAPSED: 00:00:00.3662732
            // ELAPSED: 00:00:03.1893549

            // LocalDb, Schema
            // ELAPSED: 00:00:04.6987919 // but that includes an attach
            // ELAPSED: 00:00:03.0927339

            // SqlCe, Empty
            // ELAPSED: 00:00:00.2202850
            // ELAPSED: 00:00:00.0275568

            // SqlCe, Schema
            // ELAPSED: 00:00:03.8486088
            // ELAPSED: 00:00:00.0281516

            // LocalDb, Empty+CreateSchema
            // ELAPSED: 00:00:00.3662732
            // ELAPSED: 00:00:01.1588275

            // SqlCe, Empty+CreateSchema
            // ELAPSED: 00:00:00.2248277
            // ELAPSED: 00:00:03.7441615

            // LocalDb, Empty+CreateSchema+Reset
            // ...
            // ...
            // ELAPSED: 00:00:00.1405184

            // so... LocalDb attach/detach is slow ;(
            //
            // considering creating Empty is fast yet detaches,
            // one can assume that it's attaching that is slow
            //
            // schema creation on empty is way faster w/LocalDb though
            // so it might be faster to reset & recreate all the time?

            // LocalDb, Empty+CreateSchema + Reset/CreateSchema
            // ELAPSED: 00:00:03.2845424 // attach empty, 1st time
            // ELAPSED: 00:00:00.9582610 // attach schema
            // ELAPSED: 00:00:00.5239389 // attach schema
            // ELAPSED: 00:00:00.4613089
            // ELAPSED: 00:00:00.4786560
            // ELAPSED: 00:00:00.5268836
            // ELAPSED: 00:00:00.5696863
            // ELAPSED: 00:00:00.5571707

            // scripting: horribly slow, don't do it
            // capturing commands in UmbracoDatabase...

            // LocalDb, Empty+CreateSchema + Reset/CreateSchema from commands
            // slightly better
            // ELAPSED: 00:00:03.3514529
            // ELAPSED: 00:00:00.9274382
            // ELAPSED: 00:00:00.4374127
            // ELAPSED: 00:00:00.4451872
            // ELAPSED: 00:00:00.4877072
            // ELAPSED: 00:00:00.4771871

            // still, slower than SqlCe
            // end result being, the whole tests suite executes
            // in about the same time whether it's LocalDb or SqlCe

            var stopWatch = Stopwatch.StartNew();

            AttachEmptyDatabase();

            using (var conn = GetConnection())
            using (var cmd = conn.CreateCommand())
            {
                conn.Open();
                cmd.CommandType = CommandType.Text;
                cmd.CommandText = "SELECT 1";
                cmd.ExecuteNonQuery();
            }

            Console.WriteLine("ELAPSED: {0}", stopWatch.Elapsed);
            stopWatch.Restart();

            for (var i = 0; i < 5; i++)
            {
                stopWatch.Restart();

                AttachSchemaDatabase();

                using (var conn = GetConnection())
                using (var cmd = conn.CreateCommand())
                {
                    conn.Open();
                    cmd.CommandType = CommandType.Text;
                    cmd.CommandText = "SELECT 1";
                    cmd.ExecuteNonQuery();
                }

                Console.WriteLine("ELAPSED: {0}", stopWatch.Elapsed);
            }
            return;

            // localdb
            stopWatch.Restart();

            using (var conn = GetConnection())
            using (var cmd = conn.CreateCommand())
            {
                conn.Open();
                ResetLocalDb(cmd);
            }

            Console.WriteLine("ELAPSED: {0}", stopWatch.Elapsed);
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

        //private string[] ScriptLocalDb()
        //{
        //    var server = new Server(@"(localdb)\UmbracoTests");

        //    foreach (var sdb in server.Databases)
        //        Console.WriteLine("DB: {0}", sdb);

        //    var db = server.Databases[@"D:\D\UMBRACO 7.7\SRC\UMBRACO.TESTS\BIN\DEBUG\UMBRACOTESTS.MDF"];

        //    var scrp = new Scripter(server);
        //    scrp.Options.ScriptDrops = false;
        //    scrp.Options.WithDependencies = true;

        //    var sql = new List<string>();

        //    var smoObjects = new Microsoft.SqlServer.Management.Sdk.Sfc.Urn[1];
        //    foreach (Table tb in db.Tables)
        //    {
        //        smoObjects[0] = tb.Urn;
        //        if (tb.IsSystemObject == false)
        //        {
        //            System.Collections.Specialized.StringCollection sc;
        //            sc = scrp.Script(smoObjects);
        //            foreach (var st in sc)
        //            {
        //                sql.Add(st);
        //                //Console.WriteLine(st);
        //            }
        //        }
        //    }

        //    return sql.ToArray();
        //}
    }
}
