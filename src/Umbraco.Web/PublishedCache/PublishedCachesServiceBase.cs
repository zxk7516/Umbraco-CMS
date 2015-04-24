using System;
using System.Collections.Generic;
using Umbraco.Core.ObjectResolution;
using Umbraco.Core.Models.Membership;
using Umbraco.Web.Cache;

namespace Umbraco.Web.PublishedCache
{
    abstract class PublishedCachesServiceBase : IPublishedCachesService
    {
        private Func<IPublishedCaches> _getPublishedCachesFunc = () => UmbracoContext.Current == null ? null : UmbracoContext.Current.PublishedCaches;

        public Func<IPublishedCaches> GetPublishedCachesFunc
        {
            get { return _getPublishedCachesFunc; }
            set
            {
                using (Resolution.Configuration)
                {
                    _getPublishedCachesFunc = value;
                }
            }
        }

        public abstract IPublishedCaches CreatePublishedCaches(string previewToken);

        public IPublishedCaches GetPublishedCaches()
        {
            var caches = _getPublishedCachesFunc();
            if (caches == null)
                throw new Exception("Carrier's caches is null.");
            return caches;
        }

        public abstract string EnterPreview(IUser user, int contentId);
        public abstract void RefreshPreview(string previewToken, int contentId);
        public abstract void ExitPreview(string previewToken);
        public abstract void Notify(ContentCacheRefresher.JsonPayload[] payloads, out bool draftChanged, out bool publishedChanged);
        public abstract void Notify(MediaCacheRefresher.JsonPayload[] payloads, out bool anythingChanged);
        public abstract void Notify(ContentTypeCacheRefresher.JsonPayload[] payloads);
        public abstract void Notify(DataTypeCacheRefresher.JsonPayload[] payloads);

        public virtual void Dispose()
        { }
    }
}
