using System;
using System.Runtime.Serialization;
using Newtonsoft.Json;

namespace Umbraco.Web.Routing.Segments
{
    public class Segment
    {
        /// <summary>
        /// Constructor, required for deserialization
        /// </summary>
        public Segment()
        {
        }

        public Segment(string name, object value)
        {
            Name = name;
            Value = value;
        }

        internal Segment(string name, object value, bool persist)
        {
            Name = name;
            Value = value;
            Persist = persist;
        }

        internal Segment(string name, object value, bool persist, int? slidingDays)
        {
            Name = name;
            Value = value;
            Persist = persist;
            SlidingDays = slidingDays;
        }

        internal Segment(string name, object value, bool persist, DateTime? absoluteExpiry)
        {
            Name = name;
            Value = value;
            Persist = persist;
            AbsoluteExpiry = absoluteExpiry;
        }

        /// <summary>
        /// The name of the segment
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// The value of the segment
        /// </summary>
        public object Value { get; set; }

        /// <summary>
        /// Whether or not this segment is to be persisted (default is false)
        /// </summary>
        [JsonIgnoreAttribute]
        [IgnoreDataMember]
        internal bool Persist { get; set; }

        //TODO: We should make use of these expiry settings!

        /// <summary>
        /// Defines the sliding expiration date in days from now for this particular segment
        /// </summary>
        [JsonIgnoreAttribute]
        [IgnoreDataMember]
        internal int? SlidingDays { get; set; }

        /// <summary>
        /// Defines the absolute expiration date for this particular segment
        /// </summary>
        [JsonIgnoreAttribute]
        [IgnoreDataMember]
        internal DateTime? AbsoluteExpiry { get; set; }

        protected bool Equals(Segment other)
        {
            return string.Equals(Name, other.Name);
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
            return Name.GetHashCode();
        }
    }
}