using System;
using Umbraco.Core;
using Umbraco.Core.Cache;
using Umbraco.Core.Models.Membership;
using Umbraco.Core.Services;

namespace Umbraco.Web.PublishedCache.XmlPublishedCache
{
    class PublishedCachesService : PublishedCachesServiceBase
    {
        private readonly XmlStore _xmlStore;
        private readonly RoutesCache _routesCache;
        private readonly IDomainService _domainService;
        private readonly IMemberService _memberService;
        private readonly IMediaService _mediaService;
        private readonly ICacheProvider _requestCache;

        public PublishedCachesService(ServiceContext serviceContext, ICacheProvider requestCache)
        {
            _routesCache = new RoutesCache();
            _xmlStore = new XmlStore(serviceContext, _routesCache);
            _domainService = serviceContext.DomainService;
            _memberService = serviceContext.MemberService;
            _mediaService = serviceContext.MediaService;
            _requestCache = requestCache;
        }

        // for testing
        internal PublishedCachesService(ServiceContext serviceContext, ICacheProvider requestCache, bool withEvents)
        {
            _routesCache = new RoutesCache();
            _xmlStore = new XmlStore(serviceContext, _routesCache, withEvents);
            _domainService = serviceContext.DomainService;
            _memberService = serviceContext.MemberService;
            _mediaService = serviceContext.MediaService;
            _requestCache = requestCache;
        }

        public override IPublishedCaches CreatePublishedCaches(string previewToken)
        {
            // use _requestCache to store recursive properties lookup, etc. both in content
            // and media cache. Life span should be the current request. Or, ideally
            // the current caches, but that would mean creating an extra cache (StaticCache
            // probably) so better use RequestCache.

            return new PublishedCaches(
                new PublishedContentCache(_xmlStore, _domainService, _requestCache, _routesCache, previewToken),
                new PublishedMediaCache(_xmlStore, _mediaService, _requestCache), // fixme inject
                new PublishedMemberCache(_xmlStore, _requestCache, _memberService));
        }

        public override void Dispose()
        {
            _xmlStore.Dispose();
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
            var previewContent = new PreviewContent(_xmlStore, user.Id);
            previewContent.CreatePreviewSet(contentId, true); // preview branch below that content
            return previewContent.Token;
            //previewContent.ActivatePreviewCookie();
        }

        public override void RefreshPreview(string previewToken, int contentId)
        {
            if (previewToken.IsNullOrWhiteSpace()) return;
            var previewContent = new PreviewContent(_xmlStore, previewToken);
            previewContent.CreatePreviewSet(contentId, true); // preview branch below that content
        }

        public override void ExitPreview(string previewToken)
        {
            if (previewToken.IsNullOrWhiteSpace()) return;
            var previewContent = new PreviewContent(_xmlStore, previewToken);
            previewContent.ClearPreviewSet();
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
