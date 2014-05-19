using System.Runtime.Serialization;
using Newtonsoft.Json;

namespace Umbraco.Web.Routing.Segments
{
    [DataContract(Name = "segmentConfig", Namespace = "")]
    public class SegmentProviderMatch
    {
        [DataMember(Name = "key")]       
        public string Key { get; set; }
        
        [DataMember(Name = "value")]       
        public string Value { get; set; }

        [DataMember(Name = "matchExpression")]       
        public string MatchExpression { get; set; }
    }
}