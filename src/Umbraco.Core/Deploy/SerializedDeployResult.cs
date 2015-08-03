using System.Collections.Generic;

namespace Umbraco.Core.Deploy
{
    public abstract class SerializedDeployResult
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="T:System.Object"/> class.
        /// </summary>
        protected SerializedDeployResult(IEnumerable<Dependency> dependencies = null, IEnumerable<DeployableFile> files = null)
        {
            Dependencies = dependencies ?? new List<Dependency>();
            Files = files ?? new List<DeployableFile>(); ;
        }

        /// <summary>
        /// A collection of Dependencies determined for the serialized property
        /// </summary>
        public IEnumerable<Dependency> Dependencies { get; private set; }

        /// <summary>
        /// A collection of files that may need to be deployed for the property
        /// </summary>
        public IEnumerable<DeployableFile> Files { get; private set; }
    }
}