using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Umbraco.Core.Models.ContentVariations
{
    /// <summary>
    /// A class representing a provider assigned segment to the current request
    /// </summary>
    public class AssignedSegment
    {
        public AssignedSegment(string segmentName, Type providerType)
        {
            SegmentName = segmentName;
            ProviderType = providerType;
        }

        /// <summary>
        /// The segment name/alias being assigned by the provider
        /// </summary>
        public string SegmentName { get; private set; }

        /// <summary>
        /// The system Type of the provider that assigned the segment
        /// </summary>
        /// <remarks>
        /// This can be useful if more than one provider assigns the same segment
        /// </remarks>
        public Type ProviderType { get; private set; }

    }
}
