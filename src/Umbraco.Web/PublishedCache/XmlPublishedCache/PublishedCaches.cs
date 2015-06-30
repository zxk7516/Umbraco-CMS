namespace Umbraco.Web.PublishedCache.XmlPublishedCache
{
    /// <summary>
    /// Implements a facade.
    /// </summary>
    class PublishedCaches : IPublishedCaches
    {
        private readonly PublishedContentCache _contentCache;
        private readonly PublishedMediaCache _mediaCache;
        private readonly PublishedMemberCache _memberCache;
        private readonly DomainCache _domainCache;

        /// <summary>
        /// Initializes a new instance of the <see cref="PublishedCaches"/> class with a content cache
        /// and a media cache.
        /// </summary>
        public PublishedCaches(
            PublishedContentCache contentCache, 
            PublishedMediaCache mediaCache, 
            PublishedMemberCache memberCache,
            DomainCache domainCache)
        {
            _contentCache = contentCache;
            _mediaCache = mediaCache;
            _memberCache = memberCache;
            _domainCache = domainCache;
        }

        /// <summary>
        /// Gets the <see cref="IPublishedContentCache"/>.
        /// </summary>
        public IPublishedContentCache ContentCache
        {
            get { return _contentCache; }
        }

        /// <summary>
        /// Gets the <see cref="IPublishedMediaCache"/>.
        /// </summary>
        public IPublishedMediaCache MediaCache
        {
            get { return _mediaCache; }
        }

        /// <summary>
        /// Gets the <see cref="IPublishedMemberCache"/>.
        /// </summary>
        public IPublishedMemberCache MemberCache
        {
            get { return _memberCache; }
        }

        /// <summary>
        /// Gets the <see cref="IDomainCache"/>.
        /// </summary>
        public IDomainCache DomainCache
        {
            get { return _domainCache; }
        }

        public static void ResyncCurrent()
        {
            if (PublishedCachesServiceResolver.HasCurrent == false) return;
            var service = PublishedCachesServiceResolver.Current.Service as PublishedCachesService;
            if (service == null) return;
            var facade = service.GetPublishedCaches() as PublishedCaches;
            if (facade == null) return;
            facade._contentCache.Resync();
            facade._mediaCache.Resync();

            // not trying to resync members or domains, which are not cached really
        }
    }
}
