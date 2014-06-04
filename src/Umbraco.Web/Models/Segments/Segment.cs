using System;
using System.Runtime.Serialization;
using Newtonsoft.Json;

namespace Umbraco.Web.Models.Segments
{
    /// <summary>
    /// Defines an assigned segment in a request
    /// </summary>
    /// <remarks>
    /// The serialization names are only one letter - this is intentional to keep the cookie size small
    /// </remarks>
    [DataContract(Name = "s", Namespace = "")]
    public class Segment
    {
        /// <summary>
        /// Constructor, required for deserialization
        /// </summary>
        public Segment()
        {
        }

        public Segment(string key, object value)
        {
            //ProviderTypeHashCode = providerType.GetHashCode();
            Key = key;
            Value = value;
        }

        public Segment(string key, object value, bool persist)
        {
            //ProviderTypeHashCode = providerType.GetHashCode();
            Key = key;
            Value = value;
            Persist = persist;
        }

        internal Segment(string key, object value, bool persist, int? slidingDays)
        {
            //ProviderTypeHashCode = providerType.GetHashCode();
            Key = key;
            Value = value;
            Persist = persist;
            SlidingDays = slidingDays;
        }

        internal Segment(string key, object value, bool persist, DateTime? absoluteExpiry)
        {
            //ProviderTypeHashCode = providerType.GetHashCode();
            Key = key;
            Value = value;
            Persist = persist;
            AbsoluteExpiry = absoluteExpiry;
        }

        ///// <summary>
        ///// The md5 of the type of provider that created this segment
        ///// </summary>
        ///// <remarks>
        ///// it will take up 
        ///// too much space. 
        ///// </remarks>
        //[DataMember(Name = "h", IsRequired = true)]
        //public string ProviderTypeMd5Hash { get; set; }

        /// <summary>
        /// The name of the segment
        /// </summary>
        [DataMember(Name = "k", IsRequired = true)]
        public string Key { get; set; }

        /// <summary>
        /// The value of the segment
        /// </summary>
        [DataMember(Name = "v")]
        public object Value { get; set; }

        /// <summary>
        /// Whether or not this segment is to be persisted (default is false)
        /// </summary>
        [DataMember(Name = "p")]
        public bool Persist { get; set; }

        //TODO: We should make use of these expiry settings!

        /// <summary>
        /// Defines the sliding expiration date in days from now for this particular segment
        /// </summary>
        [JsonIgnore]
        [IgnoreDataMember]
        internal int? SlidingDays { get; private set; }

        /// <summary>
        /// Defines the absolute expiration date for this particular segment
        /// </summary>
        [JsonIgnore]
        [IgnoreDataMember]
        internal DateTime? AbsoluteExpiry { get; private set; }

        protected bool Equals(Segment other)
        {
            return string.Equals(Key, other.Key);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((Segment) obj);
        }

        public override int GetHashCode()
        {
            return Key.GetHashCode();
        }
    }
}