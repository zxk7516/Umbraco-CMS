using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using umbraco;
using umbraco.BusinessLogic;

namespace Umbraco.Web.PublishedCache.XmlPublishedCache
{
    // represents the XML storage for the XmlPublishedCache
    // should move to Umbraco.Web.PublishedCache.XmlPublishedCache.XmlCache
    // should take over all the mess from content.Instance
    // everything should happen here!

    // this class should be instanciated and kept by the PublishedCachesFactory
    // and passed to all PublishedContentCache that are created
    // for medias... it's f*cked anyway

    // idea is to offload everything that's global from PublishedContentCache
    // - XML
    // - what about routes cache?

    class XmlStore
    {
        #region Legacy Xml

        private XmlDocument _xmlDocument;

        public XmlStore()
        { }

        // internal for unit tests
        internal XmlStore(XmlDocument xmlDocument)
        {
            _xmlDocument = xmlDocument;
        }

        // internal for unit tests
        internal XmlStore(Func<XmlDocument> getXmlDocument)
        {
            GetXmlDocument = getXmlDocument;
        }

        /// <summary>
        /// Gets or sets the delegate used to retrieve the Xml content, used for unit tests, else should
        /// be null and then the default content will be used. For non-preview content only.
        /// </summary>
        /// <remarks>
        /// The default content ONLY works when in the context an Http Request mostly because the 
        /// 'content' object heavily relies on HttpContext, SQL connections and a bunch of other stuff
        /// that when run inside of a unit test fails.
        /// </remarks>
        internal Func<XmlDocument> GetXmlDocument { get; set; }

        // to be used by PublishedContentCache only
        // for non-preview content only
        internal XmlDocument GetXml()
        {
            if (_xmlDocument != null)
                return _xmlDocument;
            if (GetXmlDocument != null)
                return _xmlDocument = GetXmlDocument();
            return content.Instance.XmlContent;
        }

        #endregion
    }
}
