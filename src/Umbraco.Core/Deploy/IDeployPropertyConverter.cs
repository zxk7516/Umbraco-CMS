using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Umbraco.Core.Models.PublishedContent;
using Umbraco.Core.PropertyEditors;

namespace Umbraco.Core.Deploy
{
    /// <summary>
    /// Used to convert a property value during a Deployment
    /// </summary>
    public interface IDeployPropertyConverter
    {
        /// <summary>
        /// Gets a value indicating whether the converter supports a property type.
        /// </summary>
        /// <param name="propertyType">The property type.</param>
        /// <returns>A value indicating whether the converter supports a property type.</returns>
        bool IsConverter(PublishedPropertyType propertyType);

        /// <summary>
        /// Returns a SerializedPropertyResult for the property's stored value
        /// </summary>
        /// <param name="propertyType"></param>
        /// <param name="source">The persisted value of the property</param>
        /// <returns></returns>
        SerializedPropertyResult GetSerializedPropertyResult(PublishedPropertyType propertyType, object source);

        /// <summary>
        /// Returns the value that will be used for persisting to the database from the serialized value passed in
        /// </summary>
        /// <param name="propertyType"></param>
        /// <param name="serializedValue"></param>
        /// <returns></returns>
        object GetDeserializedPropertyValue(PublishedPropertyType propertyType, string serializedValue);
    }
}
