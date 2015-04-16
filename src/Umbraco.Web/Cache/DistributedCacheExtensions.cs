using System;
using System.Linq;
using System.Collections.Generic;
using Umbraco.Core;
using Umbraco.Core.Configuration;
using Umbraco.Core.Models;
using umbraco;
using umbraco.cms.businesslogic.web;
using Umbraco.Core.Persistence.Repositories;
using Umbraco.Core.Services;

namespace Umbraco.Web.Cache
{
    /// <summary>
    /// Extension methods for <see cref="DistributedCache"/>
    /// </summary>
    internal static class DistributedCacheExtensions
    {
        #region ChangeSet

        private const string ContentCacheBufferKey = "DistributedCache.ContentCacheBuffer";
        private const string MediaCacheBufferKey = "DistributedCache.MediaCacheBuffer";
        private const string MemberCacheBufferKey = "DistributedCache.MemberCacheBuffer";
        private const string ContentTypeCacheBufferKey = "DistributedCache.ContentTypeCacheBuffer";

        public static void FlushChangeSet(this DistributedCache dc, ChangeSet changeSet)
        {
            if (changeSet == null) return;

            if (changeSet.Items.ContainsKey(ContentCacheBufferKey))
            {
                var buffer = (List<ContentCacheRefresher.JsonPayload>)changeSet.Items[ContentCacheBufferKey];
                dc.RefreshByPayload(DistributedCache.ContentCacheRefresherGuid, buffer.ToArray());
                changeSet.Items.Remove(ContentCacheBufferKey);
            }

            if (changeSet.Items.ContainsKey(MediaCacheBufferKey))
            {
                var buffer = (List<MediaCacheRefresher.JsonPayload>)changeSet.Items[MediaCacheBufferKey];
                dc.RefreshByJson(DistributedCache.MediaCacheRefresherGuid, MediaCacheRefresher.Serialize(buffer.ToArray()));
                changeSet.Items.Remove(MediaCacheBufferKey);
            }

            if (changeSet.Items.ContainsKey(MemberCacheBufferKey))
            {
                throw new NotImplementedException("ChangeSet does not support members?");
                //var buffer = (List<MemberCacheRefresher.JsonPayload>)changeSet.Items[MemberCacheBufferKey];
                //dc.RefreshByJson(DistributedCache.MemberCacheRefresherGuid, MemberCacheRefresher.Serialize(buffer));
                //changeSet.Items.Remove(MemberCacheBufferKey);
            }

            if (changeSet.Items.ContainsKey(ContentTypeCacheBufferKey))
            {
                var buffer = (List<ContentTypeCacheRefresher.JsonPayload>)changeSet.Items[ContentTypeCacheBufferKey];
                dc.RefreshByJson(DistributedCache.ContentTypeCacheRefresherGuid, ContentTypeCacheRefresher.Serialize(buffer.ToArray()));
                changeSet.Items.Remove(ContentTypeCacheBufferKey);
            }
        }

        #endregion

        #region Public access cache

        public static void RefreshPublicAccess(this DistributedCache dc)
        {
            dc.RefreshAll(DistributedCache.PublicAccessCacheRefresherGuid);
        }

        #endregion

        #region Application tree cache

        public static void RefreshAllApplicationTreeCache(this DistributedCache dc)
        {
            dc.RefreshAll(DistributedCache.ApplicationTreeCacheRefresherGuid);
        }

        #endregion

        #region Application cache

        public static void RefreshAllApplicationCache(this DistributedCache dc)
        {
            dc.RefreshAll(DistributedCache.ApplicationCacheRefresherGuid);
        }

        #endregion

        #region User type cache

        public static void RemoveUserTypeCache(this DistributedCache dc, int userTypeId)
        {
            dc.Remove(DistributedCache.UserTypeCacheRefresherGuid, userTypeId);
        }

        public static void RefreshUserTypeCache(this DistributedCache dc, int userTypeId)
        {
            dc.Refresh(DistributedCache.UserTypeCacheRefresherGuid, userTypeId);
        }

        public static void RefreshAllUserTypeCache(this DistributedCache dc)
        {
            dc.RefreshAll(DistributedCache.UserTypeCacheRefresherGuid);
        }

        #endregion

        #region User cache

        public static void RemoveUserCache(this DistributedCache dc, int userId)
        {
            dc.Remove(DistributedCache.UserCacheRefresherGuid, userId);
        }

        public static void RefreshUserCache(this DistributedCache dc, int userId)
        {
            dc.Refresh(DistributedCache.UserCacheRefresherGuid, userId);
        }

        public static void RefreshAllUserCache(this DistributedCache dc)
        {
            dc.RefreshAll(DistributedCache.UserCacheRefresherGuid);
        } 

        #endregion

        #region User permissions cache

        public static void RemoveUserPermissionsCache(this DistributedCache dc, int userId)
        {
            dc.Remove(DistributedCache.UserPermissionsCacheRefresherGuid, userId);
        }

        public static void RefreshUserPermissionsCache(this DistributedCache dc, int userId)
        {
            dc.Refresh(DistributedCache.UserPermissionsCacheRefresherGuid, userId);
        }

        public static void RefreshAllUserPermissionsCache(this DistributedCache dc)
        {
            dc.RefreshAll(DistributedCache.UserPermissionsCacheRefresherGuid);
        }

        #endregion

        #region Template cache

        public static void RefreshTemplateCache(this DistributedCache dc, int templateId)
        {
            dc.Refresh(DistributedCache.TemplateRefresherGuid, templateId);
        }

        public static void RemoveTemplateCache(this DistributedCache dc, int templateId)
        {
            dc.Remove(DistributedCache.TemplateRefresherGuid, templateId);
        } 

        #endregion

        #region Dictionary cache

        public static void RefreshDictionaryCache(this DistributedCache dc, int dictionaryItemId)
        {
            dc.Refresh(DistributedCache.DictionaryCacheRefresherGuid, dictionaryItemId);
        }

        public static void RemoveDictionaryCache(this DistributedCache dc, int dictionaryItemId)
        {
            dc.Remove(DistributedCache.DictionaryCacheRefresherGuid, dictionaryItemId);
        }

        #endregion
        
        #region Data type cache

        public static void RefreshDataTypeCache(this DistributedCache dc, global::umbraco.cms.businesslogic.datatype.DataTypeDefinition dataType)
        {
            if (dataType == null) return;
            dc.RefreshByJson(DistributedCache.DataTypeCacheRefresherGuid, DataTypeCacheRefresher.SerializeToJsonPayload(dataType));
        }

        public static void RemoveDataTypeCache(this DistributedCache dc, global::umbraco.cms.businesslogic.datatype.DataTypeDefinition dataType)
        {
            if (dataType == null) return;
            dc.RefreshByJson(DistributedCache.DataTypeCacheRefresherGuid, DataTypeCacheRefresher.SerializeToJsonPayload(dataType));
        }

        public static void RefreshDataTypeCache(this DistributedCache dc, IDataTypeDefinition dataType)
        {
            if (dataType == null) return;
            dc.RefreshByJson(DistributedCache.DataTypeCacheRefresherGuid, DataTypeCacheRefresher.SerializeToJsonPayload(dataType));
        }

        public static void RemoveDataTypeCache(this DistributedCache dc, IDataTypeDefinition dataType)
        {
            if (dataType == null) return;
            dc.RefreshByJson(DistributedCache.DataTypeCacheRefresherGuid, DataTypeCacheRefresher.SerializeToJsonPayload(dataType));
        }

        #endregion

        #region Content cache

        private static void RefreshContentCacheByPayload(this DistributedCache dc, IEnumerable<ContentCacheRefresher.JsonPayload> payloads)
        {
            var changeSet = ChangeSet.Ambient;
            if (changeSet == null)
            {
                dc.RefreshByPayload(DistributedCache.ContentCacheRefresherGuid, payloads.ToArray());
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

            dc.RefreshContentCacheByPayload(payloads);
        }

        public static void RefreshContentCache(this DistributedCache dc, TreeChange<IContent>[] changes)
        {
            if (changes.Length == 0) return;

            var payloads = changes
                .Select(x => new ContentCacheRefresher.JsonPayload(x.Item.Id, x.ChangeTypes));

            dc.RefreshContentCacheByPayload(payloads);
        }

        #endregion

        #region Member cache

        public static void RefreshMemberCache(this DistributedCache dc, params IMember[] members)
        {
            dc.Refresh(DistributedCache.MemberCacheRefresherGuid, x => x.Id, members);
        }

        public static void RemoveMemberCache(this DistributedCache dc, params IMember[] members)
        {
            dc.Remove(DistributedCache.MemberCacheRefresherGuid, x => x.Id, members);
        } 

        [Obsolete("Use the RefreshMemberCache with strongly typed IMember objects instead")]
        public static void RefreshMemberCache(this DistributedCache dc, int memberId)
        {
            dc.Refresh(DistributedCache.MemberCacheRefresherGuid, memberId);
        }

        [Obsolete("Use the RemoveMemberCache with strongly typed IMember objects instead")]
        public static void RemoveMemberCache(this DistributedCache dc, int memberId)
        {
            dc.Remove(DistributedCache.MemberCacheRefresherGuid, memberId);
        } 

        #endregion

        #region Member group cache

        public static void RefreshMemberGroupCache(this DistributedCache dc, int memberGroupId)
        {
            dc.Refresh(DistributedCache.MemberGroupCacheRefresherGuid, memberGroupId);
        }

        public static void RemoveMemberGroupCache(this DistributedCache dc, int memberGroupId)
        {
            dc.Remove(DistributedCache.MemberGroupCacheRefresherGuid, memberGroupId);
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

        public static void RefreshMacroCache(this DistributedCache dc, IMacro macro)
        {
            if (macro == null) return;
            dc.RefreshByJson(DistributedCache.MacroCacheRefresherGuid, MacroCacheRefresher.SerializeToJsonPayload(macro));
        }

        public static void RemoveMacroCache(this DistributedCache dc, IMacro macro)
        {
            if (macro == null) return;
            dc.RefreshByJson(DistributedCache.MacroCacheRefresherGuid, MacroCacheRefresher.SerializeToJsonPayload(macro));
        }

        public static void RefreshMacroCache(this DistributedCache dc, global::umbraco.cms.businesslogic.macro.Macro macro)
        {
            if (macro == null) return;
            dc.RefreshByJson(DistributedCache.MacroCacheRefresherGuid, MacroCacheRefresher.SerializeToJsonPayload(macro));
        }
        
        public static void RemoveMacroCache(this DistributedCache dc, global::umbraco.cms.businesslogic.macro.Macro macro)
        {
            if (macro == null) return;
            dc.RefreshByJson(DistributedCache.MacroCacheRefresherGuid, MacroCacheRefresher.SerializeToJsonPayload(macro));
        }

        public static void RemoveMacroCache(this DistributedCache dc, macro macro)
        {
            if (macro == null || macro.Model == null) return;
            dc.RefreshByJson(DistributedCache.MacroCacheRefresherGuid, MacroCacheRefresher.SerializeToJsonPayload(macro));
        } 

        #endregion

        #region Content/Media/Member type cache

        private static void RefreshContentTypeCacheByJson(this DistributedCache dc, IEnumerable<ContentTypeCacheRefresher.JsonPayload> payloads)
        {
            var changeSet = ChangeSet.Ambient;
            if (changeSet == null)
            {
                dc.RefreshByJson(DistributedCache.ContentTypeCacheRefresherGuid, ContentTypeCacheRefresher.Serialize(payloads));
            }
            else
            {
                var buffer = changeSet.Items.ContainsKey(ContentTypeCacheBufferKey) ? (List<ContentTypeCacheRefresher.JsonPayload>)changeSet.Items[ContentTypeCacheBufferKey] : null;
                if (buffer == null) changeSet.Items[ContentTypeCacheBufferKey] = buffer = new List<ContentTypeCacheRefresher.JsonPayload>();
                buffer.AddRange(payloads);
            }
        }

        public static void RefreshContentTypeCache(this DistributedCache dc, ContentTypeServiceBase<ContentTypeRepository, IContentType>.Change[] changes)
        {
            if (changes.Length == 0) return;

            var payloads = changes
                .Select(x => new ContentTypeCacheRefresher.JsonPayload(typeof (IContentType).Name, x.Item.Id, x.ChangeTypes));

            dc.RefreshContentTypeCacheByJson(payloads);
        }

        public static void RefreshContentTypeCache(this DistributedCache dc, ContentTypeServiceBase<MediaTypeRepository, IMediaType>.Change[] changes)
        {
            if (changes.Length == 0) return;

            var payloads = changes
                .Select(x => new ContentTypeCacheRefresher.JsonPayload(typeof(IMediaType).Name, x.Item.Id, x.ChangeTypes));

            dc.RefreshContentTypeCacheByJson(payloads);
        }

        public static void RefreshContentTypeCache(this DistributedCache dc, ContentTypeServiceBase<MemberTypeRepository, IMemberType>.Change[] changes)
        {
            if (changes.Length == 0) return;

            var payloads = changes
                .Select(x => new ContentTypeCacheRefresher.JsonPayload(typeof(IMemberType).Name, x.Item.Id, x.ChangeTypes));

            dc.RefreshContentTypeCacheByJson(payloads);
        }

        #endregion

        #region Stylesheet Cache

        public static void RefreshStylesheetPropertyCache(this DistributedCache dc, global::umbraco.cms.businesslogic.web.StylesheetProperty styleSheetProperty)
        {
            if (styleSheetProperty == null) return;
            dc.Refresh(DistributedCache.StylesheetPropertyCacheRefresherGuid, styleSheetProperty.Id);
        }

        public static void RemoveStylesheetPropertyCache(this DistributedCache dc, global::umbraco.cms.businesslogic.web.StylesheetProperty styleSheetProperty)
        {
            if (styleSheetProperty == null) return;
            dc.Remove(DistributedCache.StylesheetPropertyCacheRefresherGuid, styleSheetProperty.Id);
        }

        public static void RefreshStylesheetCache(this DistributedCache dc, StyleSheet styleSheet)
        {
            if (styleSheet == null) return;
            dc.Refresh(DistributedCache.StylesheetCacheRefresherGuid, styleSheet.Id);
        }

        public static void RemoveStylesheetCache(this DistributedCache dc, StyleSheet styleSheet)
        {
            if (styleSheet == null) return;
            dc.Remove(DistributedCache.StylesheetCacheRefresherGuid, styleSheet.Id);
        }

        public static void RefreshStylesheetCache(this DistributedCache dc, Umbraco.Core.Models.Stylesheet styleSheet)
        {
            if (styleSheet == null) return;
            dc.Refresh(DistributedCache.StylesheetCacheRefresherGuid, styleSheet.Id);
        }

        public static void RemoveStylesheetCache(this DistributedCache dc, Umbraco.Core.Models.Stylesheet styleSheet)
        {
            if (styleSheet == null) return;
            dc.Remove(DistributedCache.StylesheetCacheRefresherGuid, styleSheet.Id);
        }

        #endregion

        #region Domain Cache

        public static void RefreshDomainCache(this DistributedCache dc, IDomain domain)
        {
            if (domain == null) return;
            dc.Refresh(DistributedCache.DomainCacheRefresherGuid, domain.Id);
        }

        public static void RemoveDomainCache(this DistributedCache dc, IDomain domain)
        {
            if (domain == null) return;
            dc.Remove(DistributedCache.DomainCacheRefresherGuid, domain.Id);
        }

        #endregion

        #region Language Cache

        public static void RefreshLanguageCache(this DistributedCache dc, ILanguage language)
        {
            if (language == null) return;
            dc.Refresh(DistributedCache.LanguageCacheRefresherGuid, language.Id);
        }

        public static void RemoveLanguageCache(this DistributedCache dc, ILanguage language)
        {
            if (language == null) return;
            dc.Remove(DistributedCache.LanguageCacheRefresherGuid, language.Id);
        }

        public static void RefreshLanguageCache(this DistributedCache dc, global::umbraco.cms.businesslogic.language.Language language)
        {
            if (language == null) return;
            dc.Refresh(DistributedCache.LanguageCacheRefresherGuid, language.id);
        }

        public static void RemoveLanguageCache(this DistributedCache dc, global::umbraco.cms.businesslogic.language.Language language)
        {
            if (language == null) return;
            dc.Remove(DistributedCache.LanguageCacheRefresherGuid, language.id);
        }

        #endregion
    }
}