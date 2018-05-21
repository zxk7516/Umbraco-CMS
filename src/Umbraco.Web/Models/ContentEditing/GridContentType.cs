using System.Collections.Generic;
using System.Runtime.Serialization;
using Umbraco.Core;

namespace Umbraco.Web.Models.ContentEditing
{
    /// <summary>
    /// A simplified content type model used to render grid cells
    /// </summary>
    [DataContract(Name = "contentType", Namespace = "")]
    public class GridContentType
    {
        [DataMember(Name = "id")]
        public int Id { get; set; }

        [DataMember(Name = "udi")]
        public Udi Udi { get; set; }

        [DataMember(Name = "name")]
        public string Name { get; set; }

        [DataMember(Name = "alias")]
        public string Alias { get; set; }

        [DataMember(Name = "icon")]
        public string Icon { get; set; }

        [DataMember(Name = "views")]
        public IDictionary<string, GridEditorPath> Views { get; set; }
    }

    [DataContract(Name = "contentType", Namespace = "")]
    public class GridEditorPath
    {
        public GridEditorPath()
        {

        }

        public GridEditorPath(string view, bool isPreview)
        {
            View = view;
            IsPreview = isPreview;
        }

        [DataMember(Name = "view")]
        public string View { get; set; }

        [DataMember(Name = "isPreview")]
        public bool IsPreview { get; set; }
    }
}
