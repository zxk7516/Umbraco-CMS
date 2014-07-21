using System;
using System.Xml;
using System.Xml.Serialization;
using System.Xml.XPath;
using Umbraco.Core.Configuration;
using Umbraco.Web.Templates;
using umbraco.interfaces;

namespace umbraco.NodeFactory
{
	[Serializable]
	[XmlType(Namespace = "http://umbraco.org/webservices/")]
	public class Property : IProperty
	{
		private Guid _version;
		private string _alias;
		private string _value;

		public string Alias
		{
			get { return _alias; }
		}

		private string _parsedValue;
		public string Value
		{
            get { return _parsedValue ?? (_parsedValue = TemplateUtilities.ParseInternalLinks(TemplateUtilities.ResolveUrlsFromTextString(_value))); }
		}

		public Guid Version
		{
			get { return _version; }
		}

		public Property()
		{

		}

		public override string ToString()
		{
			return Value;
		}

		public Property(XmlNode PropertyXmlData)
		{
			if (PropertyXmlData != null)
			{
				// For backward compatibility with 2.x (the version attribute has been removed from 3.0 data nodes)
				if (PropertyXmlData.Attributes.GetNamedItem("versionID") != null)
					_version = new Guid(PropertyXmlData.Attributes.GetNamedItem("versionID").Value);
				_alias = UmbracoConfig.For.UmbracoSettings().Content.UseLegacyXmlSchema ?
				                                            	PropertyXmlData.Attributes.GetNamedItem("alias").Value :
				                                            	                                                       	PropertyXmlData.Name;
				_value = xmlHelper.GetNodeValue(PropertyXmlData);
			}
			else
				throw new ArgumentNullException("Property xml source is null");
		}

        // fixme - duplicated from Node
        private static bool ReadAttribute(XPathNavigator nav, string name, Action<XPathNavigator> action)
        {
            if (nav.MoveToAttribute(name, "") == false)
                return false;

            action(nav);
            nav.MoveToParent();
            return true;
        }
        
        public Property(XPathNavigator nav)
	    {
            if (nav == null)
                throw new ArgumentNullException("nav");

            ReadAttribute(nav, "versionID", n => _version = new Guid(n.Value));
            _alias = UmbracoConfig.For.UmbracoSettings().Content.UseLegacyXmlSchema ? nav.GetAttribute("alias", "") : nav.LocalName;

            var value = string.Empty;
            var c = nav.Clone();
            if (c.MoveToFirstChild())
            {
                // first child is text node or cdata node : get its value
                // no idea what we're doing with InnerXml here?!
                value = c.Value ?? nav.InnerXml;
            }

            _value = value.Replace("<!--CDATAOPENTAG-->", "<![CDATA[").Replace("<!--CDATACLOSETAG-->", "]]>");
        }
	}
}