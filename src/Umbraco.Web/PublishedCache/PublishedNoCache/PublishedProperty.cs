using System;
using System.Xml.Serialization;
using Umbraco.Core.Models;
using Umbraco.Core.Models.PublishedContent;

namespace Umbraco.Web.PublishedCache.PublishedNoCache
{
    [Serializable]
    [XmlType(Namespace = "http://umbraco.org/webservices/")]
    class PublishedProperty : PublishedPropertyBase
    {
        private readonly object _dataValue;
        private readonly bool _isPreviewing;

        public PublishedProperty(PublishedPropertyType propertyType, object dataValue, bool isPreviewing)
            : base(propertyType)
        {
            _dataValue = dataValue;
            _isPreviewing = isPreviewing;
        }

        public override bool HasValue
        {
            get { return _dataValue != null && ((_dataValue is string) == false || string.IsNullOrWhiteSpace((string)_dataValue) == false); }
        }

        public override object DataValue
        {
            get { return _dataValue; }
        }

        public override object Value
        {
            get
            {
                var source = PropertyType.ConvertDataToSource(_dataValue, _isPreviewing);
                return PropertyType.ConvertSourceToObject(source, _isPreviewing);
            }
        }

        public override object XPathValue
        {
            get
            {
                var source = PropertyType.ConvertDataToSource(_dataValue, _isPreviewing);
                return PropertyType.ConvertSourceToXPath(source, _isPreviewing);
            }
        }
    }
}
