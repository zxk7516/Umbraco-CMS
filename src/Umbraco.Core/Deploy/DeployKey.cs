using System;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;

namespace Umbraco.Core.Deploy
{
    /// <summary>
    /// The identifier for an item used for deployment
    /// </summary>
    public class DeployKey : IComparable<DeployKey>, IEquatable<DeployKey>, IXmlSerializable
    {
        public static DeployKey Parse(string uniqueKey)
        {
            return new DeployKey(uniqueKey);
        }


        public DeployKey(string id, Guid providerId)
        {
            this.Id = id;
            this.ProviderId = providerId;
        }

        /// <summary>
        /// Used for the 'Parse' method
        /// </summary>
        /// <param name="uniqueKey"></param>
        private DeployKey(string uniqueKey)
        {
            if (!uniqueKey.Contains("_"))
            {
                throw new InvalidOperationException("The format of the uniqueKey is invalid and must contain an underscore");
            }

            string id = uniqueKey.Substring(0, uniqueKey.LastIndexOf('_')).TrimEnd('_');
            string guid = uniqueKey.Substring(uniqueKey.LastIndexOf('_')).Trim('_');

            Id = id;

            Guid parsed;
            if (Guid.TryParse(guid, out parsed))
            {
                ProviderId = parsed;
            }
            else
            {
                throw new InvalidOperationException("The format of the uniqueKey is invalid, the 2nd part of the uniqueKey must parse into a valid GUID");
            }
        }

        protected DeployKey() { }


        public string Id { get; internal set; }

        public Guid ProviderId { get; internal set; }
        
        public int CompareTo(DeployKey other)
        {
            return String.Compare(ToString(), other.ToString(), StringComparison.Ordinal);
        }

        public override string ToString()
        {
            return Id + "_" + ProviderId;
        }

        public bool Equals(DeployKey other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return string.Equals(Id, other.Id) && ProviderId.Equals(other.ProviderId);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((DeployKey)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((Id != null ? Id.GetHashCode() : 0) * 397) ^ ProviderId.GetHashCode();
            }
        }

        public static bool operator ==(DeployKey left, DeployKey right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(DeployKey left, DeployKey right)
        {
            return !Equals(left, right);
        }

        public XmlSchema GetSchema()
        {
            return null;
        }

        public void ReadXml(XmlReader reader)
        {
            reader.MoveToContent();

            var wasEmpty = reader.IsEmptyElement;
            reader.ReadStartElement();

            if (wasEmpty)
                return;

            Id = reader.ReadElementString("Id");
            ProviderId = Guid.Parse(reader.ReadElementString("ProviderId"));
            reader.ReadEndElement();

        }

        public void WriteXml(XmlWriter writer)
        {
            writer.WriteStartElement("Id");
            writer.WriteValue(Id);
            writer.WriteEndElement();
            writer.WriteStartElement("ProviderId");
            writer.WriteValue(ProviderId.ToString());
            writer.WriteEndElement();
        }
    }
}