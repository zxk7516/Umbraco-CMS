using System;
using Umbraco.Core.Cache;
using Umbraco.Core.ObjectResolution;

namespace Umbraco.Web.PublishedCache.NuCache
{
    // implements the facade
    class Facade : IPublishedCaches
    {
        #region Constructors

        public Facade(bool defaultPreview, IPublishedMemberCache memberCache,
            ContentView contentView, ContentView mediaView,
            ICacheProvider snapshotCache)
        {
            ContentCache = new ContentCache(defaultPreview, contentView);
            MediaCache = new MediaCache(defaultPreview, mediaView);
            MemberCache = memberCache;

            FacadeCache = new ObjectCacheRuntimeCacheProvider();
            SnapshotCache = snapshotCache;
        }

        #endregion

        #region Current - for tests

        private static Func<Facade> _getCurrentFacadeFunc = () =>
        {
#if DEBUG
            if (PublishedCachesServiceResolver.HasCurrent == false) return null;
            var service = PublishedCachesServiceResolver.Current.Service as FacadeService;
            if (service == null) return null;
            return (Facade) service.GetPublishedCachesFunc();
#endif
#if RELEASE
            return (Facade) ((FacadeService) PublishedCachesServiceResolver.Current.Service).GetPublishedCaches();
#endif
        };

        public static Func<Facade> GetCurrentFacadeFunc
        {
            get { return _getCurrentFacadeFunc; }
            set
            {
                using (Resolution.Configuration)
                {
                    _getCurrentFacadeFunc = value;
                }
            }
        }

        public static Facade Current
        {
            get { return _getCurrentFacadeFunc(); }
        }

        #endregion

        #region Caches

        public ICacheProvider FacadeCache { get; private set; }

        public ICacheProvider SnapshotCache { get; private set; }

        #endregion

        #region IFacade

        public IPublishedContentCache ContentCache { get; private set; }

        public IPublishedMediaCache MediaCache { get; private set; }

        public IPublishedMemberCache MemberCache { get; private set; }

        public void Resync()
        {
            // fixme - implement!
            throw new NotImplementedException();

            // we want
            // - new snapshots
            // - new caches
        }

        #endregion
    }
}
