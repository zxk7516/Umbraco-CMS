using System;
using Umbraco.Core.Cache;
using Umbraco.Core.ObjectResolution;

namespace Umbraco.Web.PublishedCache.NuCache
{
    // implements the facade
    class Facade : IPublishedCaches
    {
        private readonly FacadeService _service;
        private readonly bool _defaultPreview;
        private FacadeElements _elements;

        #region Constructors

        public Facade(FacadeService service, bool defaultPreview)
        {
            _service = service;
            _defaultPreview = defaultPreview;
            FacadeCache = new ObjectCacheRuntimeCacheProvider();
        }

        public class FacadeElements
        {
            public ContentCache ContentCache;
            public MediaCache MediaCache;
            public MemberCache MemberCache;
            public ICacheProvider FacadeCache;
            public ICacheProvider SnapshotCache;
        }

        private FacadeElements Elements
        {
            get
            {
                // no lock - facades are single-thread
                return _elements ?? (_elements = _service.GetElements(_defaultPreview));
            }
        }

        public void Resync()
        {
            // no lock - facades are single-thread
            _elements = null;
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
#else
            return (Facade)((FacadeService)PublishedCachesServiceResolver.Current.Service).GetPublishedCaches();
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
            get { return GetCurrentFacadeFunc(); }
        }

        #endregion

        #region Caches

        public ICacheProvider FacadeCache { get; private set; }

        public ICacheProvider SnapshotCache { get { return Elements.SnapshotCache; } }

        #endregion

        #region IFacade

        public IPublishedContentCache ContentCache { get { return Elements.ContentCache; } }

        public IPublishedMediaCache MediaCache { get { return Elements.MediaCache; } }

        public IPublishedMemberCache MemberCache { get { return Elements.MemberCache; } }

        #endregion
    }
}
