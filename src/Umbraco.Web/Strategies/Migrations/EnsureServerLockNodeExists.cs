using System;
using Semver;
using Umbraco.Core;
using Umbraco.Core.Events;
using Umbraco.Core.Models.Rdbms;
using Umbraco.Core.Persistence;
using Umbraco.Core.Persistence.Migrations;
using Umbraco.Core.Persistence.SqlSyntax;

namespace Umbraco.Web.Strategies.Migrations
{
    public class EnsureServerLockNodeExists : MigrationStartupHander
    {
        protected override void AfterMigration(MigrationRunner sender, MigrationEventArgs e)
        {
            var target = new SemVersion(7, 3);

            if (e.ConfiguredSemVersion > target)
                return;

            var context = e.MigrationContext;
            var sqlSyntax = SqlSyntaxContext.SqlSyntaxProvider;

            // wrap in a transaction so that everything runs on the same connection
            // and the IDENTITY_INSERT stuff is effective for all inserts.
            using (var tr = context.Database.GetTransaction())
            {
                // turn on identity insert if db provider is not mysql
                if (sqlSyntax.SupportsIdentityInsert())
                    context.Database.Execute(new Sql(string.Format("SET IDENTITY_INSERT {0} ON", sqlSyntax.GetQuotedTableName("umbracoNode"))));

                EnsureLockNode(context, Constants.System.ServersLock, "0AF5E610-A310-4B6F-925F-E928D5416AF7", "LOCK: Servers");
                EnsureLockNode(context, Constants.System.ContentTypesLock, "A88CE058-A627-46BB-878A-07C5FE3D870A", "LOCK: ContentTypes");
                EnsureLockNode(context, Constants.System.ContentTreeLock, "3FB9211A-B4F9-449C-8725-A075AE500518", "LOCK: ContentTree");
                EnsureLockNode(context, Constants.System.MediaTreeLock, "5B4408F9-D9FB-4145-84BB-5F0F2C35B4B0", "LOCK: MediaTree");
                EnsureLockNode(context, Constants.System.MemberTreeLock, "FA951390-DF12-4594-8366-89CA8396D977", "LOCK: MemberTree");
                EnsureLockNode(context, Constants.System.MediaTypesLock, "BA13E02F-E595-4415-8693-B044C83AA9A7", "LOCK: MediaTypes");
                EnsureLockNode(context, Constants.System.MemberTypesLock, "B23EED6B-CC05-48FE-B096-B50441D0E825", "LOCK: MemberTypes");
                EnsureLockNode(context, Constants.System.DomainsLock, "0AF5E610-A310-4B6F-925F-E928D5416AF7", "LOCK: Domains");

                // turn off identity insert if db provider is not mysql
                if (sqlSyntax.SupportsIdentityInsert())
                    context.Database.Execute(new Sql(string.Format("SET IDENTITY_INSERT {0} OFF", sqlSyntax.GetQuotedTableName("umbracoNode"))));

                tr.Complete();
            }
        }

        private static void EnsureLockNode(IMigrationContext context, int id, string uniqueId, string text)
        {
            var exists = context.Database.Exists<NodeDto>(id);
            if (exists) return;

            context.Database.Insert("umbracoNode", "id", false, new NodeDto
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
