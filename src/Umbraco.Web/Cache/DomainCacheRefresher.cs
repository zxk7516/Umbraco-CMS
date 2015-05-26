using System;
using Umbraco.Core;
using Umbraco.Core.Cache;
using Umbraco.Core.Persistence.Repositories;
using Umbraco.Web.PublishedCache;
using Umbraco.Web.PublishedCache.XmlPublishedCache;

namespace Umbraco.Web.Cache
{
    /// <summary>
    /// A cache refresher to ensure language cache is refreshed when languages change
    /// </summary>
    public sealed class DomainCacheRefresher : CacheRefresherBase<DomainCacheRefresher>
    {
        protected override DomainCacheRefresher Instance
        {
            get { return this; }
        }

        public override Guid UniqueIdentifier
        {
            get { return new Guid(DistributedCache.DomainCacheRefresherId); }
        }

        public override string Name
        {
            get { return "Domain cache refresher"; }
        }

        public override void Refresh(int id)
        {
            ClearCache();
            // notify
            var svce = PublishedCachesServiceResolver.Current.Service;
            svce.NotifyDomain(id, false);
            // then trigger event
            base.Refresh(id);
        }

        public override void Remove(int id)
        {
            ClearCache();
            // notify
            var svce = PublishedCachesServiceResolver.Current.Service;
            svce.NotifyDomain(id, true);
            // then trigger event
            base.Remove(id);
        }

        private void ClearCache()
        {
            ApplicationContext.Current.ApplicationCache.RuntimeCache.ClearCacheObjectTypes<DomainRepository.CacheableDomain>();
        }
    }
}