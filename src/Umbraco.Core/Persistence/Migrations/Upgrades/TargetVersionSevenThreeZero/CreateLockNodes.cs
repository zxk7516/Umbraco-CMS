using System;
using Umbraco.Core.Configuration;
using Umbraco.Core.Models.Rdbms;
using Umbraco.Core.Persistence.SqlSyntax;

namespace Umbraco.Core.Persistence.Migrations.Upgrades.TargetVersionSevenThreeZero
{
    [Migration("7.3.0", 100, GlobalSettings.UmbracoMigrationName)]
    public class CreateLockNodes : MigrationBase
    {
        public override void Up()
        {
            // turn on identity insert if db provider is not mysql
            if (SqlSyntax.SupportsIdentityInsert())
                Context.Database.Execute(new Sql(string.Format("SET IDENTITY_INSERT {0} ON ", SqlSyntax.GetQuotedTableName("umbracoNode"))));

            InsertLockObject(Constants.System.ContentTypesLock, "A88CE058-A627-46BB-878A-07C5FE3D870A", "LOCK: ContentTypes");
            InsertLockObject(Constants.System.ContentTreeLock, "3FB9211A-B4F9-449C-8725-A075AE500518", "LOCK: ContentTree");
            InsertLockObject(Constants.System.MediaTreeLock, "5B4408F9-D9FB-4145-84BB-5F0F2C35B4B0", "LOCK: MediaTree");
            InsertLockObject(Constants.System.MemberTreeLock, "FA951390-DF12-4594-8366-89CA8396D977", "LOCK: MemberTree");

            // turn off identity insert if db provider is not mysql
            if (SqlSyntax.SupportsIdentityInsert())
                Context.Database.Execute(new Sql(string.Format("SET IDENTITY_INSERT {0} OFF;", SqlSyntax.GetQuotedTableName("umbracoNode"))));

        }

        public override void Down()
        {
            Context.Database.Execute("DELETE FROM umbracoNode WHERE id=@id", new { @id = Constants.System.ContentTypesLock });
            Context.Database.Execute("DELETE FROM umbracoNode WHERE id=@id", new { @id = Constants.System.ContentTreeLock });
            Context.Database.Execute("DELETE FROM umbracoNode WHERE id=@id", new { @id = Constants.System.MediaTreeLock });
            Context.Database.Execute("DELETE FROM umbracoNode WHERE id=@id", new { @id = Constants.System.MemberTreeLock });
        }

        private void InsertLockObject(int id, string uniqueId, string text)
        {
            Context.Database.Insert("umbracoNode", "id", false, new NodeDto
            {
                NodeId = id,
                Trashed = false,
                ParentId = -1,
                UserId = 0,
                Level = 1,
                Path = "-1," + id,
                SortOrder = 0,
                UniqueId = new Guid(uniqueId),
                Text = text,
                NodeObjectType = new Guid(Constants.ObjectTypes.LockObject),
                CreateDate = DateTime.Now
            });
        }
    }
}
