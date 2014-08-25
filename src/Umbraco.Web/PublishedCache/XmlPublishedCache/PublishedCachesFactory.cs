using System;
using Umbraco.Core.ObjectResolution;

namespace Umbraco.Web.PublishedCache.XmlPublishedCache
{
    class PublishedCachesFactory : PublishedCachesFactoryBase
    {
        private XmlStore _xmlStore;
        private PublishedMediaCache _mediaCache;
        private IPublishedCaches _caches;
        private IPublishedCaches _previewCaches;

        public PublishedCachesFactory()
        {
            // fixme - temp - we probably want to get it from?!
            _xmlStore = new XmlStore();
        }

        public override IPublishedCaches CreatePublishedCaches(bool preview)
        {
            // xml cache: no point creating plenty of cache objects
            // just need a preview and non-preview one, and share them

            // fixme - this is not thread-safe

            if (_mediaCache == null)
                _mediaCache = new PublishedMediaCache();

            // fixme - if we create two PublishedContentCache instance then
            // we need to get the GetXml etc stuff out of it...

            if (preview)
                return _previewCaches ?? (_previewCaches = new PublishedCaches(new PublishedContentCache(_xmlStore, true), _mediaCache));

            return _caches ?? (_caches = new PublishedCaches(new PublishedContentCache(_xmlStore, false), _mediaCache));
        }

        // fixme 
        public XmlStore XmlStore { get { return _xmlStore; }}
    }
}
