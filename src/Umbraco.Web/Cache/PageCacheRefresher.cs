using System;
using Umbraco.Core;
using Umbraco.Core.Cache;
using Umbraco.Core.Models;
using Umbraco.Core.Sync;
using umbraco;
using umbraco.cms.businesslogic.web;

namespace Umbraco.Web.Cache
{
    /// <summary>
    /// PageCacheRefresher is the standard CacheRefresher used by Load-Balancing in Umbraco.
    /// </summary>
    /// <remarks>
    /// If Load balancing is enabled (by default disabled, is set in umbracoSettings.config) PageCacheRefresher will be called
    /// everytime content is added/updated/removed to ensure that the content cache is identical on all load balanced servers
    /// </remarks>    
    public class PageCacheRefresher : TypedCacheRefresherBase<PageCacheRefresher, IContent>
    {
        protected override PageCacheRefresher Instance
        {
            get { return this; }
        }

        /// <summary>
        /// Gets the unique identifier of the CacheRefresher.
        /// </summary>
        /// <value>The unique identifier.</value>
        public override Guid UniqueIdentifier
        {
            get { return DistributedCache.PageCacheRefresherGuid; }
        }

        /// <summary>
        /// Gets the name of the CacheRefresher.
        /// </summary>
        /// <value>The name.</value>
        public override string Name
        {
            get { return "Page Refresher"; }
        }

        public override void RefreshAll()
        {
            base.RefreshAll();

            ApplicationContext.Current.ApplicationCache.ClearPartialViewCache();
            DistributedCache.Instance.ClearAllMacroCacheOnCurrentServer();
            DistributedCache.Instance.ClearXsltCacheOnCurrentServer();
        }

        public override void Refresh(int id)
        {
            base.Refresh(id);

            ApplicationContext.Current.ApplicationCache.ClearPartialViewCache();
            DistributedCache.Instance.ClearAllMacroCacheOnCurrentServer();
            DistributedCache.Instance.ClearXsltCacheOnCurrentServer();
        }

        public override void Remove(int id)
        {
            base.Remove(id);

            ApplicationContext.Current.ApplicationCache.ClearPartialViewCache();
            DistributedCache.Instance.ClearAllMacroCacheOnCurrentServer();
            DistributedCache.Instance.ClearXsltCacheOnCurrentServer();
            ApplicationContext.Current.ApplicationCache.RuntimeCache.ClearCacheObjectTypes<PublicAccessEntry>();
        }

        public override void Refresh(IContent instance)
        {
            base.Refresh(instance);

            ApplicationContext.Current.ApplicationCache.ClearPartialViewCache();
            DistributedCache.Instance.ClearAllMacroCacheOnCurrentServer();
            DistributedCache.Instance.ClearXsltCacheOnCurrentServer();
            ApplicationContext.Current.ApplicationCache.RuntimeCache.ClearCacheObjectTypes<PublicAccessEntry>();
        }

        public override void Remove(IContent instance)
        {
            base.Remove(instance);

            ApplicationContext.Current.ApplicationCache.ClearPartialViewCache();
            DistributedCache.Instance.ClearAllMacroCacheOnCurrentServer();
            DistributedCache.Instance.ClearXsltCacheOnCurrentServer();
            ApplicationContext.Current.ApplicationCache.RuntimeCache.ClearCacheObjectTypes<PublicAccessEntry>();
        }
    }
}
