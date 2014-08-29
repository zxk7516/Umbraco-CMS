using System;
using Umbraco.Core;
using Umbraco.Core.Models.Membership;

namespace Umbraco.Web.PublishedCache.XmlPublishedCache
{
    class PublishedCachesFactory : PublishedCachesFactoryBase
    {
        private readonly XmlStore _xmlStore;

        public PublishedCachesFactory()
        {
            // instanciate an XmlStore
            // we're not going to have an IXmlStore of some sort, so it's ok to do it here
            _xmlStore = new XmlStore();
        }

        public override IPublishedCaches CreatePublishedCaches(string previewToken)
        {
            // used to store recursive properties lookup, etc. both in content
            // and media cache. Life span should be the current request. Or, ideally
            // the current caches, but that would mean creating an extra cache (StaticCache
            // probably) so better use RequestCache.
            var cache = ApplicationContext.Current.ApplicationCache.RequestCache;

            return new PublishedCaches(
                new PublishedContentCache(_xmlStore, cache, previewToken),
                new PublishedMediaCache(cache));
        }

        /// <summary>
        /// Gets the underlying XML store.
        /// </summary>
        public XmlStore XmlStore { get { return _xmlStore; } }

        public override string EnterPreview(IUser user, int contentId)
        {
            var previewContent = new PreviewContent(user.Id, Guid.NewGuid() /*, false*/);
            previewContent.CreatePreviewSet(contentId, true); // preview branch below that content
            return previewContent.Token;
            //previewContent.ActivatePreviewCookie();
        }

        public override void RefreshPreview(string previewToken, int contentId)
        {
            if (previewToken.IsNullOrWhiteSpace()) return;
            var previewContent = new PreviewContent(previewToken);
            previewContent.CreatePreviewSet(contentId, true); // preview branch below that content
        }

        public override void ExitPreview(string previewToken)
        {
            if (previewToken.IsNullOrWhiteSpace()) return;
            var previewContent = new PreviewContent(previewToken);
            previewContent.ClearPreviewSet();
        }
    }
}
