using System;
using NUnit.Framework;
using Umbraco.Core.Persistence;
using Umbraco.Core.Persistence.DatabaseAnnotations;
using Umbraco.Tests.TestHelpers;

namespace Umbraco.Tests.Persistence
{
    [DatabaseTestBehavior(DatabaseBehavior.NewDbFileAndSchemaPerTest)]
    [TestFixture]
    public class PetaPocoRowVersionTests : BaseDatabaseFactoryTest
    {
        public override void Initialize()
        {
            base.Initialize();

            var database = DatabaseContext.Database;
            var sqlSyntax = DatabaseContext.SqlSyntax;
            var schemaHelper = new DatabaseSchemaHelper(database, Logger, sqlSyntax);
            schemaHelper.CreateTable(true, typeof (TestRowVersionDto));
        }

        [Test]
        public void CannotCreateInvalidRowVersionColumn()
        {
            var database = DatabaseContext.Database;
            var sqlSyntax = DatabaseContext.SqlSyntax;
            var schemaHelper = new DatabaseSchemaHelper(database, Logger, sqlSyntax);

            Assert.Throws<Exception>(() => schemaHelper.CreateTable(true, typeof (TestInvalidRowVersionDto)));
        }

        [Test]
        public void RowVersionColumnJustWorks()
        {
            var db = DatabaseContext.Database;

            var dto = new TestRowVersionDto {Value = "value1"};
            db.Insert(dto);
            Assert.AreEqual(0, dto.RowVersion); // default value

            dto.Value = "value2";
            var uc = db.Update(dto);
            Assert.AreEqual(1, uc); // updated row
            Assert.AreEqual(1, dto.RowVersion); // updated dto

            // result: has been updated because RowVersion was OK

            dto = new TestRowVersionDto {Id = dto.Id, RowVersion = 0, Value = "value3"};
            uc = db.Update(dto);
            Assert.AreEqual(0, uc); // no updated row
            Assert.AreEqual(0, dto.RowVersion); // dto not updated

            // result: has NOT been updated because RowVersion was out-of-sync
            // now it's up to the repository to detect & report it properly

            // NOTE
            // means that every "object" must also have the ppty
            // and the factories need to map to/from DTOs...

            // NOTE
            // should NOT mix InsertOrUpdate with RowVersion as it's going to
            // fail after a number of queries, trying to insert (can't, exists)
            // and update (can't invalid rowversion).
        }
    }

    [TableName("testRowVersion1")]
    [PrimaryKey("pk")]
    [ExplicitColumns]
    internal class TestRowVersionDto
    {
        [Column("pk")]
        [PrimaryKeyColumn]
        public int Id { get; set; }

        [Column("value")]
        public string Value { get; set; }

        [RowVersionColumn("rv")]
        public long RowVersion { get; set; }
    }

    [TableName("testRowVersion2")]
    [PrimaryKey("pk")]
    [ExplicitColumns]
    internal class TestInvalidRowVersionDto
    {
        [Column("pk")]
        [PrimaryKeyColumn]
        public int Id { get; set; }

        [Column("value")]
        public string Value { get; set; }

        [RowVersionColumn("rv")]
        public int RowVersion { get; set; }
    }
}
