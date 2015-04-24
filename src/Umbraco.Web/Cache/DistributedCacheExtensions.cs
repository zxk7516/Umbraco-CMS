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

        // fixme - issue!
        //
        // however, as soon as more than one refresher is impacted, things become complicated,
        // because buffering all events mean that they will not be processed in an ordered way,
        // eg: change content A, change type 1, change content B
        // will be processed as (A,B) then 1, or 1 then (A,B) but cannot be A then 1 then B.
        //
        // must fix FlushChangeSet and RefreshCacheByPayload<T> - but how?
        //
        // the way the distributed cache works, the only way to get atomic changes is to group
        // these changes in one payload, which will be processed as one event by the remote
        // refresher - no way we can tell one remote that a payload is part of a greater thing.
        //
        // must let ppl do something like...
        // using (ChangeSet.WithAmbient)
        // {
        //   ...
        //   ChangeSet.FlushAmbient();
        //   ...
        //   ChangeSet.FlushAmbient();
        //   ...
        // }
        //
        // example:
        // delete a content type
        // = delete all content of that type, then delete the content type
        // WHEN would we have more than 1 buffer?
        //
        // fixme OR should a changeset auto-commit soon as we switch to another service?!

        public static void FlushChangeSet(this DistributedCache dc, ChangeSet changeSet)
        {
            if (changeSet == null) return;

            var key = DistributedCache.ContentCacheRefresherGuid.ToString();
            if (changeSet.Items.ContainsKey(key))
            {
                var buffer = (List<ContentCacheRefresher.JsonPayload>) changeSet.Items[key];
                dc.RefreshByPayload(DistributedCache.ContentCacheRefresherGuid, buffer.ToArray());
                changeSet.Items.Remove(key);
            }

            key = DistributedCache.MediaCacheRefresherGuid.ToString();
            if (changeSet.Items.ContainsKey(key))
            {
                var buffer = (List<MediaCacheRefresher.JsonPayload>) changeSet.Items[key];
                dc.RefreshByJson(DistributedCache.MediaCacheRefresherGuid, MediaCacheRefresher.Serialize(buffer.ToArray()));
                changeSet.Items.Remove(key);
            }

            key = DistributedCache.MemberCacheRefresherGuid.ToString();
            if (changeSet.Items.ContainsKey(key))
            {
                throw new NotImplementedException("ChangeSet does not support members?");
                //var buffer = (List<MemberCacheRefresher.JsonPayload>) changeSet.Items[MemberCacheBufferKey];
                //dc.RefreshByJson(DistributedCache.MemberCacheRefresherGuid, MemberCacheRefresher.Serialize(buffer));
                //changeSet.Items.Remove(MemberCacheBufferKey);
            }

            key = DistributedCache.ContentTypeCacheRefresherGuid.ToString();
            if (changeSet.Items.ContainsKey(key))
            {
                var buffer = (List<ContentTypeCacheRefresher.JsonPayload>) changeSet.Items[key];
                dc.RefreshByPayload(DistributedCache.ContentTypeCacheRefresherGuid, buffer.ToArray());
                changeSet.Items.Remove(key);
            }

            key = DistributedCache.DataTypeCacheRefresherGuid.ToString();
            if (changeSet.Items.ContainsKey(key))
            {
                var buffer = (List<DataTypeCacheRefresher.JsonPayload>) changeSet.Items[key];
                dc.RefreshByPayload(DistributedCache.DataTypeCacheRefresherGuid, buffer.ToArray());
                changeSet.Items.Remove(key);
            }
        }

        private static void RefreshCacheByPayload<T>(this DistributedCache dc, Guid refresherId, IEnumerable<T> payloads)
        {
            var changeSet = ChangeSet.Ambient;
            if (changeSet == null)
            {
                dc.RefreshByPayload(refresherId, payloads);
            }
            else
            {
                var key = refresherId.ToString();

                // fixme
                // OR just do
                // changeSet.Add(refresherId, payloads)
                // AND if refresherId != changeSet.RefresherId
                // it will first flush itself?

                var buffer = changeSet.Items.ContainsKey(key) ? (List<T>)changeSet.Items[key] : null;
                if (buffer == null) changeSet.Items[key] = buffer = new List<T>();
                buffer.AddRange(payloads);
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
            var payloads = new[] { new DataTypeCacheRefresher.JsonPayload(dataType.Id, dataType.UniqueId, false) };
            dc.RefreshCacheByPayload(DistributedCache.DataTypeCacheRefresherGuid, payloads);
        }

        public static void RemoveDataTypeCache(this DistributedCache dc, global::umbraco.cms.businesslogic.datatype.DataTypeDefinition dataType)
        {
            if (dataType == null) return;
            var payloads = new[] { new DataTypeCacheRefresher.JsonPayload(dataType.Id, dataType.UniqueId, true) };
            dc.RefreshCacheByPayload(DistributedCache.DataTypeCacheRefresherGuid, payloads);
        }

        public static void RefreshDataTypeCache(this DistributedCache dc, IDataTypeDefinition dataType)
        {
            if (dataType == null) return;
            var payloads = new[] { new DataTypeCacheRefresher.JsonPayload(dataType.Id, dataType.Key, false) };
            dc.RefreshCacheByPayload(DistributedCache.DataTypeCacheRefresherGuid, payloads);
        }

        public static void RemoveDataTypeCache(this DistributedCache dc, IDataTypeDefinition dataType)
        {
            if (dataType == null) return;
            var payloads = new[] { new DataTypeCacheRefresher.JsonPayload(dataType.Id, dataType.Key, true) };
            dc.RefreshCacheByPayload(DistributedCache.DataTypeCacheRefresherGuid, payloads);
        }

        #endregion

        #region Content cache

        /// <summary>
        /// Refreshes all published content.
        /// </summary>
        /// <param name="dc"></param>
        public static void RefreshAllPublishedContentCache(this DistributedCache dc)
        {
            var payloads = new[] { new ContentCacheRefresher.JsonPayload(0, TreeChangeTypes.RefreshAll) };

            dc.RefreshByPayload(DistributedCache.ContentCacheRefresherGuid, payloads);
        }

        public static void RefreshContentCache(this DistributedCache dc, TreeChange<IContent>[] changes)
        {
            if (changes.Length == 0) return;

            var payloads = changes
                .Select(x => new ContentCacheRefresher.JsonPayload(x.Item.Id, x.ChangeTypes));

            dc.RefreshByPayload(DistributedCache.ContentCacheRefresherGuid, payloads);
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

        public static void RefreshMediaCache(this DistributedCache dc, TreeChange<IMedia>[] changes)
        {
            if (changes.Length == 0) return;

            var payloads = changes
                .Select(x => new MediaCacheRefresher.JsonPayload(x.Item.Id, x.ChangeTypes));

            dc.RefreshCacheByPayload(DistributedCache.MediaCacheRefresherGuid, payloads);
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

        public static void RefreshContentTypeCache(this DistributedCache dc, ContentTypeServiceBase<ContentTypeRepository, IContentType>.Change[] changes)
        {
            if (changes.Length == 0) return;

            var payloads = changes
                .Select(x => new ContentTypeCacheRefresher.JsonPayload(typeof (IContentType).Name, x.Item.Id, x.ChangeTypes));

            dc.RefreshByPayload(DistributedCache.ContentTypeCacheRefresherGuid, payloads);
        }

        public static void RefreshContentTypeCache(this DistributedCache dc, ContentTypeServiceBase<MediaTypeRepository, IMediaType>.Change[] changes)
        {
            if (changes.Length == 0) return;

            var payloads = changes
                .Select(x => new ContentTypeCacheRefresher.JsonPayload(typeof(IMediaType).Name, x.Item.Id, x.ChangeTypes));

            dc.RefreshByPayload(DistributedCache.ContentTypeCacheRefresherGuid, payloads);
        }

        public static void RefreshContentTypeCache(this DistributedCache dc, ContentTypeServiceBase<MemberTypeRepository, IMemberType>.Change[] changes)
        {
            if (changes.Length == 0) return;

            var payloads = changes
                .Select(x => new ContentTypeCacheRefresher.JsonPayload(typeof(IMemberType).Name, x.Item.Id, x.ChangeTypes));

            dc.RefreshByPayload(DistributedCache.ContentTypeCacheRefresherGuid, payloads);
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