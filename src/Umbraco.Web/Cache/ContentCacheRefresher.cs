using System;
using System.Collections.Generic;
using System.Linq;
using Umbraco.Core;
using Umbraco.Core.Cache;
using Umbraco.Core.Models;
using Umbraco.Core.Persistence.Repositories;
using Umbraco.Core.Services;
using Umbraco.Web.PublishedCache;
using ChangeEventTypes = Umbraco.Core.Services.ContentService.ChangeEventTypes;

namespace Umbraco.Web.Cache
{
    public sealed class ContentCacheRefresher : JsonCacheRefresherBase<ContentCacheRefresher>
    {
        #region Json

        // ContentCacheRefresher is the result of the merge of PageCacheRefresher and
        // UnpublishedPageCacheRefresher - because they have to work together as one cache

        // each content HAS a NEWEST version, and MAY have a PUBLISHED version
        // they MAY be the same version, else the NEWEST version is the most recent of both

        // when... triger...
        //
        // Save, Rollback: RefreshNewest
        // Publish: RefreshNewest + RefreshPublished -- RefreshNewest since newest changes & is now published
        // Save&Publish: RefreshNewest + RefreshPublished
        // Unpublish: RefreshNewest + RemovePublished
        // (republished): RefreshPublished -- when it's "published again" because of parents
        // Delete: RemoveNewest + RemovePublished
        // Sort: RefreshNewest [+ RefreshPublished] -- if there's a published version, refresh it
        // Move: RefreshNewest [+ RefreshPublished] -- fixme what shall we do
        // Cancel: RefreshNewest -- when cancelling changes & coming back to published

        // RefreshPublished
        // - Examine should update indexers that do NOT support unpublished content w/published version
        // - IContent cache does nothing
        // - PublishedContent cache should refresh the published content w/published version
        // RefreshNewest
        // - Examine should update indexers that DO support unpublished content w/newest version
        // - IContent cache should clear the content
        // - PublishedContent cache should refresh the preview content w/newest version
        //   if that version is not published, else it should clear the preview content
        // RemovePublished
        // - Examine should clear indexers that do NOT support unpublished content
        // - IContent cache does nothing
        // - PublishedContent cache should clear the published content
        // RemoveNewest
        // - Examine should clear indexers that DO support unpublished content
        // - IContent cache should clear the content
        // - PublishedContent cache should clear the preview content

        internal class JsonPayload
        {
            public JsonPayload(int id, ContentService.ChangeEventTypes action)
            {
                Id = id;
                Action = action;
            }

            public int Id { get; private set; }
            public ContentService.ChangeEventTypes Action { get; private set; }
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
            var resA = svce.NotifyChanges(payloads).ToArray();

            if (payloads.Any(x => x.Action.HasType(ChangeEventTypes.RefreshAll)) 
                || resA.Any(x => x.PublishedChanged))
            {
                // from PageCacheRefresher - when a public version changes
                ApplicationContext.Current.ApplicationCache.ClearPartialViewCache();
                DistributedCache.Instance.ClearAllMacroCacheOnCurrentServer();
                DistributedCache.Instance.ClearXsltCacheOnCurrentServer();
            }

            // from UnpublishedPageCacheRefresher
            var runtimeCache = ApplicationContext.Current.ApplicationCache.RuntimeCache;
            runtimeCache.ClearCacheObjectTypes<PublicAccessEntry>();
            foreach (var payload in payloads)
                runtimeCache.ClearCacheItem(RepositoryBase.GetCacheIdKey<IContent>(payload.Id));

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
    }
}
