using Newtonsoft.Json.Linq;
using Umbraco.Core.PropertyEditors;

namespace Umbraco.Web.PropertyEditors
{
    /// <summary>
    /// Represents the configuration for the grid value editor
    /// </summary>
    public class Grid2Configuration
    {
        [ConfigurationField("items", "Grid", "views/propertyeditors/grid2/grid.prevalues.html", Description = "Grid configuration")]
        public JObject Items { get; set; }
    }
}
