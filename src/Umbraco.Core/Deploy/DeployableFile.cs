using System;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;

namespace Umbraco.Core.Deploy
{
    public class DeployableFile : IEquatable<DeployableFile>, IComparable<DeployableFile>, IXmlSerializable
    {
        protected DeployableFile()
        {

        }

        public DeployableFile(string name, string path, string hash)
        {
            if (name == null) throw new ArgumentNullException("name");
            if (path == null) throw new ArgumentNullException("path");
            if (hash == null) throw new ArgumentNullException("hash");

            Name = name;
            Path = path;
            Hash = hash;
        }

        public string Name { get; private set; }
        public string Path { get; private set; }
        public string Hash { get; private set; }

        /// <summary>
        /// Indicates whether the current object is equal to another object of the same type.
        /// </summary>
        /// <returns>
        /// true if the current object is equal to the <paramref name="other"/> parameter; otherwise, false.
        /// </returns>
        /// <param name="other">An object to compare with this object.</param>
        public bool Equals(DeployableFile other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return string.Equals(Hash, other.Hash);
        }

        /// <summary>
        /// Compares the current object with another object of the same type.
        /// </summary>
        /// <returns>
        /// A value that indicates the relative order of the objects being compared. The return value has the following meanings: Value Meaning Less than zero This object is less than the <paramref name="other"/> parameter.Zero This object is equal to <paramref name="other"/>. Greater than zero This object is greater than <paramref name="other"/>. 
        /// </returns>
        /// <param name="other">An object to compare with this object.</param>
        public int CompareTo(DeployableFile other)
        {
            return String.Compare(Hash, other.Hash, StringComparison.Ordinal);
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
            return Equals((DeployableFile)obj);
        }

        /// <summary>
        /// Serves as a hash function for a particular type. 
        /// </summary>
        /// <returns>
        /// A hash code for the current <see cref="T:System.Object"/>.
        /// </returns>
        public override int GetHashCode()
        {
            return Hash.GetHashCode();
        }

        public static bool operator ==(DeployableFile left, DeployableFile right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(DeployableFile left, DeployableFile right)
        {
            return !Equals(left, right);
        }

        public XmlSchema GetSchema()
        {
            return null;
        }

        public void ReadXml(XmlReader reader)
        {
            throw new NotImplementedException();
        }

        public void WriteXml(XmlWriter writer)
        {
            writer.WriteStartElement("Name");
            writer.WriteValue(Name);
            writer.WriteEndElement();
            writer.WriteStartElement("Path");
            writer.WriteValue(Path);
            writer.WriteEndElement();
            writer.WriteStartElement("Hash");
            writer.WriteValue(Hash);
            writer.WriteEndElement();
        }
    }
}