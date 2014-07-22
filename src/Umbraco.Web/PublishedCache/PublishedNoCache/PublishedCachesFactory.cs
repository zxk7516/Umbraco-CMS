using System;
using System.Runtime.CompilerServices;
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

        public override IPublishedCaches CreatePublishedCaches(bool preview)
        {
            var contentCache = new PublishedContentCache(preview, _services.ContentService);
            var mediaCache = new PublishedMediaCache(preview, _services.MediaService);
            return new PublishedCaches(contentCache, mediaCache);
        }
    }
}
