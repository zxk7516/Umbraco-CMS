using System;
using Umbraco.Core;
using Umbraco.Core.Models.Membership;

namespace Umbraco.Web.PublishedCache.XmlPublishedCache
{
    class PublishedCachesService : PublishedCachesServiceBase
    {
        private readonly XmlStore _xmlStore;
        private readonly RoutesCache _routesCache;

        public PublishedCachesService(XmlStore xmlStore, RoutesCache routesCache)
        {
            _xmlStore = xmlStore;
            _routesCache = routesCache;
        }

        public override IPublishedCaches CreatePublishedCaches(string previewToken)
        {
            // used to store recursive properties lookup, etc. both in content
            // and media cache. Life span should be the current request. Or, ideally
            // the current caches, but that would mean creating an extra cache (StaticCache
            // probably) so better use RequestCache.
            var cache = ApplicationContext.Current.ApplicationCache.RequestCache;

            return new PublishedCaches(
                new PublishedContentCache(_xmlStore, cache, _routesCache, previewToken),
                new PublishedMediaCache(ApplicationContext.Current, cache));
        }

        /// <summary>
        /// Gets the underlying XML store.
        /// </summary>
        public XmlStore XmlStore { get { return _xmlStore; } }

        /// <summary>
        /// Gets the underlying RoutesCache.
        /// </summary>
        public RoutesCache RoutesCache { get { return _routesCache; } }

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

        public override void Flush()
        {
            XmlStore.Flush();
            base.Flush();
        }

        public bool VerifyContentAndPreviewXml()
        {
            return XmlStore.VerifyContentAndPreviewXml();
        }

        public void RebuildContentAndPreviewXml()
        {
            XmlStore.RebuildContentAndPreviewXml();
        }

        public bool VerifyMediaXml()
        {
            return XmlStore.VerifyMediaXml();
        }

        public void RebuildMediaXml()
        {
            XmlStore.RebuildMediaXml();
        }

        public bool VerifyMemberXml()
        {
            return XmlStore.VerifyMemberXml();
        }

        public void RebuildMemberXml()
        {
            XmlStore.RebuildMemberXml();
        }
    }
}
