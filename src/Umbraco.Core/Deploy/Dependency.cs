using System;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;

namespace Umbraco.Core.Deploy
{
    /// <summary>
    /// Represents a dependency of a deployment item
    /// </summary>
    public class Dependency : IEquatable<Dependency>, IXmlSerializable
    {
        protected Dependency()
        {

        }

        public Dependency(string name, string id, Guid provider, bool isChild = false)
        {
            ItemId = new DeployKey(id, provider);
            Name = name;
            IsChild = isChild;
            ForceHashCheck = false;
        }

        public Dependency(string name, DeployKey id, bool isChild = false)
        {
            if (id == null) throw new ArgumentNullException("id");
            Name = name;
            IsChild = isChild;
            ItemId = id;
            ForceHashCheck = false;
        }

        public Dependency(string id, Guid provider, bool isChild = false)
        {
            ItemId = new DeployKey(id, provider);
            IsChild = isChild;
            ForceHashCheck = false;
        }

        public Dependency(DeployKey id, bool isChild = false)
        {
            if (id == null) throw new ArgumentNullException("id");
            IsChild = isChild;
            ItemId = id;
            ForceHashCheck = false;
        }


        //If loose that means the graph will not map it out
        //So the dependency will only exists if the property maps it the other way
        public bool IsChild { get; private set; }

        /// <summary>
        /// The dependency identifier
        /// </summary>
        public DeployKey ItemId { get; private set; }

        /// <summary>
        /// This is the description
        /// </summary>
        public string Name { get; private set; }

        /// <summary>
        /// This forces the deploy engine to determine if this dependency is needed based on presence or hash
        /// so if forceHashCheck is true, it will try to look up the current version and see if it is identical
        /// if turned off - it will simply just check if the dependency is present and not care about how it looks
        /// </summary>
        public bool ForceHashCheck { get; set; }

        /// <summary>
        /// Indicates whether the current object is equal to another object of the same type.
        /// </summary>
        /// <returns>
        /// true if the current object is equal to the <paramref name="other"/> parameter; otherwise, false.
        /// </returns>
        /// <param name="other">An object to compare with this object.</param>
        public bool Equals(Dependency other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return Equals(ItemId, other.ItemId);
        }

        /// <summary>
        /// Determines whether the specified <see cref="T:System.Object"/> is equal to the current <see cref="T:System.Object"/>.
        /// </summary>
        /// <returns>
        /// true if the specified object  is equal to the current object; otherwise, false.
        /// </returns>
        /// <param name="obj">The object to compare with the current object. </param>
        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((Dependency)obj);
        }

        /// <summary>
        /// Serves as a hash function for a particular type. 
        /// </summary>
        /// <returns>
        /// A hash code for the current <see cref="T:System.Object"/>.
        /// </returns>
        public override int GetHashCode()
        {
            return ItemId.GetHashCode();
        }

        public static bool operator ==(Dependency left, Dependency right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(Dependency left, Dependency right)
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

            IsChild = bool.Parse(reader.ReadElementString("IsChild"));

            var idserializer = new XmlSerializer(typeof(DeployKey));
            ItemId = (DeployKey)idserializer.Deserialize(reader);

            Name = reader.ReadElementString("Name");
            ForceHashCheck = bool.Parse(reader.ReadElementString("ForceHashCheck"));

            reader.ReadEndElement();
        }

        public void WriteXml(XmlWriter writer)
        {
            writer.WriteStartElement("IsChild");
            writer.WriteValue(IsChild.ToString().ToLower());
            writer.WriteEndElement();

            var idserializer = new XmlSerializer(typeof(DeployKey));
            idserializer.Serialize(writer, ItemId);

            writer.WriteStartElement("Name");
            writer.WriteValue(Name);
            writer.WriteEndElement();

            writer.WriteStartElement("ForceHashCheck");
            writer.WriteValue(ForceHashCheck.ToString().ToLower());
            writer.WriteEndElement();
        }
    }
}