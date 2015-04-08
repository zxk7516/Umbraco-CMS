using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;
using Umbraco.Core;
using Umbraco.Core.Cache;
using Umbraco.Core.Models;
using Umbraco.Core.Models.EntityBase;
using Umbraco.Core.Models.PublishedContent;
using Umbraco.Core.Models.Rdbms;
using Umbraco.Core.Services;
using Umbraco.Web.PublishedCache;
using Umbraco.Web.PublishedCache.XmlPublishedCache;

namespace Umbraco.Web.Cache
{

    /// <summary>
    /// A cache refresher to ensure content type cache is updated when content types change - this is applicable to content, media and member types
    /// </summary>
    /// <remarks>
    /// This is not intended to be used directly in your code
    /// </remarks>
    public sealed class ContentTypeCacheRefresher : JsonCacheRefresherBase<ContentTypeCacheRefresher>
    {
        #region Json

        internal class JsonPayload
        {
            public JsonPayload(string itemType, int id, TreeChangeTypes changeTypes)
            {
                ItemType = itemType;
                Id = id;
                ChangeTypes = changeTypes;
            }

            public string ItemType { get; private set; }
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

        protected override ContentTypeCacheRefresher Instance
        {
            get { return this; }
        }

        public override Guid UniqueIdentifier
        {
            get { return DistributedCache.ContentTypeCacheRefresherGuid; }
        }

        public override string Name
        {
            get { return "ContentTypeCacheRefresher"; }
        }
        
        #endregion

        #region Events

        public override void Refresh(string json)
        {
            var payloads = Deserialize(json);

            // TODO: refactor
            // we should NOT directly clear caches here, but instead ask whatever class
            // is managing the cache to please clear that cache properly

            if (payloads.Any(x => x.ItemType == typeof(IContentType).Name))
            {
                ApplicationContext.Current.ApplicationCache.RuntimeCache.ClearCacheObjectTypes<IContent>();
                ApplicationContext.Current.ApplicationCache.RuntimeCache.ClearCacheObjectTypes<IContentType>();
            }

            if (payloads.Any(x => x.ItemType == typeof(IMediaType).Name))
            {
                ApplicationContext.Current.ApplicationCache.RuntimeCache.ClearCacheObjectTypes<IMedia>();
                ApplicationContext.Current.ApplicationCache.RuntimeCache.ClearCacheObjectTypes<IMediaType>();
            }

            if (payloads.Any(x => x.ItemType == typeof(IMemberType).Name))
            {
                ApplicationContext.Current.ApplicationCache.RuntimeCache.ClearCacheObjectTypes<IMember>();
                ApplicationContext.Current.ApplicationCache.RuntimeCache.ClearCacheObjectTypes<IMemberType>();
            }

            foreach (var id in payloads.Select(x => x.Id))
                ClearLegacyCaches(id);

            // do it in THIS order: we want to trigger content type events BEFORE managing content
            base.Refresh(json);

            // NOTE
            // anything that deals with content needs to be done after base.Refresh()
            // so that all content type handling has taken place & we can reference types

            if (payloads.Any(x => x.ItemType == typeof (IContentType).Name))
                RefreshContentCache(payloads.Where(x => x.ItemType == typeof (IContentType).Name));

            if (payloads.Any(x => x.ItemType == typeof(IMediaType).Name))
                RefreshMediaCache(payloads.Where(x => x.ItemType == typeof (IMediaType).Name));

            if (payloads.Any(x => x.ItemType == typeof (IMemberType).Name))
                RefreshMemberCache(payloads.Where(x => x.ItemType == typeof (IMemberType).Name));
        }

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

        private static void RefreshContentCache(IEnumerable<JsonPayload> payloads)
        {
            var contentCacheRefresher = CacheRefreshersResolver.Current.GetById(DistributedCache.ContentCacheRefresherGuid) as ContentCacheRefresher;
            if (contentCacheRefresher == null) throw new Exception("oops");

            // when an 'item' type is removed, all 'items' are previously removed,
            // so here we only need to bother with 'item' types that have changed.
            contentCacheRefresher.RefreshContentTypes(payloads.Where(x => x.ChangeTypes.HasType(TreeChangeTypes.RefreshNode)).Select(x => x.Id));

            // fixme - this is xml-cache specific & should be handled by the PublishedCacheService itself!
            var service = PublishedCachesServiceResolver.Current.Service as PublishedCachesService;
            if (service != null)
                service.RoutesCache.Clear();
        }

        private static void RefreshMediaCache(IEnumerable<JsonPayload> payloads)
        {
            var mediaCacheRefresher = CacheRefreshersResolver.Current.GetById(DistributedCache.MediaCacheRefresherGuid) as MediaCacheRefresher;
            if (mediaCacheRefresher == null) throw new Exception("oops");

            // when an 'item' type is removed, all 'items' are previously removed,
            // so here we only need to bother with 'item' types that have changed.
            mediaCacheRefresher.RefreshMediaTypes(payloads.Where(x => x.ChangeTypes.HasType(TreeChangeTypes.RefreshNode)).Select(x => x.Id));
        }

        private static void RefreshMemberCache(IEnumerable<JsonPayload> payloads)
        {
            var memberCacheRefresher = CacheRefreshersResolver.Current.GetById(DistributedCache.MemberCacheRefresherGuid) as MemberCacheRefresher;
            if (memberCacheRefresher == null) throw new Exception("oops");

            // when an 'item' type is removed, all 'items' are previously removed,
            // so here we only need to bother with 'item' types that have changed.
            memberCacheRefresher.RefreshMemberTypes(payloads.Where(x => x.ChangeTypes.HasType(TreeChangeTypes.RefreshNode)).Select(x => x.Id));
        }
        
        private static void ClearLegacyCaches(int contentTypeId /*, string contentTypeAlias, IEnumerable<int> propertyTypeIds*/)
        {
            // legacy umbraco.cms.businesslogic.ContentType

            // TODO - get rid of all this mess

            // clears the cache for each property type associated with the content type
            // see src/umbraco.cms/businesslogic/propertytype/propertytype.cs
            // that cache is disabled because we could not clear it properly
            //foreach (var pid in propertyTypeIds)
            //    ApplicationContext.Current.ApplicationCache.RuntimeCache.ClearCacheItem(CacheKeys.PropertyTypeCacheKey + pid);

            // clears the cache associated with the content type itself
            ApplicationContext.Current.ApplicationCache.RuntimeCache.ClearCacheItem(CacheKeys.ContentTypeCacheKey + contentTypeId);

            // clears the cache associated with the content type properties collection
            ApplicationContext.Current.ApplicationCache.RuntimeCache.ClearCacheItem(CacheKeys.ContentTypePropertiesCacheKey + contentTypeId);

            // clears the dictionary object cache of the legacy ContentType
            // see src/umbraco.cms/businesslogic/ContentType.cs
            // that cache is disabled because we could not clear it properly
            //global::umbraco.cms.businesslogic.ContentType.RemoveFromDataTypeCache(contentTypeAlias);
        }

        #endregion
    }
}
