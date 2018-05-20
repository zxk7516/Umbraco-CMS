using System.Collections.Generic;
using Umbraco.Core.Models.PublishedContent;

namespace Umbraco.Core.Models
{
    public class Grid2Cell
    {
        public int Colspan { get; set; }

        public IPublishedElement Settings { get; set; }
            
        public IEnumerable<IPublishedElement> Items { get; set; }
    }
}
