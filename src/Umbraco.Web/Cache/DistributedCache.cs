using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Hosting;
using Umbraco.Core;
using Umbraco.Core.Cache;
using Umbraco.Core.Sync;
using umbraco.interfaces;

namespace Umbraco.Web.Cache
{
    /// <summary>
    /// Represents the entry point into Umbraco's distributed cache infrastructure.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The distributed cache infrastructure ensures that distributed caches are
    /// invalidated properly in load balancing environments.
    /// </para>
    /// <para>
    /// Distribute caches include static (in-memory) cache, runtime cache, front-end content cache, Examine/Lucene indexes
    /// </para>
    /// </remarks>
    public sealed class DistributedCache
    {
        #region Public constants/Ids

        public const string ApplicationTreeCacheRefresherId = "0AC6C028-9860-4EA4-958D-14D39F45886E";
        public const string ApplicationCacheRefresherId = "B15F34A1-BC1D-4F8B-8369-3222728AB4C8";
        public const string TemplateRefresherId = "DD12B6A0-14B9-46e8-8800-C154F74047C8";
        public const string MemberCacheRefresherId = "E285DF34-ACDC-4226-AE32-C0CB5CF388DA";
        public const string MemberGroupCacheRefresherId = "187F236B-BD21-4C85-8A7C-29FBA3D6C00C";
        public const string MediaCacheRefresherId = "B29286DD-2D40-4DDB-B325-681226589FEC";
        public const string MacroCacheRefresherId = "7B1E683C-5F34-43dd-803D-9699EA1E98CA";
        public const string UserCacheRefresherId = "E057AF6D-2EE6-41F4-8045-3694010F0AA6";
        public const string UserPermissionsCacheRefresherId = "840AB9C5-5C0B-48DB-A77E-29FE4B80CD3A";
        public const string UserTypeCacheRefresherId = "7E707E21-0195-4522-9A3C-658CC1761BD4";
        public const string ContentTypeCacheRefresherId = "6902E22C-9C10-483C-91F3-66B7CAE9E2F5";
        public const string LanguageCacheRefresherId = "3E0F95D8-0BE5-44B8-8394-2B8750B62654";
        public const string DomainCacheRefresherId = "11290A79-4B57-4C99-AD72-7748A3CF38AF";
        public const string StylesheetCacheRefresherId = "E0633648-0DEB-44AE-9A48-75C3A55CB670";
        public const string StylesheetPropertyCacheRefresherId = "2BC7A3A4-6EB1-4FBC-BAA3-C9E7B6D36D38";
        public const string DataTypeCacheRefresherId = "35B16C25-A17E-45D7-BC8F-EDAB1DCC28D2";
        public const string DictionaryCacheRefresherId = "D1D7E227-F817-4816-BFE9-6C39B6152884";
        public const string PublicAccessCacheRefresherId = "1DB08769-B104-4F8B-850E-169CAC1DF2EC";
        public const string ContentCacheRefresherId = "900A4FBE-DF3C-41E6-BB77-BE896CD158EA";

        public static readonly Guid ApplicationTreeCacheRefresherGuid = new Guid(ApplicationTreeCacheRefresherId);
        public static readonly Guid ApplicationCacheRefresherGuid = new Guid(ApplicationCacheRefresherId);
        public static readonly Guid TemplateRefresherGuid = new Guid(TemplateRefresherId);
        public static readonly Guid MemberCacheRefresherGuid = new Guid(MemberCacheRefresherId);
        public static readonly Guid MemberGroupCacheRefresherGuid = new Guid(MemberGroupCacheRefresherId);
        public static readonly Guid MediaCacheRefresherGuid = new Guid(MediaCacheRefresherId);
        public static readonly Guid MacroCacheRefresherGuid = new Guid(MacroCacheRefresherId);
        public static readonly Guid UserCacheRefresherGuid = new Guid(UserCacheRefresherId);
        public static readonly Guid UserPermissionsCacheRefresherGuid = new Guid(UserPermissionsCacheRefresherId);
        public static readonly Guid UserTypeCacheRefresherGuid = new Guid(UserTypeCacheRefresherId);
        public static readonly Guid ContentTypeCacheRefresherGuid = new Guid(ContentTypeCacheRefresherId);
        public static readonly Guid LanguageCacheRefresherGuid = new Guid(LanguageCacheRefresherId);
        public static readonly Guid DomainCacheRefresherGuid = new Guid(DomainCacheRefresherId);
        public static readonly Guid StylesheetCacheRefresherGuid = new Guid(StylesheetCacheRefresherId);
        public static readonly Guid StylesheetPropertyCacheRefresherGuid = new Guid(StylesheetPropertyCacheRefresherId);
        public static readonly Guid DataTypeCacheRefresherGuid = new Guid(DataTypeCacheRefresherId);
        public static readonly Guid DictionaryCacheRefresherGuid = new Guid(DictionaryCacheRefresherId);
        public static readonly Guid PublicAccessCacheRefresherGuid = new Guid(PublicAccessCacheRefresherId);
        public static readonly Guid ContentCacheRefresherGuid = new Guid(ContentCacheRefresherId);

        #endregion

        #region Constructor & Singleton

        // note - should inject into the application instead of using a singleton
        private static readonly DistributedCache InstanceObject = new DistributedCache();

        /// <summary>
        /// Initializes a new instance of the <see cref="DistributedCache"/> class.
        /// </summary>
        private DistributedCache()
        { }
        
        /// <summary>
        /// Gets the static unique instance of the <see cref="DistributedCache"/> class.
        /// </summary>
        /// <returns>The static unique instance of the <see cref="DistributedCache"/> class.</returns>
        /// <remarks>Exists so that extension methods can be added to the distributed cache.</remarks>
        public static DistributedCache Instance
        {
            get
            {
                return InstanceObject;    
            }
        }

        #endregion

        #region Core notification methods

        internal IServerMessenger Messenger
        {
            get { return ServerMessengerResolver.Current.Messenger; }
        }

        private IEnumerable<IServerAddress> Registrations
        {
            get { return ServerRegistrarResolver.Current.Registrar.Registrations; }
        }

        /// <summary>
        /// Notifies the distributed cache of specifieds item invalidation, for a specified <see cref="ICacheRefresher"/>.
        /// </summary>
        /// <typeparam name="T">The type of the invalidated items.</typeparam>
        /// <param name="refresherGuid">The unique identifier of the ICacheRefresher.</param>
        /// <param name="getNumericId">A function returning the unique identifier of items.</param>
        /// <param name="instances">The invalidated items.</param>
        /// <remarks>
        /// This method is much better for performance because it does not need to re-lookup object instances.
        /// </remarks>
        public void Refresh<T>(Guid refresherGuid, Func<T, int> getNumericId, params T[] instances)
        {
            if (refresherGuid == Guid.Empty || instances.Length == 0 || getNumericId == null) return;

            Messenger.PerformRefresh(
                Registrations,
                GetRefresherById(refresherGuid),
                getNumericId,
                instances);
        }

        /// <summary>
        /// Notifies the distributed cache of a specified item invalidation, for a specified <see cref="ICacheRefresher"/>.
        /// </summary>
        /// <param name="refresherGuid">The unique identifier of the ICacheRefresher.</param>
        /// <param name="id">The unique identifier of the invalidated item.</param>
        public void Refresh(Guid refresherGuid, int id)
        {
            if (refresherGuid == Guid.Empty || id == default(int)) return;

            Messenger.PerformRefresh(
                Registrations, 
                GetRefresherById(refresherGuid), 
                id);
        }

        /// <summary>
        /// Notifies the distributed cache of a specified item invalidation, for a specified <see cref="ICacheRefresher"/>.
        /// </summary>
        /// <param name="refresherGuid">The unique identifier of the ICacheRefresher.</param>
        /// <param name="id">The unique identifier of the invalidated item.</param>
        public void Refresh(Guid refresherGuid, Guid id)
        {
            if (refresherGuid == Guid.Empty || id == Guid.Empty) return;

            Messenger.PerformRefresh(
                Registrations,
                GetRefresherById(refresherGuid),
                id);
        }

        // payload should be an object, or array of objects, NOT a
        // Linq enumerable of some sort (IEnumerable, query...) 
        public void RefreshByPayload(Guid refresherGuid, object payload)
        {
            if (refresherGuid == Guid.Empty || payload == null) return;

            Messenger.PerformRefresh(
                Registrations,
                GetRefresherById(refresherGuid),
                payload);
        }

        public void RefreshByPayload<T>(Guid refresherGuid, IEnumerable<T> payloads)
            where T : class
        {
            if (refresherGuid == Guid.Empty || payloads == null) return;

            var payloadsA = payloads.GetType().IsArray
                ? payloads
                : payloads.ToArray();

            RefreshByPayload(refresherGuid, (object) payloadsA);
        }

        public void RefreshSetByPayload<T>(Guid refresherGuid, IEnumerable<T> payloads)
            where T : class // else cannot add to changeSet IEnumerable<object>
        {
            if (refresherGuid == Guid.Empty || payloads == null) return;

            var changeSet = ChangeSet.Ambient;
            if (changeSet == null)
                RefreshByPayload(refresherGuid, payloads);
            else
                changeSet.Add(refresherGuid, payloads);
        }

        /// <summary>
        /// Notifies the distributed cache, for a specified <see cref="ICacheRefresher"/>.
        /// </summary>
        /// <param name="refresherGuid">The unique identifier of the ICacheRefresher.</param>
        /// <param name="jsonPayload">The notification content.</param>
        public void RefreshByJson(Guid refresherGuid, string jsonPayload)
        {
            if (refresherGuid == Guid.Empty || jsonPayload.IsNullOrWhiteSpace()) return;

            Messenger.PerformRefresh(
                Registrations,
                GetRefresherById(refresherGuid),
                jsonPayload);
        }

        ///// <summary>
        ///// Notifies the distributed cache, for a specified <see cref="ICacheRefresher"/>.
        ///// </summary>
        ///// <param name="refresherId">The unique identifier of the ICacheRefresher.</param>
        ///// <param name="payload">The notification content.</param>
        //internal void Notify(Guid refresherId, object payload)
        //{
        //    if (refresherId == Guid.Empty || payload == null) return;

        //    Messenger.Notify(
        //        Registrations,
        //        GetRefresherById(refresherId),
        //        json);
        //}

        /// <summary>
        /// Notifies the distributed cache of a global invalidation for a specified <see cref="ICacheRefresher"/>.
        /// </summary>
        /// <param name="refresherGuid">The unique identifier of the ICacheRefresher.</param>
        public void RefreshAll(Guid refresherGuid)
        {
            if (refresherGuid == Guid.Empty) return;

            Messenger.PerformRefreshAll(
                Registrations,
                GetRefresherById(refresherGuid));
        }

        /// <summary>
        /// Notifies the distributed cache of a specified item removal, for a specified <see cref="ICacheRefresher"/>.
        /// </summary>
        /// <param name="refresherGuid">The unique identifier of the ICacheRefresher.</param>
        /// <param name="id">The unique identifier of the removed item.</param>
        public void Remove(Guid refresherGuid, int id)
        {
            if (refresherGuid == Guid.Empty || id == default(int)) return;

            Messenger.PerformRemove(
                Registrations,
                GetRefresherById(refresherGuid),
                id);
        }

        /// <summary>
        /// Notifies the distributed cache of specifieds item removal, for a specified <see cref="ICacheRefresher"/>.
        /// </summary>
        /// <typeparam name="T">The type of the removed items.</typeparam>
        /// <param name="refresherGuid">The unique identifier of the ICacheRefresher.</param>
        /// <param name="getNumericId">A function returning the unique identifier of items.</param>
        /// <param name="instances">The removed items.</param>
        /// <remarks>
        /// This method is much better for performance because it does not need to re-lookup object instances.
        /// </remarks>
        public void Remove<T>(Guid refresherGuid, Func<T, int> getNumericId, params T[] instances)
        {
            Messenger.PerformRemove(
                Registrations,
                GetRefresherById(refresherGuid),
                getNumericId,
                instances);
        }

        #endregion

        // helper method to get an ICacheRefresher by its unique identifier
        private static ICacheRefresher GetRefresherById(Guid refresherGuid)
        {
            var refresher = CacheRefreshersResolver.Current.GetById(refresherGuid);
            if (refresher == null)
                throw new ArgumentException("Not a registered cache refresher UID: {0}".FormatWith(refresherGuid));
            return refresher;
        }
    }
}
