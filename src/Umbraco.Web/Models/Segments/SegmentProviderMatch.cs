using System.Runtime.Serialization;

namespace Umbraco.Web.Models.Segments
{
    /// <summary>
    /// Used as a configuration item for a configurable segment provider
    /// </summary>
    [DataContract(Name = "segment", Namespace = "")]
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

        /// <summary>
        /// A flag of whether or not to persist the segment match to the user's cookies (and to the member profile if they are logged in)
        /// </summary>
        /// <remarks>
        /// This might be useful if you want to track a segment from a provider, perhaps if it's a referral provider a segment might be set 
        /// when coming from mysite.com and you want to know about that later on when the segment no longer exists in the request.
        /// </remarks>
        [DataMember(Name = "persist")]
        public bool Persist { get; set; }
    }
}