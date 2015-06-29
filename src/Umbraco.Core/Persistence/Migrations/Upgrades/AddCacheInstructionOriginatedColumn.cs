using System.Linq;
using Umbraco.Core.Configuration;
using Umbraco.Core.Logging;
using Umbraco.Core.Persistence.SqlSyntax;

namespace Umbraco.Core.Persistence.Migrations.Upgrades
{
    [Migration("7.3.0", 2, GlobalSettings.UmbracoMigrationName)]
    public class AddCacheInstructionOriginatedColumn : MigrationBase
    {
        public AddCacheInstructionOriginatedColumn(ISqlSyntaxProvider sqlSyntax, ILogger logger)
            : base(sqlSyntax, logger)
        { }

        public override void Up()
        {
            // don't execute if the column is already there
            var columns = SqlSyntax.GetColumnsInSchema(Context.Database).ToArray();
            if (columns.Any(x => x.TableName.InvariantEquals("umbracoCacheInstruction") && x.ColumnName.InvariantEquals("originated")) == false)
                Create.Column("originated").OnTable("umbracoCacheInstruction").AsString(500).NotNullable();
        }

        public override void Down()
        {
            // nothing - going down, the table will be removed anyway
        }
    }
}
