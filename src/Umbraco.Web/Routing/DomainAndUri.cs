using System;
using System.Globalization;
using Umbraco.Core;

namespace Umbraco.Web.Routing
{
    /// <summary>
    /// Represents a facade domain with its normalized uri.
    /// </summary>
    /// <remarks>
    /// <para>In Umbraco it is valid to create domains with name such as <c>example.com</c>, <c>https://www.example.com</c>, <c>example.com/foo/</c>.</para>
    /// <para>The normalized uri of a domain begins with a scheme and ends with no slash, eg <c>http://example.com/</c>, <c>https://www.example.com/</c>, <c>http://example.com/foo/</c>.</para>
    /// </remarks>
    public class DomainAndUri : Domain
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DomainAndUri"/> class.
        /// </summary>
        /// <param name="domain">The original domain.</param>
        /// <param name="currentUri">The context current Uri.</param>
        public DomainAndUri(Domain domain, Uri currentUri)
            : base(domain)
        {
            try
            {
                // turn "/en" into "http://whatever.com/en" so it becomes a parseable uri
                var name = Name.StartsWith("/") && currentUri != null
                    ? currentUri.GetLeftPart(UriPartial.Authority) + Name
                    : Name;
                var scheme = currentUri == null ? Uri.UriSchemeHttp : currentUri.Scheme;
                Uri = new Uri(UriUtility.TrimPathEndSlash(UriUtility.StartWithScheme(name, scheme)));
            }
            catch (UriFormatException)
            {
                throw new ArgumentException(string.Format("Failed to parse invalid domain: node id={0}, hostname=\"{1}\"."
                    + " Hostname should be a valid uri.", domain.ContentId, Name.ToCSharpString()), "domain");
            }
        }

        /// <summary>
        /// Gets the normalized Uri of the domain, within the current context.
        /// </summary>
        public Uri Uri { get; private set; }

        public override string ToString()
        {
            return string.Format("{{ \"{0}\", \"{1}\" }}", Name, Uri);
        }
    }
}
