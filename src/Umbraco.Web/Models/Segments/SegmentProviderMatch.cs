using System.Runtime.Serialization;

namespace Umbraco.Web.Models.Segments
{
    /// <summary>
    /// Used as a configuration item for a configurable segment provider
    /// </summary>
    [DataContract(Name = "segmentConfig", Namespace = "")]
    public class SegmentProviderMatch
    {
        /// <summary>
        /// The Segment Key to add to the request
        /// </summary>
        [DataMember(Name = "key")]       
        public string Key { get; set; }
        
        /// <summary>
        /// The segment Value to add to the request for this match's Key
        /// </summary>
        [DataMember(Name = "value")]       
        public string Value { get; set; }

        /// <summary>
        /// The configured expression to match against the advertised value of a configurable segment provider
        /// </summary>
        [DataMember(Name = "matchExpression")]       
        public string MatchExpression { get; set; }
    }
}