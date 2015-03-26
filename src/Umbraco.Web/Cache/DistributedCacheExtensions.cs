using System;
using System.Collections.Generic;
using System.Linq;
using Umbraco.Core;
using Umbraco.Core.Configuration;
using Umbraco.Core.Events;
using Umbraco.Core.Models;
using umbraco;
using umbraco.cms.businesslogic.web;
using Umbraco.Core.Services;

namespace Umbraco.Web.Cache
{
    /// <summary>
    /// Extension methods for DistrubutedCache
    /// </summary>
    internal static class DistributedCacheExtensions
    {
        #region Public access

        public static void RefreshPublicAccess(this DistributedCache dc)
        {
            dc.RefreshAll(new Guid(DistributedCache.PublicAccessCacheRefresherId));
        }


        #endregion

        #region Application tree cache
        public static void RefreshAllApplicationTreeCache(this DistributedCache dc)
        {
            dc.RefreshAll(new Guid(DistributedCache.ApplicationTreeCacheRefresherId));
        }
        #endregion

        #region Application cache
        public static void RefreshAllApplicationCache(this DistributedCache dc)
        {
            dc.RefreshAll(new Guid(DistributedCache.ApplicationCacheRefresherId));
        }
        #endregion

        #region User type cache
        public static void RemoveUserTypeCache(this DistributedCache dc, int userTypeId)
        {
            dc.Remove(new Guid(DistributedCache.UserTypeCacheRefresherId), userTypeId);
        }

        public static void RefreshUserTypeCache(this DistributedCache dc, int userTypeId)
        {
            dc.Refresh(new Guid(DistributedCache.UserTypeCacheRefresherId), userTypeId);
        }

        public static void RefreshAllUserTypeCache(this DistributedCache dc)
        {
            dc.RefreshAll(new Guid(DistributedCache.UserTypeCacheRefresherId));
        }
        #endregion

        #region User cache
        public static void RemoveUserCache(this DistributedCache dc, int userId)
        {
            dc.Remove(new Guid(DistributedCache.UserCacheRefresherId), userId);
        }

        public static void RefreshUserCache(this DistributedCache dc, int userId)
        {
            dc.Refresh(new Guid(DistributedCache.UserCacheRefresherId), userId);
        }

        public static void RefreshAllUserCache(this DistributedCache dc)
        {
            dc.RefreshAll(new Guid(DistributedCache.UserCacheRefresherId));
        } 
        #endregion

        #region User permissions cache
        public static void RemoveUserPermissionsCache(this DistributedCache dc, int userId)
        {
            dc.Remove(new Guid(DistributedCache.UserPermissionsCacheRefresherId), userId);
        }

        public static void RefreshUserPermissionsCache(this DistributedCache dc, int userId)
        {
            dc.Refresh(new Guid(DistributedCache.UserPermissionsCacheRefresherId), userId);
        }

        public static void RefreshAllUserPermissionsCache(this DistributedCache dc)
        {
            dc.RefreshAll(new Guid(DistributedCache.UserPermissionsCacheRefresherId));
        }
        #endregion

        #region Template cache
        /// <summary>
        /// Refreshes the cache amongst servers for a template
        /// </summary>
        /// <param name="dc"></param>
        /// <param name="templateId"></param>
        public static void RefreshTemplateCache(this DistributedCache dc, int templateId)
        {
            dc.Refresh(new Guid(DistributedCache.TemplateRefresherId), templateId);
        }

        /// <summary>
        /// Removes the cache amongst servers for a template
        /// </summary>
        /// <param name="dc"></param>
        /// <param name="templateId"></param>
        public static void RemoveTemplateCache(this DistributedCache dc, int templateId)
        {
            dc.Remove(new Guid(DistributedCache.TemplateRefresherId), templateId);
        } 

        #endregion

        #region Dictionary cache
        /// <summary>
        /// Refreshes the cache amongst servers for a dictionary item
        /// </summary>
        /// <param name="dc"></param>
        /// <param name="dictionaryItemId"></param>
        public static void RefreshDictionaryCache(this DistributedCache dc, int dictionaryItemId)
        {
            dc.Refresh(new Guid(DistributedCache.DictionaryCacheRefresherId), dictionaryItemId);
        }

        /// <summary>
        /// Refreshes the cache amongst servers for a dictionary item
        /// </summary>
        /// <param name="dc"></param>
        /// <param name="dictionaryItemId"></param>
        public static void RemoveDictionaryCache(this DistributedCache dc, int dictionaryItemId)
        {
            dc.Remove(new Guid(DistributedCache.DictionaryCacheRefresherId), dictionaryItemId);
        }

        #endregion
        
        #region Data type cache
        /// <summary>
        /// Refreshes the cache amongst servers for a data type
        /// </summary>
        /// <param name="dc"></param>
        /// <param name="dataType"></param>
        public static void RefreshDataTypeCache(this DistributedCache dc, global::umbraco.cms.businesslogic.datatype.DataTypeDefinition dataType)
        {
            if (dataType != null)
            {
                dc.RefreshByJson(new Guid(DistributedCache.DataTypeCacheRefresherId),
                    DataTypeCacheRefresher.SerializeToJsonPayload(dataType));
            }       
        }

        /// <summary>
        /// Removes the cache amongst servers for a data type
        /// </summary>
        /// <param name="dc"></param>
        /// <param name="dataType"></param>
        public static void RemoveDataTypeCache(this DistributedCache dc, global::umbraco.cms.businesslogic.datatype.DataTypeDefinition dataType)
        {
            if (dataType != null)
            {
                dc.RefreshByJson(new Guid(DistributedCache.DataTypeCacheRefresherId),
                    DataTypeCacheRefresher.SerializeToJsonPayload(dataType));
            }  
        }

        /// <summary>
        /// Refreshes the cache amongst servers for a data type
        /// </summary>
        /// <param name="dc"></param>
        /// <param name="dataType"></param>
        public static void RefreshDataTypeCache(this DistributedCache dc, IDataTypeDefinition dataType)
        {
            if (dataType != null)
            {
                dc.RefreshByJson(new Guid(DistributedCache.DataTypeCacheRefresherId),
                    DataTypeCacheRefresher.SerializeToJsonPayload(dataType));
            }
        }

        /// <summary>
        /// Removes the cache amongst servers for a data type
        /// </summary>
        /// <param name="dc"></param>
        /// <param name="dataType"></param>
        public static void RemoveDataTypeCache(this DistributedCache dc, IDataTypeDefinition dataType)
        {
            if (dataType != null)
            {
                dc.RefreshByJson(new Guid(DistributedCache.DataTypeCacheRefresherId),
                    DataTypeCacheRefresher.SerializeToJsonPayload(dataType));
            }
        }

        #endregion

        #region Content+Media cache

        const string ContentCacheBufferKey = "DistributedCache.ContentCacheBuffer";
        const string MediaCacheBufferKey = "DistributedCache.MediaCacheBuffer";

        public static void FlushChangeSet(this DistributedCache dc, ChangeSet changeSet)
        {
            if (changeSet == null) return;

            if (changeSet.Items.ContainsKey(ContentCacheBufferKey))
            {
                var contentBuffer = (List<ContentCacheRefresher.JsonPayload>)changeSet.Items[ContentCacheBufferKey];
                dc.RefreshByJson(DistributedCache.ContentCacheRefresherGuid, ContentCacheRefresher.Serialize(contentBuffer));
                changeSet.Items.Remove(ContentCacheBufferKey);
            }

            if (changeSet.Items.ContainsKey(MediaCacheBufferKey))
            {
                var mediaBuffer = (List<MediaCacheRefresher.JsonPayload>)changeSet.Items[MediaCacheBufferKey];
                dc.RefreshByJson(DistributedCache.MediaCacheRefresherGuid, MediaCacheRefresher.Serialize(mediaBuffer));
                changeSet.Items.Remove(MediaCacheBufferKey);
            }
        }

        #endregion

        #region Content cache

        private static void RefreshContentCacheByJson(this DistributedCache dc, IEnumerable<ContentCacheRefresher.JsonPayload> payloads)
        {
            var changeSet = ChangeSet.Ambient;
            if (changeSet == null)
            {
                dc.RefreshByJson(DistributedCache.ContentCacheRefresherGuid, ContentCacheRefresher.Serialize(payloads));
            }
            else
            {
                var buffer = changeSet.Items.ContainsKey(ContentCacheBufferKey) ? (List<ContentCacheRefresher.JsonPayload>) changeSet.Items[ContentCacheBufferKey] : null;
                if (buffer == null) changeSet.Items[ContentCacheBufferKey] = buffer = new List<ContentCacheRefresher.JsonPayload>();
                buffer.AddRange(payloads);
            }
        }

        /// <summary>
        /// Refreshes all published content.
        /// </summary>
        /// <param name="dc"></param>
        public static void RefreshAllPublishedContentCache(this DistributedCache dc)
        {
            var payloads = new[] { new ContentCacheRefresher.JsonPayload(0, TreeChangeTypes.RefreshAll) };

            dc.RefreshContentCacheByJson(payloads);
        }

        public static void RefreshContentCache(this DistributedCache dc, TreeChange<IContent>[] changes)
        {
            if (changes.Length == 0) return;

            var payloads = changes
                .Select(x => new ContentCacheRefresher.JsonPayload(x.Item.Id, x.ChangeTypes));

            dc.RefreshContentCacheByJson(payloads);
        }

        #endregion

        #region Member cache

        /// <summary>
        /// Refreshes the cache among servers for a member
        /// </summary>
        /// <param name="dc"></param>
        /// <param name="members"></param>
        public static void RefreshMemberCache(this DistributedCache dc, params IMember[] members)
        {
            dc.Refresh(new Guid(DistributedCache.MemberCacheRefresherId), x => x.Id, members);
        }

        /// <summary>
        /// Removes the cache among servers for a member
        /// </summary>
        /// <param name="dc"></param>
        /// <param name="members"></param>
        public static void RemoveMemberCache(this DistributedCache dc, params IMember[] members)
        {
            dc.Remove(new Guid(DistributedCache.MemberCacheRefresherId), x => x.Id, members);
        } 

        /// <summary>
        /// Refreshes the cache among servers for a member
        /// </summary>
        /// <param name="dc"></param>
        /// <param name="memberId"></param>
        [Obsolete("Use the RefreshMemberCache with strongly typed IMember objects instead")]
        public static void RefreshMemberCache(this DistributedCache dc, int memberId)
        {
            dc.Refresh(new Guid(DistributedCache.MemberCacheRefresherId), memberId);
        }

        /// <summary>
        /// Removes the cache among servers for a member
        /// </summary>
        /// <param name="dc"></param>
        /// <param name="memberId"></param>
        [Obsolete("Use the RemoveMemberCache with strongly typed IMember objects instead")]
        public static void RemoveMemberCache(this DistributedCache dc, int memberId)
        {
            dc.Remove(new Guid(DistributedCache.MemberCacheRefresherId), memberId);
        } 

        #endregion

        #region Member group cache
        /// <summary>
        /// Refreshes the cache among servers for a member group
        /// </summary>
        /// <param name="dc"></param>
        /// <param name="memberGroupId"></param>
        public static void RefreshMemberGroupCache(this DistributedCache dc, int memberGroupId)
        {
            dc.Refresh(new Guid(DistributedCache.MemberGroupCacheRefresherId), memberGroupId);
        }

        /// <summary>
        /// Removes the cache among servers for a member group
        /// </summary>
        /// <param name="dc"></param>
        /// <param name="memberGroupId"></param>
        public static void RemoveMemberGroupCache(this DistributedCache dc, int memberGroupId)
        {
            dc.Remove(new Guid(DistributedCache.MemberGroupCacheRefresherId), memberGroupId);
        }

        #endregion

        #region Media Cache

        private static void RefreshMediaCacheByJson(this DistributedCache dc, IEnumerable<MediaCacheRefresher.JsonPayload> payloads)
        {
            var changeSet = ChangeSet.Ambient;
            if (changeSet == null)
            {
                dc.RefreshByJson(DistributedCache.MediaCacheRefresherGuid, MediaCacheRefresher.Serialize(payloads));
            }
            else
            {
                var buffer = changeSet.Items.ContainsKey(MediaCacheBufferKey) ? (List<MediaCacheRefresher.JsonPayload>)changeSet.Items[MediaCacheBufferKey] : null;
                if (buffer == null) changeSet.Items[MediaCacheBufferKey] = buffer = new List<MediaCacheRefresher.JsonPayload>();
                buffer.AddRange(payloads);
            }
        }

        public static void RefreshMediaCache(this DistributedCache dc, TreeChange<IMedia>[] changes)
        {
            if (changes.Length == 0) return;

            var payloads = changes
                .Select(x => new MediaCacheRefresher.JsonPayload(x.Item.Id, x.ChangeTypes));

            dc.RefreshMediaCacheByJson(payloads);
        }

        #endregion

        #region Macro Cache

        /// <summary>
        /// Clears the cache for all macros on the current server
        /// </summary>
        /// <param name="dc"></param>
        public static void ClearAllMacroCacheOnCurrentServer(this DistributedCache dc)
        {
            //NOTE: The 'false' ensure that it will only refresh on the current server, not post to all servers
            dc.RefreshAll(new Guid(DistributedCache.MacroCacheRefresherId), false);
        }

        /// <summary>
        /// Refreshes the cache amongst servers for a macro item
        /// </summary>
        /// <param name="dc"></param>
        /// <param name="macro"></param>
        public static void RefreshMacroCache(this DistributedCache dc, IMacro macro)
        {
            if (macro != null)
            {
                dc.RefreshByJson(new Guid(DistributedCache.MacroCacheRefresherId),
                    MacroCacheRefresher.SerializeToJsonPayload(macro));
            }
        }

        /// <summary>
        /// Removes the cache amongst servers for a macro item
        /// </summary>
        /// <param name="dc"></param>
        /// <param name="macro"></param>
        public static void RemoveMacroCache(this DistributedCache dc, IMacro macro)
        {
            if (macro != null)
            {
                dc.RefreshByJson(new Guid(DistributedCache.MacroCacheRefresherId),
                    MacroCacheRefresher.SerializeToJsonPayload(macro));
            }
        }

        /// <summary>
        /// Refreshes the cache amongst servers for a macro item
        /// </summary>
        /// <param name="dc"></param>
        /// <param name="macro"></param>
        public static void RefreshMacroCache(this DistributedCache dc, global::umbraco.cms.businesslogic.macro.Macro macro)
        {
            if (macro != null)
            {
                dc.RefreshByJson(new Guid(DistributedCache.MacroCacheRefresherId),
                    MacroCacheRefresher.SerializeToJsonPayload(macro));
            }
        }
        
        /// <summary>
        /// Removes the cache amongst servers for a macro item
        /// </summary>
        /// <param name="dc"></param>
        /// <param name="macro"></param>
        public static void RemoveMacroCache(this DistributedCache dc, global::umbraco.cms.businesslogic.macro.Macro macro)
        {
            if (macro != null)
            {
                dc.RefreshByJson(new Guid(DistributedCache.MacroCacheRefresherId),
                    MacroCacheRefresher.SerializeToJsonPayload(macro));
            }
        }

        /// <summary>
        /// Removes the cache amongst servers for a macro item
        /// </summary>
        /// <param name="dc"></param>
        /// <param name="macro"></param>
        public static void RemoveMacroCache(this DistributedCache dc, macro macro)
        {
            if (macro != null && macro.Model != null)
            {
                dc.RefreshByJson(new Guid(DistributedCache.MacroCacheRefresherId),
                    MacroCacheRefresher.SerializeToJsonPayload(macro));
            }
        } 
        #endregion

        #region Document type cache

        /// <summary>
        /// Remove all cache for a given content type
        /// </summary>
        /// <param name="dc"></param>
        /// <param name="contentType"></param>
        public static void RefreshContentTypeCache(this DistributedCache dc, IContentType contentType)
        {
            if (contentType != null)
            {
                dc.RefreshByJson(new Guid(DistributedCache.ContentTypeCacheRefresherId),
                    ContentTypeCacheRefresher.SerializeToJsonPayload(false, contentType));
            }
        }

        /// <summary>
        /// Remove all cache for a given content type
        /// </summary>
        /// <param name="dc"></param>
        /// <param name="contentType"></param>
        public static void RemoveContentTypeCache(this DistributedCache dc, IContentType contentType)
        {
            if (contentType != null)
            {
                dc.RefreshByJson(new Guid(DistributedCache.ContentTypeCacheRefresherId),
                    ContentTypeCacheRefresher.SerializeToJsonPayload(true, contentType));
            }
        }

        #endregion

        #region Media type cache

        /// <summary>
        /// Remove all cache for a given media type
        /// </summary>
        /// <param name="dc"></param>
        /// <param name="mediaType"></param>
        public static void RefreshMediaTypeCache(this DistributedCache dc, IMediaType mediaType)
        {
            if (mediaType != null)
            {
                dc.RefreshByJson(new Guid(DistributedCache.ContentTypeCacheRefresherId),
                    ContentTypeCacheRefresher.SerializeToJsonPayload(false, mediaType));
            }
        }

        /// <summary>
        /// Remove all cache for a given media type
        /// </summary>
        /// <param name="dc"></param>
        /// <param name="mediaType"></param>
        public static void RemoveMediaTypeCache(this DistributedCache dc, IMediaType mediaType)
        {
            if (mediaType != null)
            {
                dc.RefreshByJson(new Guid(DistributedCache.ContentTypeCacheRefresherId),
                    ContentTypeCacheRefresher.SerializeToJsonPayload(true, mediaType));
            }
        }

        #endregion

        #region Member type cache

        /// <summary>
        /// Remove all cache for a given media type
        /// </summary>
        /// <param name="dc"></param>
        /// <param name="memberType"></param>
        public static void RefreshMemberTypeCache(this DistributedCache dc, IMemberType memberType)
        {
            if (memberType != null)
            {
                dc.RefreshByJson(new Guid(DistributedCache.ContentTypeCacheRefresherId),
                    ContentTypeCacheRefresher.SerializeToJsonPayload(false, memberType));
            }
        }

        /// <summary>
        /// Remove all cache for a given media type
        /// </summary>
        /// <param name="dc"></param>
        /// <param name="memberType"></param>
        public static void RemoveMemberTypeCache(this DistributedCache dc, IMemberType memberType)
        {
            if (memberType != null)
            {
                dc.RefreshByJson(new Guid(DistributedCache.ContentTypeCacheRefresherId),
                    ContentTypeCacheRefresher.SerializeToJsonPayload(true, memberType));
            }
        }

        #endregion

        #region Stylesheet Cache

        public static void RefreshStylesheetPropertyCache(this DistributedCache dc, global::umbraco.cms.businesslogic.web.StylesheetProperty styleSheetProperty)
        {
            if (styleSheetProperty != null)
            {
                dc.Refresh(new Guid(DistributedCache.StylesheetPropertyCacheRefresherId), styleSheetProperty.Id);
            }
        }

        public static void RemoveStylesheetPropertyCache(this DistributedCache dc, global::umbraco.cms.businesslogic.web.StylesheetProperty styleSheetProperty)
        {
            if (styleSheetProperty != null)
            {
                dc.Remove(new Guid(DistributedCache.StylesheetPropertyCacheRefresherId), styleSheetProperty.Id);
            }
        }

        public static void RefreshStylesheetCache(this DistributedCache dc, StyleSheet styleSheet)
        {
            if (styleSheet != null)
            {
                dc.Refresh(new Guid(DistributedCache.StylesheetCacheRefresherId), styleSheet.Id);
            }
        }

        public static void RemoveStylesheetCache(this DistributedCache dc, StyleSheet styleSheet)
        {
            if (styleSheet != null)
            {
                dc.Remove(new Guid(DistributedCache.StylesheetCacheRefresherId), styleSheet.Id);
            }
        }

        public static void RefreshStylesheetCache(this DistributedCache dc, Umbraco.Core.Models.Stylesheet styleSheet)
        {
            if (styleSheet != null)
            {
                dc.Refresh(new Guid(DistributedCache.StylesheetCacheRefresherId), styleSheet.Id);
            }
        }

        public static void RemoveStylesheetCache(this DistributedCache dc, Umbraco.Core.Models.Stylesheet styleSheet)
        {
            if (styleSheet != null)
            {
                dc.Remove(new Guid(DistributedCache.StylesheetCacheRefresherId), styleSheet.Id);
            }
        }

        #endregion

        #region Domain Cache

        public static void RefreshDomainCache(this DistributedCache dc, IDomain domain)
        {
            if (domain != null)
            {
                dc.Refresh(new Guid(DistributedCache.DomainCacheRefresherId), domain.Id);
            }
        }

        public static void RemoveDomainCache(this DistributedCache dc, IDomain domain)
        {
            if (domain != null)
            {
                dc.Remove(new Guid(DistributedCache.DomainCacheRefresherId), domain.Id);
            }
        }

        #endregion

        #region Language Cache

        public static void RefreshLanguageCache(this DistributedCache dc, ILanguage language)
        {
            if (language != null)
            {
                dc.Refresh(new Guid(DistributedCache.LanguageCacheRefresherId), language.Id);
            }
        }

        public static void RemoveLanguageCache(this DistributedCache dc, ILanguage language)
        {
            if (language != null)
            {
                dc.Remove(new Guid(DistributedCache.LanguageCacheRefresherId), language.Id);
            }
        }

        public static void RefreshLanguageCache(this DistributedCache dc, global::umbraco.cms.businesslogic.language.Language language)
        {
            if (language != null)
            {
                dc.Refresh(new Guid(DistributedCache.LanguageCacheRefresherId), language.id);
            }
        }

        public static void RemoveLanguageCache(this DistributedCache dc, global::umbraco.cms.businesslogic.language.Language language)
        {
            if (language != null)
            {
                dc.Remove(new Guid(DistributedCache.LanguageCacheRefresherId), language.id);
            }
        }

        #endregion

        public static void ClearXsltCacheOnCurrentServer(this DistributedCache dc)
        {
            if (UmbracoConfig.For.UmbracoSettings().Content.UmbracoLibraryCacheDuration > 0)
            {
                ApplicationContext.Current.ApplicationCache.ClearCacheObjectTypes("MS.Internal.Xml.XPath.XPathSelectionIterator");
            }
        }
    }
}