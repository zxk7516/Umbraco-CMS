using System;
using System.Web.Script.Serialization;
using Umbraco.Core.Cache;
using Umbraco.Core.Models;
using System.Linq;
using Umbraco.Core.Persistence.Caching;
using Umbraco.Core.Sync;

namespace Umbraco.Web.Cache
{
    /// <summary>
    /// A cache refresher used for non-published content, this is primarily to notify Examine indexes to update and to refresh the RuntimeCacheRefresher
    /// </summary>
    public sealed class UnpublishedPageCacheRefresher : CacheRefresherBase<UnpublishedPageCacheRefresher>, IJsonCacheRefresher
    {
        protected override UnpublishedPageCacheRefresher Instance
        {
            get { return this; }
        }

        public override Guid UniqueIdentifier
        {
            get { return new Guid(DistributedCache.UnpublishedPageCacheRefresherId); }
        }

        public override string Name
        {
            get { return "Unpublished Page Refresher"; }
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

        public override void RefreshAll()
        {
            RuntimeCacheProvider.Current.Clear(typeof(IContent));
            base.RefreshAll();
        }

        public override void Refresh(int id)
        {
            RuntimeCacheProvider.Current.Delete(typeof(IContent), id);
            base.Refresh(id);
        }

        public override void Remove(int id)
        {
            RuntimeCacheProvider.Current.Delete(typeof(IContent), id);
            base.Remove(id);
        }

        /// <summary>
        /// Implement the IJsonCacheRefresher so that we can bulk delete/refresh the cache based on multiple IDs
        /// </summary>
        /// <param name="jsonPayload"></param>
        public void Refresh(string jsonPayload)
        {
            foreach (var payload in DeserializeFromJsonPayload(jsonPayload))
            {
                RuntimeCacheProvider.Current.Delete(typeof(IContent), payload.Id);
            }

            OnCacheUpdated(Instance, new CacheRefresherEventArgs(jsonPayload, MessageType.RefreshByJson));
        }
        
    }
}