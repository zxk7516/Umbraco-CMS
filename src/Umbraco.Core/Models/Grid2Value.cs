using System.Collections.Generic;

namespace Umbraco.Core.Models
{
    public class Grid2Value
    {
        public int Columns { get; set; }

        public IEnumerable<Grid2Row> Rows { get; set; }
    }
}
