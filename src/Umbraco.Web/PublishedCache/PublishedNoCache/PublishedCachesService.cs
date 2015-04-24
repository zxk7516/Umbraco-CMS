using System;
using System.Collections.Generic;
using Umbraco.Core;
using Umbraco.Core.Models.Membership;
using Umbraco.Core.ObjectResolution;
using Umbraco.Core.Services;
using Umbraco.Web.Cache;

namespace Umbraco.Web.PublishedCache.PublishedNoCache
{
    class PublishedCachesService : PublishedCachesServiceBase
    {
        private ServiceContext _services;

        public PublishedCachesService(Func<ServiceContext> services)
        {
            // too soon to resolve the services in the ctor
            // do in when resolution freezes
            Resolution.Frozen += (sender, args) =>
            {
                _services = services();
            };
        }

        public override IPublishedCaches CreatePublishedCaches(string previewToken)
        {
            var preview = previewToken.IsNullOrWhiteSpace() == false;
            var contentCache = new PublishedContentCache(previewToken, _services.DomainService, _services.ContentService, _services.ContentTypeService);
            var mediaCache = new PublishedMediaCache(preview, _services.MediaService, _services.MediaTypeService);
            var memberCache = new PublishedMemberCache(_services.DataTypeService, _services.MemberService);
            return new PublishedCaches(contentCache, mediaCache, memberCache);
        }

        public override string EnterPreview(IUser user, int contentId)
        {
            return "preview"; // anything
        }

        public override void RefreshPreview(string previewToken, int contentId)
        {
            // nothing
        }

        public override void ExitPreview(string previewToken)
        {
            // nothing
        }

        public override void Notify(ContentCacheRefresher.JsonPayload[] payloads, out bool draftChanged, out bool publishedChanged)
        {
            draftChanged = publishedChanged = true;
        }

        public override void Notify(MediaCacheRefresher.JsonPayload[] payloads, out bool anythingChanged)
        {
            anythingChanged = true;
        }

        public override void Notify(ContentTypeCacheRefresher.JsonPayload[] payloads)
        {
            // nothing
        }

        public override void Notify(DataTypeCacheRefresher.JsonPayload[] payloads)
        {
            // nothing
        }
    }
}
