using System.Collections.Generic;

namespace Umbraco.Core.Deploy
{
    public class SerializedPropertyResult
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="T:System.Object"/> class.
        /// </summary>
        public SerializedPropertyResult(IEnumerable<DeployKey> dependencies, object serializedPropertyValue)
        {
            Dependencies = dependencies;
            SerializedPropertyValue = serializedPropertyValue;
        }

        /// <summary>
        /// A collection of Dependencies determined for the serialized property
        /// </summary>
        public IEnumerable<DeployKey> Dependencies { get; private set; } 

        /// <summary>
        /// The property value to be serialized
        /// </summary>
        public object SerializedPropertyValue { get; private set; }
    }
}