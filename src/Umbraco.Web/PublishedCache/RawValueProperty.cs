using System;
using Umbraco.Core.Models.PublishedContent;

namespace Umbraco.Web.PublishedCache
{
    /// <summary>
    /// A published property base that uses a raw object value.
    /// </summary>
    /// <remarks>Conversions results are stored within the property and will not
    /// be refreshed, so this class is not suitable for cached properties.</remarks>
    internal class RawValueProperty : PublishedPropertyBase
    {
        private readonly object _dbVal; //the value in the db
        private readonly Lazy<object> _sourceValue;
        private readonly Lazy<object> _objectValue;
        private readonly Lazy<object> _xpathValue;
        private readonly bool _isPreviewing;

        /// <summary>
        /// Gets the raw value of the property.
        /// </summary>
        public override object DataValue { get { return _dbVal; } }
        
        public override bool HasValue 
        {
            get { return _dbVal != null && _dbVal.ToString().Trim().Length > 0; }
        }

        public override object Value { get { return _objectValue.Value; } }
        public override object XPathValue { get { return _xpathValue.Value; } }
        
        public RawValueProperty(PublishedPropertyType propertyType, object propertyData, bool isPreviewing = false)
            : this(propertyType)
        {
            if (propertyData == null)
                throw new ArgumentNullException("propertyData");
            _dbVal = propertyData;
            _isPreviewing = isPreviewing;
        }

        public RawValueProperty(PublishedPropertyType propertyType)
            : base(propertyType)
        {
            _dbVal = null;
            _sourceValue = new Lazy<object>(() => PropertyType.ConvertDataToSource(_dbVal, _isPreviewing));
            _objectValue = new Lazy<object>(() => PropertyType.ConvertSourceToObject(_sourceValue.Value, _isPreviewing));
            _xpathValue = new Lazy<object>(() => PropertyType.ConvertSourceToXPath(_sourceValue.Value, _isPreviewing));
        }
    }
}