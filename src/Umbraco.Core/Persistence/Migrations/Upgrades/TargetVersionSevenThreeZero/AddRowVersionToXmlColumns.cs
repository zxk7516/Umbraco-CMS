using System.Linq;
using Umbraco.Core.Configuration;

namespace Umbraco.Core.Persistence.Migrations.Upgrades.TargetVersionSevenThreeZero
{
    [Migration("7.3.0", 9, GlobalSettings.UmbracoMigrationName)]
    public class AddRowVersionToXmlColumns : MigrationBase
    {
        public override void Up()
        {
            if (Exists("cmsContentXml", "Rv") == false)
                Alter.Table("cmsContentXml").AddColumn("Rv").AsInt64().NotNullable().WithDefaultValue(0);

            if (Exists("cmsPreviewXml", "Rv") == false)
                Alter.Table("cmsPreviewXml").AddColumn("Rv").AsInt64().NotNullable().WithDefaultValue(0);
        }

        public override void Down()
        {
            if (Exists("cmsContentXml", "Rv"))
                Delete.Column("Rv").FromTable("cmsContentXml");
            if (Exists("cmsPreviewXml", "Rv"))
                Delete.Column("Rv").FromTable("cmsContentXml");
        }

        private bool Exists(string tableName, string columnName)
        {
            var columns = SqlSyntax.GetColumnsInSchema(Context.Database).Distinct().ToArray();
            return columns.Any(x => x.TableName.InvariantEquals(tableName) && x.ColumnName.InvariantEquals(columnName));
        }
    }
}
