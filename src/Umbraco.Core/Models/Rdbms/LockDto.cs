using Umbraco.Core.Persistence;
using Umbraco.Core.Persistence.DatabaseAnnotations;

namespace Umbraco.Core.Models.Rdbms
{
    [TableName("umbracoLock")]
    [PrimaryKey("id")]
    [ExplicitColumns]
    internal class LockDto
    {
        [Column("id")]
        [PrimaryKeyColumn(Name = "PK_structure")]
        public int Id { get; set; }

        [Column("value")]
        public int Value { get; set; } = 1;
    }
}