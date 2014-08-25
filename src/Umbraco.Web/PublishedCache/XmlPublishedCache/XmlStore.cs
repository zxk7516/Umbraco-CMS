using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using umbraco;
using umbraco.BusinessLogic;
using umbraco.presentation.preview;

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

        static readonly ConditionalWeakTable<UmbracoContext, PreviewContent> PreviewContentCache
            = new ConditionalWeakTable<UmbracoContext, PreviewContent>();

        private Func<bool, XmlDocument> _xmlDelegate;

        /// <summary>
        /// Gets/sets the delegate used to retrieve the Xml content, generally the setter is only used for unit tests
        /// and by default if it is not set will use the standard delegate which ONLY works when in the context an Http Request
        /// </summary>
        /// <remarks>
        /// If not defined, we will use the standard delegate which ONLY works when in the context an Http Request
        /// mostly because the 'content' object heavily relies on HttpContext, SQL connections and a bunch of other stuff
        /// that when run inside of a unit test fails.
        /// </remarks>
        internal Func<bool, XmlDocument> GetXmlDelegate
        {
            get
            {
                return _xmlDelegate ?? (_xmlDelegate = preview =>
                {
                    if (preview)
                    {
                        if (UmbracoContext.Current == null)
                            throw new InvalidOperationException("UmbracoContext.Current is null.");
                        var previewContent = PreviewContentCache.GetOrCreateValue(UmbracoContext.Current); // will use the ctor with no parameters
                        previewContent.EnsureInitialized(UmbracoContext.Current.UmbracoUser, StateHelper.Cookies.Preview.GetValue(), true, () =>
                        {
                            if (previewContent.ValidPreviewSet)
                                previewContent.LoadPreviewset();
                        });
                        if (previewContent.ValidPreviewSet)
                            return previewContent.XmlContent;
                    }
                    return content.Instance.XmlContent;
                });
            }
            set
            {
                // note: bad idea to do this while running
                // but it's internal anyway so probably safe
                _xmlDelegate = value;
            }
        }

        internal XmlDocument GetXml(bool preview)
        {
            return GetXmlDelegate(preview);
        }

        #endregion
    }
}
