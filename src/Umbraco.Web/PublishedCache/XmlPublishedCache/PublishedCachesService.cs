using System;
using System.Collections.Generic;
using System.Linq;
using Umbraco.Core;
using Umbraco.Core.Cache;
using Umbraco.Core.Models;
using Umbraco.Core.Models.Membership;
using Umbraco.Core.Persistence.Repositories;
using Umbraco.Core.Services;
using Umbraco.Core.Sync;
using Umbraco.Web.Cache;

namespace Umbraco.Web.PublishedCache.XmlPublishedCache
{
    class PublishedCachesService : PublishedCachesServiceBase
    {
        private readonly XmlStore _xmlStore;
        private readonly RoutesCache _routesCache;
        private readonly PublishedContentTypeCache _contentTypeCache;
        private readonly IDomainService _domainService;
        private readonly IMemberService _memberService;
        private readonly IMediaService _mediaService;
        private readonly ICacheProvider _requestCache;

        #region Constructors

        // FIXME must cleanup those constructors?!

        public PublishedCachesService(ServiceContext serviceContext, DatabaseContext databaseContext, ICacheProvider requestCache)
            : this(serviceContext, databaseContext, requestCache,
                new PublishedContentTypeCache(serviceContext.ContentTypeService, serviceContext.MediaTypeService, serviceContext.MemberTypeService), false, true)
        { }

        internal PublishedCachesService(ServiceContext serviceContext, DatabaseContext databaseContext,
            ICacheProvider requestCache,
            bool testing, bool enableRepositoryEvents)
            : this(serviceContext, databaseContext, requestCache,
                new PublishedContentTypeCache(serviceContext.ContentTypeService, serviceContext.MediaTypeService, serviceContext.MemberTypeService), testing, enableRepositoryEvents)
        { }

        // for testing
        internal PublishedCachesService(ServiceContext serviceContext, DatabaseContext databaseContext, ICacheProvider requestCache,
            PublishedContentTypeCache contentTypeCache,
            bool testing, bool enableRepositoryEvents)
        {
            _routesCache = new RoutesCache();
            _contentTypeCache = contentTypeCache;

            _xmlStore = new XmlStore(serviceContext, databaseContext, _routesCache, _contentTypeCache, testing, enableRepositoryEvents);

            _domainService = serviceContext.DomainService;
            _memberService = serviceContext.MemberService;
            _mediaService = serviceContext.MediaService;
            _requestCache = requestCache;
        }

        public override void Dispose()
        {
            _xmlStore.Dispose();
        }

        #endregion

        #region PublishedCachesService Caches

        public override IPublishedCaches CreatePublishedCaches(string previewToken)
        {
            // use _requestCache to store recursive properties lookup, etc. both in content
            // and media cache. Life span should be the current request. Or, ideally
            // the current caches, but that would mean creating an extra cache (StaticCache
            // probably) so better use RequestCache.

            return new PublishedCaches(
                new PublishedContentCache(_xmlStore, _domainService, _requestCache, _contentTypeCache, _routesCache, previewToken),
                new PublishedMediaCache(_xmlStore, _mediaService, _requestCache, _contentTypeCache),
                new PublishedMemberCache(_xmlStore, _requestCache, _memberService, _contentTypeCache));
        }

        #endregion

        #region PublishedCachesService Preview

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

        #endregion

        #region Xml specific

        /// <summary>
        /// Gets the underlying XML store.
        /// </summary>
        public XmlStore XmlStore { get { return _xmlStore; } }

        /// <summary>
        /// Gets the underlying RoutesCache.
        /// </summary>
        public RoutesCache RoutesCache { get { return _routesCache; } }

        public bool VerifyContentAndPreviewXml()
        {
            return XmlStore.VerifyContentAndPreviewXml();
        }

        // FIXME missing LOCKS here ** AND IN MANY SIMILAR PLACES ** WTF?
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

        #endregion

        #region Change management

        public override void Notify(ContentCacheRefresher.JsonPayload[] payloads, out bool draftChanged, out bool publishedChanged)
        {
            _xmlStore.Notify(payloads, out draftChanged, out publishedChanged);
        }

        public override void Notify(MediaCacheRefresher.JsonPayload[] payloads, out bool anythingChanged)
        {
            foreach (var payload in payloads)
                PublishedMediaCache.ClearCache(payload.Id);

            anythingChanged = true;
        }

        public override void Notify(ContentTypeCacheRefresher.JsonPayload[] payloads)
        {
            _xmlStore.Notify(payloads);
            if (payloads.Any(x => x.ItemType == typeof (IContentType).Name))
                _routesCache.Clear();
        }

        public override void Notify(DataTypeCacheRefresher.JsonPayload[] payloads)
        {
            _xmlStore.Notify(payloads);
        }

        public override void NotifyDomain(int id, bool removed)
        {
            _routesCache.Clear();
        }

        #endregion
    }
}
