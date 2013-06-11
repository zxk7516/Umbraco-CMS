using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Umbraco.Core;
using Umbraco.Core.Models;
using Umbraco.Core.Strings;
using Umbraco.Core.Xml.XPath;

namespace Umbraco.Web.PublishedCache.PublishedNoCache.Navigable
{
    class NavigableContent : INavigableContent
    {
        private readonly IPublishedContentOrMedia _content;
        private readonly object[] _builtInValues;

        public NavigableContent(IPublishedContentOrMedia content)
        {
            _content = content;

            // built-in properties (attributes)
            _builtInValues = new object[]
                {
                    _content.Name,
                    _content.ParentId,
                    _content.CreateDate,
                    _content.UpdateDate,
                    true, // isDoc
                    _content.SortOrder,
                    _content.Level,
                    _content.TemplateId,
                    _content.WriterId,
                    _content.CreatorId,
                    _content.UrlName
                };
        }

        #region INavigableContent

        public IPublishedContentOrMedia InnerContent
        {
            get { return _content; }
        }

        public int Id
        {
            get { return _content.Id; }
        }

        public int ParentId
        {
            get { return _content.ParentId; }
        }

        public INavigableContentType Type
        {
            get { return _content.NavigableContentType; }
        }

        public IList<int> ChildIds
        {
            get { return _content.ChildIds; }
        }

        public object Value(int index)
        {
            if (index < 0)
                throw new ArgumentOutOfRangeException("index");

            if (index < NavigableContentType.BuiltinProperties.Length)
            {
                // built-in field, ie attribute
                var value = _builtInValues[index];
                var field = Type.FieldTypes[index];
                // fixme - should do it once and only once
                return field.XmlStringConverter == null ? value.ToString() : field.XmlStringConverter(value);
            }

            index -= NavigableContentType.BuiltinProperties.Length;
            var properties = _content.PropertiesArray;
            if (index >= properties.Length)
                throw new ArgumentOutOfRangeException("index");

            // custom property, ie element
            return properties[index].XPathValue;
        }

        #endregion
    }
}
