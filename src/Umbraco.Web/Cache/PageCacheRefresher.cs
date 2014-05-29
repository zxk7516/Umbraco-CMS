using System;
using System.Web.Script.Serialization;
using Umbraco.Core;
using Umbraco.Core.Cache;
using Umbraco.Core.Models;
using Umbraco.Core.Sync;
using umbraco;
using umbraco.cms.businesslogic.web;
using System.Linq;

namespace Umbraco.Web.Cache
{
    /// <summary>
    /// PageCacheRefresher is the standard CacheRefresher used by Load-Balancing in Umbraco.
    /// </summary>
    /// <remarks>
    /// If Load balancing is enabled (by default disabled, is set in umbracoSettings.config) PageCacheRefresher will be called
    /// everytime content is added/updated/removed to ensure that the content cache is identical on all load balanced servers
    /// </remarks>    
    public class PageCacheRefresher : JsonCacheRefresherBase<PageCacheRefresher>
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
            get
            {
                return new Guid(DistributedCache.PageCacheRefresherId);
            }
        }

        /// <summary>
        /// Gets the name of the CacheRefresher
        /// </summary>
        /// <value>The name.</value>
        public override string Name
        {
            get { return "Page Refresher"; }
        }

        #region Static helpers

        /// <summary>
        /// Converts the json to a JsonPayload object
        /// </summary>
        /// <param name="json"></param>
        /// <returns></returns>
        internal static JsonPayload[] DeserializeFromJsonPayload(string json)
        {
            var serializer = new JavaScriptSerializer();
            var jsonObject = serializer.Deserialize<JsonPayload[]>(json);
            return jsonObject;
        }


        internal static string SerializeToJsonPayload(OperationType op, params int[] contentIds)
        {
            var serializer = new JavaScriptSerializer();
            var items = contentIds.Select(x => new JsonPayload
            {
                Id = x,
                Operation = op
            }).ToArray();
            var json = serializer.Serialize(items);
            return json;
        }

        #endregion

        #region Sub classes

        internal enum OperationType
        {
            Deleted,
            Saved
        }

        internal class JsonPayload
        {
            public int Id { get; set; }
            public OperationType Operation { get; set; }
        }

        #endregion

        /// <summary>
        /// Refreshes all nodes in umbraco.
        /// </summary>
        public override void RefreshAll()
        {
            content.Instance.RefreshContentFromDatabaseAsync();
            base.RefreshAll();
        }

        /// <summary>
        /// Refreshes the cache for the node with specified id
        /// </summary>
        /// <param name="id">The id.</param>
        public override void Refresh(int id)
        {
            ApplicationContext.Current.ApplicationCache.ClearPartialViewCache();
            content.Instance.UpdateDocumentCache(id);
            DistributedCache.Instance.ClearAllMacroCacheOnCurrentServer();
            DistributedCache.Instance.ClearXsltCacheOnCurrentServer();
            base.Refresh(id);
        }

        /// <summary>
        /// Removes the node with the specified id from the cache
        /// </summary>
        /// <param name="id">The id.</param>
        public override void Remove(int id)
        {
            ApplicationContext.Current.ApplicationCache.ClearPartialViewCache();
            content.Instance.ClearDocumentCache(id);
            DistributedCache.Instance.ClearAllMacroCacheOnCurrentServer();
            DistributedCache.Instance.ClearXsltCacheOnCurrentServer();
            base.Remove(id);
        }

        /// <summary>
        /// Implement the IJsonCacheRefresher so that we can bulk delete/refresh the cache based on multiple IDs
        /// </summary>
        /// <param name="jsonPayload"></param>
        public override void Refresh(string jsonPayload)
        {
            var payloads = DeserializeFromJsonPayload(jsonPayload);

            if (payloads.Any() == false) return;

            ApplicationContext.Current.ApplicationCache.ClearPartialViewCache();
            DistributedCache.Instance.ClearAllMacroCacheOnCurrentServer();
            DistributedCache.Instance.ClearXsltCacheOnCurrentServer();

            foreach (var payload in payloads)
            {
                switch (payload.Operation)
                {
                    case OperationType.Deleted:
                        content.Instance.ClearDocumentCache(payload.Id);
                        break;
                    case OperationType.Saved:
                        content.Instance.UpdateDocumentCache(payload.Id);
                        break;                    
                }
            }

            base.Refresh(jsonPayload);
        }
    }
}
