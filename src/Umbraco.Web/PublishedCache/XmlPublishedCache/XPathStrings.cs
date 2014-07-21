using System;
using Umbraco.Core.Configuration;

namespace Umbraco.Web.PublishedCache.XmlPublishedCache
{
    class XPathStrings
    {
        private static XPathStringDefinitions _definitions;

		public static string Root { get { return "/root"; } }
		public static string RootDocuments { get { return Definitions.RootDocuments; } }
		public static string DescendantDocumentById { get { return Definitions.DescendantDocumentById; } }
        public static string ChildDocumentByUrlName { get { return Definitions.ChildDocumentByUrlName; } }
        public static string ChildDocumentByUrlNameVar { get { return Definitions.ChildDocumentByUrlNameVar; } }
        public static string RootDocumentWithLowestSortOrder { get { return Definitions.RootDocumentWithLowestSortOrder; } }

        private static XPathStringDefinitions Definitions
        {
            get
            {
				// in theory XPathStrings should be a static variable that
				// we should initialize in a static ctor - but then test cases
				// that switch schemas fail - so cache and refresh when needed,
				// ie never when running the actual site

				var version = UmbracoConfig.For.UmbracoSettings().Content.UseLegacyXmlSchema ? 0 : 1;
                if (_definitions == null || _definitions.Version != version)
                    _definitions = new XPathStringDefinitions(version);
                return _definitions;
            }
        }

        class XPathStringDefinitions
        {
            public int Version { get; private set; }

            // ReSharper disable MemberHidesStaticFromOuterClass
            public string RootDocuments { get; private set; }
            public string DescendantDocumentById { get; private set; }
            public string ChildDocumentByUrlName { get; private set; }
            public string ChildDocumentByUrlNameVar { get; private set; }
            public string RootDocumentWithLowestSortOrder { get; private set; }
            // ReSharper restore MemberHidesStaticFromOuterClass

            public XPathStringDefinitions(int version)
		    {
			    Version = version;

			    switch (version)
			    {
				    // legacy XML schema
				    case 0:
					    RootDocuments = "/root/node";
					    DescendantDocumentById = "//node [@id={0}]";
					    ChildDocumentByUrlName = "/node [@urlName='{0}']";
					    ChildDocumentByUrlNameVar = "/node [@urlName=${0}]";
					    RootDocumentWithLowestSortOrder = "/root/node [not(@sortOrder > ../node/@sortOrder)][1]";
					    break;

				    // default XML schema as of 4.10
				    case 1:
					    RootDocuments = "/root/* [@isDoc]";
					    DescendantDocumentById = "//* [@isDoc and @id={0}]";
					    ChildDocumentByUrlName = "/* [@isDoc and @urlName='{0}']";
					    ChildDocumentByUrlNameVar = "/* [@isDoc and @urlName=${0}]";
					    RootDocumentWithLowestSortOrder = "/root/* [@isDoc and not(@sortOrder > ../* [@isDoc]/@sortOrder)][1]";
					    break;

				    default:
					    throw new Exception(string.Format("Unsupported Xml schema version '{0}').", version));
			    }
    		}
        }
    }
}
