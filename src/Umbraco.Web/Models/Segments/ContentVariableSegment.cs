using System.Runtime.Serialization;

namespace Umbraco.Web.Models.Segments
{
    /// <summary>
    /// A definition of a segment that can be used to create content variants
    /// </summary>
    [DataContract(Name = "segment", Namespace = "")]
    public class ContentVariableSegment
    {
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="key"></param>
        /// <param name="isLanguage"></param>
        /// <param name="exists"></param>
        public ContentVariableSegment(string key, bool isLanguage, bool exists)
        {
            Key = key;
            IsLanguage = isLanguage;
            Exists = exists;
        }

        /// <summary>
        /// The segment Key associated with the content variant
        /// </summary>
        [DataMember(Name = "key")]       
        public string Key { get; private set; }

        /// <summary>
        /// Whether or not the key for this segment is based on a language
        /// </summary>
        /// <remarks>
        /// In the case that it is based on a language, the key will be the ISO lang code like en-US
        /// </remarks>
        [DataMember(Name = "isLanguage")]
        public bool IsLanguage { get; private set; }

        /// <summary>
        /// Whether or not a content variant current exists for this segment for a content item
        /// </summary>
        [DataMember(Name = "exists")]
        public bool Exists { get; set; }

        protected bool Equals(ContentVariableSegment other)
        {
            return string.Equals(Key, other.Key);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((ContentVariableSegment) obj);
        }

        public override int GetHashCode()
        {
            return Key.GetHashCode();
        }
    }
}