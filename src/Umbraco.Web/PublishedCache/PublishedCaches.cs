namespace Umbraco.Web.PublishedCache
{
    /// <summary>
    /// Provides caches (content and media).
    /// </summary>
    /// <remarks>Default implementation for unrelated caches.</remarks>
    class PublishedCaches : IPublishedCaches
    {
        private readonly IPublishedContentCache _contentCache;
        private readonly IPublishedMediaCache _mediaCache;

        /// <summary>
        /// Initializes a new instance of the <see cref="PublishedCaches"/> class with a content cache
        /// and a media cache.
        /// </summary>
        public PublishedCaches(IPublishedContentCache contentCache, IPublishedMediaCache mediaCache)
        {
            _contentCache = contentCache;
            _mediaCache = mediaCache;
        }

        // fixme - document
        public IPublishedContentCache ContentCache
        {
            get { return _contentCache; }
        }

        // fixme - document
        public IPublishedMediaCache MediaCache
        {
            get { return _mediaCache; }
        }

        // fixme - implement
        public void Resync()
        { }
    }
}
