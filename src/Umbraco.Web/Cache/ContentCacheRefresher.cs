using System;
using System.Collections.Generic;
using System.Linq;
using Umbraco.Core;
using Umbraco.Core.Cache;
using Umbraco.Core.Models;
using Umbraco.Core.Persistence.Repositories;
using Umbraco.Core.Services;
using Umbraco.Web.PublishedCache;

namespace Umbraco.Web.Cache
{
    public sealed class ContentCacheRefresher : JsonCacheRefresherBase<ContentCacheRefresher>
    {
        #region Json

        internal class JsonPayload
        {
            public JsonPayload(int id, TreeChangeTypes changeTypes)
            {
                Id = id;
                ChangeTypes = changeTypes;
            }

            public int Id { get; private set; }
            public TreeChangeTypes ChangeTypes { get; private set; }
        }

        internal static string Serialize(IEnumerable<JsonPayload> payloads)
        {
            return Newtonsoft.Json.JsonConvert.SerializeObject(payloads.ToArray());
        }

        internal static JsonPayload[] Deserialize(string json)
        {
            return Newtonsoft.Json.JsonConvert.DeserializeObject<JsonPayload[]>(json);
        }

        #endregion

        #region Define

        protected override ContentCacheRefresher Instance
        {
            get { return this; }
        }

        public override Guid UniqueIdentifier
        {
            get { return DistributedCache.ContentCacheRefresherGuid; }
        }

        public override string Name
        {
            get { return "ContentCacheRefresher"; }
        }
        
        #endregion

        #region Events

        public override void Refresh(string json)
        {
            var payloads = Deserialize(json);
            var svce = PublishedCachesServiceResolver.Current.Service;
            bool draftChanged, publishedChanged;
            svce.NotifyChanges(payloads, out draftChanged, out publishedChanged);

            if (payloads.Any(x => x.ChangeTypes.HasType(TreeChangeTypes.RefreshAll)) || publishedChanged)
            {
                // when a public version changes
                ApplicationContext.Current.ApplicationCache.ClearPartialViewCache();
                DistributedCache.Instance.ClearAllMacroCacheOnCurrentServer();
                DistributedCache.Instance.ClearXsltCacheOnCurrentServer();
            }

            var runtimeCache = ApplicationContext.Current.ApplicationCache.RuntimeCache;
            runtimeCache.ClearCacheObjectTypes<PublicAccessEntry>();
            foreach (var payload in payloads)
            {
                // remove that one
                runtimeCache.ClearCacheItem(RepositoryBase.GetCacheIdKey<IContent>(payload.Id));

                // remove those that are in the branch
                if (payload.ChangeTypes.HasTypesAny(TreeChangeTypes.RefreshBranch | TreeChangeTypes.Remove))
                {
                    var pathid = "," + payload.Id + ",";
                    runtimeCache.ClearCacheObjectTypes<IContent>((k, v) => v.Path.Contains(pathid));
                }
            }

            // fixme - and we want to notify Examine, etc?

            base.Refresh(json);
        }

        // these events should never trigger
        // everything should be JSON

        public override void RefreshAll()
        {
            throw new NotSupportedException();
        }

        public override void Refresh(int id)
        {
            throw new NotSupportedException();
        }

        public override void Refresh(Guid id)
        {
            throw new NotSupportedException();
        }

        public override void Remove(int id)
        {
            throw new NotSupportedException();
        }

        #endregion

        #region Indirect

        public void RefreshContentTypes(IEnumerable<int> refreshedIds, IEnumerable<int> removedIds)
        {
            // we could try to have a mechanism to notify the PublishedCachesService
            // and figure out whether published items were modified or not... keep it
            // simple for now, just clear the whole thing

            // fixme - but HOW does the PublishedCachesService knows about modified types?!

            ApplicationContext.Current.ApplicationCache.ClearPartialViewCache();
            DistributedCache.Instance.ClearAllMacroCacheOnCurrentServer();
            DistributedCache.Instance.ClearXsltCacheOnCurrentServer();

            var runtimeCache = ApplicationContext.Current.ApplicationCache.RuntimeCache;
            runtimeCache.ClearCacheObjectTypes<PublicAccessEntry>();
            runtimeCache.ClearCacheObjectTypes<IContent>();
        }

        #endregion
    }
}
