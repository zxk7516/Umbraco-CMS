using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using Umbraco.Core.Models;
using Umbraco.Core.Models.PublishedContent;
using Umbraco.Core.Xml.XPath;

namespace Umbraco.Web.PublishedCache.PublishedNoCache.Navigable
{
    class NavigableContentType : INavigableContentType
    {
        public static readonly INavigableFieldType[] BuiltinProperties;

        static NavigableContentType()
        {
            BuiltinProperties = new INavigableFieldType[]
                    {
                        new NavigablePropertyType("nodeName"), 
                        new NavigablePropertyType("parentId"), 
                        new NavigablePropertyType("createDate", v => XmlConvert.ToString((DateTime)v, "yyyy-MM-ddTHH:mm:ss")),
                        new NavigablePropertyType("updateDate", v => XmlConvert.ToString((DateTime)v,  "yyyy-MM-ddTHH:mm:ss")), 
                        new NavigablePropertyType("isDoc", v => XmlConvert.ToString((bool)v)), 
                        new NavigablePropertyType("sortOrder"), 
                        new NavigablePropertyType("level"), 
                        new NavigablePropertyType("templateId"), 
                        new NavigablePropertyType("writerId"), 
                        new NavigablePropertyType("creatorId"), 
                        new NavigablePropertyType("urlName")
                    };
        }

        public NavigableContentType(PublishedContentType contentType)
        {
            Name = contentType.Alias;
            FieldTypes = BuiltinProperties
                .Union(contentType.PropertyTypes.Select(propertyType => new NavigablePropertyType(propertyType.PropertyTypeAlias)))
                .ToArray();
        }

        public string Name { get; private set; }
        public INavigableFieldType[] FieldTypes { get; protected set; }
    }
}
