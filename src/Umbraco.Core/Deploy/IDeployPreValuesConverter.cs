using Umbraco.Core.Models;
using Umbraco.Core.Models.PublishedContent;

namespace Umbraco.Core.Deploy
{
    /// <summary>
    /// Used to convert data type values during a Deployment
    /// </summary>
    public interface IDeployPreValuesConverter
    {
        /// <summary>
        /// Gets a value indicating whether the converter supports a property type.
        /// </summary>
        /// <param name="dataType">The data type.</param>
        bool IsConverter(IDataTypeDefinition dataType);

        /// <summary>
        /// Serializes the stored pre-values for a data type
        /// </summary>
        /// <param name="preValues"></param>
        /// <returns></returns>
        /// <remarks>
        /// Since pre-value values can store any sort of data, in some cases this data needs to be converted
        /// to something that is deployable. For example, if the pre-value value is an integer ID referencing a node (i.e. 1234)
        /// then this integer id would need to be serialized as a GUID. Then during deserialization it would need to be re-converted
        /// into an integer for persistence.
        /// </remarks>
        SerializedPreValuesResult GetSerializedPreValues(PreValueCollection preValues);
    }
}