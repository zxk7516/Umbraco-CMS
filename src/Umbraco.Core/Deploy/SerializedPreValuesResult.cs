using System.Collections.Generic;

namespace Umbraco.Core.Deploy
{
    public class SerializedPreValuesResult : SerializedDeployResult
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="T:System.Object"/> class.
        /// </summary>
        public SerializedPreValuesResult(
            IDictionary<string, string> preValues,
            IEnumerable<Dependency> dependencies = null, 
            IEnumerable<DeployableFile> files = null) : base(dependencies, files)
        {
            PreValues = preValues;
        }

        public IDictionary<string, string> PreValues { get; private set; }

    }
}