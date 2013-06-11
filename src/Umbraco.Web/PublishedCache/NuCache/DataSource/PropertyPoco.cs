using System;

namespace Umbraco.Web.PublishedCache.NuCache.DataSource
{
    class PropertyPoco
    {
        public int NodeId { get; set; }
        public Guid VersionId { get; set; }
        //public int PropertyTypeId { get; set; }
        public string Alias { get; set; }
        public int? ValueInt { get; set; }
        public DateTime? ValueDateTime { get; set; }
        public string ValueVarchar { get; set; }
        public string ValueText { get; set; }

        public object Value
        {
            get
            {
                if (ValueInt.HasValue) return ValueInt.Value;
                if (ValueDateTime.HasValue) return ValueDateTime;
                if (ValueVarchar != null) return ValueVarchar;
                if (ValueText != null) return ValueText;
                return null;
            }
        }
    }
}
