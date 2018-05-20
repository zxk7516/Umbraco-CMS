using System.Runtime.Serialization;
using Umbraco.Core.Models;

namespace Umbraco.Web.Models.ContentEditing
{
    [DataContract(Name = "gridCell", Namespace = "")]
    public class GridContentCell : TabbedContentItem<ContentPropertyDisplay, IContentBase>
    {

    }
}
