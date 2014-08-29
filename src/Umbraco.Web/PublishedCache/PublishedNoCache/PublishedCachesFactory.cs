using System;
using System.Runtime.CompilerServices;
using Umbraco.Core;
using Umbraco.Core.Models.Membership;
using Umbraco.Core.ObjectResolution;
using Umbraco.Core.Services;

namespace Umbraco.Web.PublishedCache.PublishedNoCache
{
    class PublishedCachesFactory : PublishedCachesFactoryBase
    {
        private ServiceContext _services;

        public PublishedCachesFactory(Func<ServiceContext> services)
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
            var contentCache = new PublishedContentCache(previewToken, _services.ContentService);
            var mediaCache = new PublishedMediaCache(preview, _services.MediaService);
            return new PublishedCaches(contentCache, mediaCache);
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
    }
}
