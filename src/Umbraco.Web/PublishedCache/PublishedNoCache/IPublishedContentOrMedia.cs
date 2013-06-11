using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Umbraco.Core.Models;
using Umbraco.Core.Xml.XPath;

namespace Umbraco.Web.PublishedCache.PublishedNoCache
{
    internal interface IPublishedContentOrMedia : IPublishedContent
    {
        // gets the properties as an array of properties
        IPublishedProperty[] PropertiesArray { get; }

        // gets the parent ID
        int ParentId { get; }

        // gets the child IDs
        // includes all children, published or unpublished
        IList<int> ChildIds { get; }

        // gets the corresponding navigable content type
        INavigableContentType NavigableContentType { get; }

        // gets a value indicating whether the content or media exists in
        // a previewing context or not, ie whether its Parent, Children, and
        // properties should refer to published, or draft content
        bool IsPreviewing { get; }
    }
}
