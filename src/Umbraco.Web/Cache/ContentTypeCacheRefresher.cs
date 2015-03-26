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
            public JsonPayload()
            { }

            public int Id { get; set; }
            public bool Removed { get; set; } // otherwise, Refreshed
            public string Type { get; set; } // content, media, member...

            // legacy
            public string Alias { get; set; }
            public int[] PropertyTypeIds { get; set; }
            public bool AliasChanged { get; set; }
            public bool PropertyRemoved { get; set; }
            public JsonPayload[] DescendantPayloads { get; set; }
            public bool IsNew { get; set; }
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

        /// <summary>
        /// Converts a content type to a jsonPayload object
        /// </summary>
        /// <param name="contentType"></param>
        /// <param name="isDeleted">if the item was deleted</param>
        /// <returns></returns>
        private static JsonPayload FromContentType(IContentTypeBase contentType, bool isDeleted = false)
        {
            var payload = new JsonPayload
                {
                    Alias = contentType.Alias,
                    Id = contentType.Id,
                    PropertyTypeIds = contentType.PropertyTypes.Select(x => x.Id).ToArray(),
                    //either IContentType or IMediaType or IMemberType
                    Type = (contentType is IContentType) 
                        ? typeof(IContentType).Name 
                        : (contentType is IMediaType)
                        ? typeof(IMediaType).Name
                        : typeof(IMemberType).Name,
                    DescendantPayloads = contentType.Descendants().Select(x => FromContentType(x)).ToArray(),
                    Removed = isDeleted
                };
            //here we need to check if the alias of the content type changed or if one of the properties was removed.                    
            var dirty = contentType as IRememberBeingDirty;
            if (dirty != null)
            {
                payload.PropertyRemoved = dirty.WasPropertyDirty("HasPropertyTypeBeenRemoved");
                payload.AliasChanged = dirty.WasPropertyDirty("Alias");
                payload.IsNew = dirty.WasPropertyDirty("HasIdentity");
            }
            return payload;
        }

        /// <summary>
        /// Creates the custom Json payload used to refresh cache amongst the servers
        /// </summary>
        /// <param name="isDeleted">specify false if this is an update, otherwise true if it is a deletion</param>
        /// <param name="contentTypes"></param>
        /// <returns></returns>
        internal static string SerializeToJsonPayload(bool isDeleted, params IContentTypeBase[] contentTypes)
        {
            var serializer = new JavaScriptSerializer();
            var items = contentTypes.Select(x => FromContentType(x, isDeleted)).ToArray();
            var json = serializer.Serialize(items);
            return json;
        }

        #endregion

        #region Events

        public override void Refresh(string json)
        {
            var payloads = Deserialize(json);

            if (payloads.Any(x => x.Type == typeof(IContentType).Name))
            {
                ApplicationContext.Current.ApplicationCache.RuntimeCache.ClearCacheObjectTypes<IContent>();
                ApplicationContext.Current.ApplicationCache.RuntimeCache.ClearCacheObjectTypes<IContentType>();
            }

            if (payloads.Any(x => x.Type == typeof(IMediaType).Name))
            {
                ApplicationContext.Current.ApplicationCache.RuntimeCache.ClearCacheObjectTypes<IMedia>();
                ApplicationContext.Current.ApplicationCache.RuntimeCache.ClearCacheObjectTypes<IMediaType>();
            }
            
            if (payloads.Any(x => x.Type == typeof(IMemberType).Name))
            {
                ApplicationContext.Current.ApplicationCache.RuntimeCache.ClearCacheObjectTypes<IMember>();
                ApplicationContext.Current.ApplicationCache.RuntimeCache.ClearCacheObjectTypes<IMemberType>();
            }

            var contentTypeService = ApplicationContext.Current.Services.ContentTypeService;
            var impactedRefreshed = payloads
                .Where(x => x.Removed == false)
                .Select(x => contentTypeService.GetContentType(x.Id))
                .WhereNotNull()
                .SelectMany(x => x.ComposedOf()) // fixme what about descendants?!
                .DistinctBy(x => x.Id)
                .ToArray();

            foreach (var contentType in impactedRefreshed)
            {
                ClearLegacyCaches(contentType.Id, contentType.Alias, contentType.PropertyTypes.Select(x => x.Id));
                PublishedContentType.ClearContentType(contentType.Id);
            }

            foreach (var payload in payloads.Where(x => x.Removed))
            {
                ClearLegacyCaches(payload.Id, payload.Alias, payload.PropertyTypeIds);
                PublishedContentType.ClearContentType(payload.Id);
            }

            // do it in THIS order: we want to trigger content type events BEFORE managing content
            base.Refresh(json);

            // NOTE
            // anything that deals with content needs to be done after base.Refresh()
            // so that all content type handling has taken place & we can reference types

            if (payloads.Any(x => x.Type == typeof (IContentType).Name))
            {
                RefreshContentCache(
                    impactedRefreshed.OfType<IContentType>(),
                    payloads.Where(x => x.Removed && x.Type == typeof(IContentType).Name).Select(x => x.Id));
            }

            if (payloads.Any(x => x.Type == typeof(IMediaType).Name))
            {
                RefreshMediaCache(
                    impactedRefreshed.OfType<IMediaType>(),
                    payloads.Where(x => x.Removed && x.Type == typeof(IMediaType).Name).Select(x => x.Id));
            }

            if (payloads.Any(x => x.Type == typeof(IMemberType).Name))
            {
                RefreshMemberCache(
                    impactedRefreshed.OfType<IMemberType>(),
                    payloads.Where(x => x.Removed && x.Type == typeof(IMemberType).Name).Select(x => x.Id));
            }            
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

        private static void RefreshContentCache(IEnumerable<IContentTypeBase> impacted, IEnumerable<int> removed)
        {
            var contentCacheRefresher = CacheRefreshersResolver.Current.GetById(DistributedCache.ContentCacheRefresherGuid) as ContentCacheRefresher;
            if (contentCacheRefresher == null) throw new Exception("oops");

            contentCacheRefresher.RefreshContentTypes(impacted.Select(x => x.Id), removed);

            // fixme - this is xml-cache specific & should be handled by the PublishedCacheService itself!
            var service = PublishedCachesServiceResolver.Current.Service as PublishedCachesService;
            if (service != null)
                service.RoutesCache.Clear();
        }

        private static void RefreshMediaCache(IEnumerable<IContentTypeBase> impacted, IEnumerable<int> removed)
        {
            var mediaCacheRefresher = CacheRefreshersResolver.Current.GetById(DistributedCache.MediaCacheRefresherGuid) as MediaCacheRefresher;
            if (mediaCacheRefresher == null) throw new Exception("oops");

            mediaCacheRefresher.RefreshMediaTypes(impacted.Select(x => x.Id), removed);
        }

        private static void RefreshMemberCache(IEnumerable<IContentTypeBase> impacted, IEnumerable<int> removed)
        {
            var memberCacheRefresher = CacheRefreshersResolver.Current.GetById(DistributedCache.MemberCacheRefresherGuid) as MemberCacheRefresher;
            if (memberCacheRefresher == null) throw new Exception("oops");

            memberCacheRefresher.RefreshMemberTypes(impacted.Select(x => x.Id), removed);
        }
        
        private static void ClearLegacyCaches(int contentTypeId, string contentTypeAlias, IEnumerable<int> propertyTypeIds)
        {
            // legacy umbraco.cms.businesslogic.ContentType

            // clears the cache for each property type associated with the content type
            foreach (var pid in propertyTypeIds)
                ApplicationContext.Current.ApplicationCache.RuntimeCache.ClearCacheItem(CacheKeys.PropertyTypeCacheKey + pid);

            // clears the cache associated with the content type itself
            ApplicationContext.Current.ApplicationCache.RuntimeCache.ClearCacheItem(string.Format("{0}{1}", CacheKeys.ContentTypeCacheKey, contentTypeId));

            // clears the cache associated with the content type properties collection
            ApplicationContext.Current.ApplicationCache.RuntimeCache.ClearCacheItem(CacheKeys.ContentTypePropertiesCacheKey + contentTypeId);

            //clears the dictionary object cache of the legacy ContentType
            global::umbraco.cms.businesslogic.ContentType.RemoveFromDataTypeCache(contentTypeAlias);
        }

        #endregion
    }
}
