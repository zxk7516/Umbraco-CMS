using System.Collections.Generic;
using Umbraco.Core.Models.PublishedContent;

namespace Umbraco.Core.Models
{
    public class Grid2Row
    {
        public string Alias { get; set; }

        public string Name { get; set; }

        public IPublishedElement Settings { get; set; }

        public IEnumerable<Grid2Cell> Cells { get; set; }
    }
}
