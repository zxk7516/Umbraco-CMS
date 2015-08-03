using System.Collections.Generic;

namespace Umbraco.Core.Deploy
{
    public class SerializedPropertyResult : SerializedDeployResult
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="T:System.Object"/> class.
        /// </summary>
        public SerializedPropertyResult(
            object serializedPropertyValue, 
            IEnumerable<Dependency> dependencies = null, 
            IEnumerable<DeployableFile> files = null)
            :base(dependencies, files)
        {   
            SerializedPropertyValue = serializedPropertyValue;
        }

        /// <summary>
        /// The property value to be serialized
        /// </summary>
        public object SerializedPropertyValue { get; private set; }
    }
}