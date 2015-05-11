using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Umbraco.Core;
using Umbraco.Core.Cache;
using Umbraco.Core.Logging;
using Umbraco.Core.Models;
using Umbraco.Core.Models.Membership;
using Umbraco.Core.ObjectResolution;
using Umbraco.Core.PropertyEditors;
using Umbraco.Core.Services;
using Umbraco.Core.Sync;
using Umbraco.Web.Cache;
using Umbraco.Web.PublishedCache.NuCache.DataSource;
using Umbraco.Web.PublishedCache.XmlPublishedCache;

namespace Umbraco.Web.PublishedCache.NuCache
{
    class FacadeService : PublishedCachesServiceBase
    {
        private readonly IMemberService _memberService;
        private readonly IDataTypeService _dataTypeService;

        private readonly ContentStore _contentStore;
        private readonly ContentStore _mediaStore;
        private readonly object _storesLock = new object();

        public FacadeService(IMemberService memberService, IDataTypeService dataTypeService)
        {
            _memberService = memberService;
            _dataTypeService = dataTypeService;

            _contentStore = new ContentStore();
            _mediaStore = new ContentStore();

            // fixme - missing events

            // fixme is that ok?
            Resolution.Frozen += (sender, args) =>
            {
                // fixme temp
                _contentTypeCache = new PublishedContentTypeCache(
                    ApplicationContext.Current.Services.ContentTypeService,
                    ApplicationContext.Current.Services.MediaTypeService,
                    ApplicationContext.Current.Services.MemberTypeService);

                lock (_storesLock)
                {
                    LoadContent();
                    LoadMedia();
                }
            };
        }

        // FIXME obviously temp!
        private PublishedContentTypeCache _contentTypeCache;

        // fixme - needs to be improved of course!
        #region Populate Stores

        private void LoadContent()
        {
            // fixme
            // service should be injected
            // content should be locked
            // but really we should have our own cmsContentNu table
            var rootContent = ApplicationContext.Current.Services.ContentService.GetRootContent();
            foreach (var content in rootContent)
                LoadContentBranch(content);
        }

        private void LoadContentBranch(IContent content)
        {
            LoadContent(content);

            foreach (var child in content.Children())
                LoadContentBranch(child);
        }

        private void LoadContent(IContent content)
        {
            var contentService = ApplicationContext.Current.Services.ContentService; // fixme inject
            var newest = content;
            var published = newest.Published
                ? newest
                : (newest.HasPublishedVersion ? contentService.GetByVersion(newest.PublishedVersionGuid) : null);

            var contentNode = CreateContentNode(newest, published);
            _contentStore.Set(contentNode);
        }

        private ContentData CreateContentData(IContent content)
        {
            return new ContentData
            {
                Name = content.Name,
                Published = content.Published,
                Version = content.Version,
                VersionDate = content.UpdateDate,
                WriterId = content.WriterId,
                TemplateId = content.Template == null ? -1 : content.Template.Id,
                Properties = GetPropertyValues(content)
            };
        }

        private ContentNode CreateContentNode(IContent newest, IContent published)
        {
            var contentType = _contentTypeCache.Get(PublishedItemType.Content, newest.ContentTypeId);

            var draftData = newest.Published 
                ? null 
                : CreateContentData(newest);

            var publishedData = newest.Published 
                ? CreateContentData(newest) 
                : (published == null ? null : CreateContentData(published));

            var contentNode = new ContentNode(newest.Id, contentType,
                newest.Level, newest.Path, newest.SortOrder,
                newest.ParentId, newest.CreateDate, newest.CreatorId,
                draftData, publishedData);

            return contentNode;
        }

        private void LoadMedia()
        {
            // fixme
            // service should be injected
            // content should be locked
            // but really we should have our own cmsContentNu table
            var rootMedia = ApplicationContext.Current.Services.MediaService.GetRootMedia();
            foreach (var media in rootMedia)
                LoadMediaBranch(media);
        }

        private void LoadMediaBranch(IMedia media)
        {
            LoadMedia(media);

            foreach (var child in media.Children())
                LoadMediaBranch(child);
        }

        private void LoadMedia(IMedia media)
        {
            var mediaType = _contentTypeCache.Get(PublishedItemType.Media, media.ContentTypeId);

            var mediaData = new ContentData
            {
                Name = media.Name,
                Published = true,
                Version = media.Version,
                VersionDate = media.UpdateDate,
                WriterId = media.CreatorId, // what else?
                TemplateId = -1, // have none
                Properties = GetPropertyValues(media)
            };

            var mediaNode = new ContentNode(media.Id, mediaType,
                media.Level, media.Path, media.SortOrder,
                media.ParentId, media.CreateDate, media.CreatorId,
                null, mediaData);

            _mediaStore.Set(mediaNode);
        }

        private Dictionary<string, object> GetPropertyValues(IContentBase content)
        {
            var propertyEditorResolver = PropertyEditorResolver.Current; // FIXME inject

            return content
                .Properties
                .Select(property =>
                {
                    var e = propertyEditorResolver.GetByAlias(property.PropertyType.PropertyEditorAlias);
                    var v = e == null
                        ? property.Value
                        : e.ValueEditor.ConvertDbToString(property, property.PropertyType, _dataTypeService);
                    return new KeyValuePair<string, object>(property.Alias, v);
                })
                .ToDictionary(x => x.Key, x => x.Value);
        }

        #endregion

        #region Maintain Stores

        public override void Notify(ContentCacheRefresher.JsonPayload[] payloads, out bool draftChanged, out bool publishedChanged)
        {
            _contentStore.Freeze(true);
            try
            {
                NotifyFrozen(payloads, out draftChanged, out publishedChanged);
            }
            finally
            {
                _contentStore.Freeze(false);
            }

            if (draftChanged || publishedChanged)
                Facade.Current.Resync();
        }

        private void NotifyFrozen(ContentCacheRefresher.JsonPayload[] payloads, out bool draftChanged, out bool publishedChanged)
        {
            publishedChanged = false;
            draftChanged = false;

            foreach (var payload in payloads)
            {
                // fixme - inject logger
                LogHelper.Debug<FacadeService>("Notified {0} for content {1}".FormatWith(payload.ChangeTypes, payload.Id));

                if (payload.ChangeTypes.HasType(TreeChangeTypes.RefreshAll))
                {
                    // fixme - we don't have a "reload all" method at the moment
                    throw new NotImplementedException();
                    draftChanged = publishedChanged = true;
                    continue;
                }

                if (payload.ChangeTypes.HasType(TreeChangeTypes.Remove))
                {
                    if (_contentStore.Has(payload.Id))
                        draftChanged = publishedChanged = true;                        
                    _contentStore.Clear(payload.Id); // fixme missing locks obviously
                    continue;
                }

                if (payload.ChangeTypes.HasTypesNone(TreeChangeTypes.RefreshNode | TreeChangeTypes.RefreshBranch))
                {
                    // ?!
                    continue;
                }

                var contentService = ApplicationContext.Current.Services.ContentService as ContentService; // fixme inject
                if (contentService == null) throw new Exception("oops");
                var capture = payload;
                contentService.WithReadLocked(repository =>
                {
                    if (capture.ChangeTypes.HasType(TreeChangeTypes.RefreshBranch))
                        LoadContentBranch(contentService.GetById(capture.Id));
                    else
                        LoadContent(contentService.GetById(capture.Id));
                });

                // fixme
                // at the moment we have no way to tell, really, because we have no ROW VERSION of
                // any sort - that will come when we have the cmsContentNu table - l8tr
                draftChanged = publishedChanged = true;
            }
        }

        public override void Notify(MediaCacheRefresher.JsonPayload[] payloads, out bool anythingChanged)
        {
            _mediaStore.Freeze(true);
            try
            {
                NotifyFrozen(payloads, out anythingChanged);
            }
            finally
            {
                _mediaStore.Freeze(false);
            }

            if (anythingChanged)
                Facade.Current.Resync();
        }

        private void NotifyFrozen(MediaCacheRefresher.JsonPayload[] payloads, out bool anythingChanged)
        {
            anythingChanged = false;

            foreach (var payload in payloads)
            {
                // fixme - inject logger
                LogHelper.Debug<FacadeService>("Notified {0} for media {1}".FormatWith(payload.ChangeTypes, payload.Id));

                if (payload.ChangeTypes.HasType(TreeChangeTypes.RefreshAll))
                {
                    // fixme - we don't have a "reload all" method at the moment
                    throw new NotImplementedException();
                    anythingChanged = true;
                    continue;
                }

                if (payload.ChangeTypes.HasType(TreeChangeTypes.Remove))
                {
                    if (_mediaStore.Has(payload.Id))
                        anythingChanged = true;
                    _mediaStore.Clear(payload.Id); // fixme missing locks obviously
                    continue;
                }

                if (payload.ChangeTypes.HasTypesNone(TreeChangeTypes.RefreshNode | TreeChangeTypes.RefreshBranch))
                {
                    // ?!
                    continue;
                }

                var mediaService = ApplicationContext.Current.Services.MediaService as MediaService; // fixme inject
                if (mediaService == null) throw new Exception("oops");
                var capture = payload;
                mediaService.WithReadLocked(repository =>
                {
                    if (capture.ChangeTypes.HasType(TreeChangeTypes.RefreshBranch))
                        LoadMediaBranch(mediaService.GetById(capture.Id));
                    else
                        LoadMedia(mediaService.GetById(capture.Id));
                });

                // fixme
                // at the moment we have no way to tell, really, because we have no ROW VERSION of
                // any sort - that will come when we have the cmsContentNu table - l8tr
                anythingChanged = true;
            }
        }

        public override void Notify(ContentTypeCacheRefresher.JsonPayload[] payloads)
        {
            // see ContentTypeServiceBase
            // in all cases we just want to clear the content type cache
            // the type will be reloaded if/when needed
            foreach (var payload in payloads)
                _contentTypeCache.ClearContentType(payload.Id);

            // process content types / content cache
            // only those that have been changed - with impact on content - RefreshMain
            // for those that have been removed, content is removed already
            var ids = payloads
                .Where(x => x.ItemType == typeof(IContentType).Name && x.ChangeTypes.HasType(ContentTypeServiceBase.ChangeTypes.RefreshMain))
                .Select(x => x.Id)
                .ToArray();

            foreach (var payload in payloads)
                LogHelper.Debug<XmlStore>("Notified {0} for content type {1}".FormatWith(payload.ChangeTypes, payload.Id));

            if (ids.Length > 0) // must have refreshes, not only removes
                RefreshContentTypes(ids);

            ids = payloads
                .Where(x => x.ItemType == typeof(IMediaType).Name && x.ChangeTypes.HasType(ContentTypeServiceBase.ChangeTypes.RefreshMain))
                .Select(x => x.Id)
                .ToArray();

            foreach (var payload in payloads)
                LogHelper.Debug<FacadeService>("Notified {0} for media type {1}".FormatWith(payload.ChangeTypes, payload.Id));

            if (ids.Length > 0) // must have refreshes, not only removes
                RefreshMediaTypes(ids);

            // fixme - members? (what about XmlStore?)

            Facade.Current.Resync();
        }

        public override void Notify(DataTypeCacheRefresher.JsonPayload[] payloads)
        {
            // see above
            // in all cases we just want to clear the content type cache
            // the types will be reloaded if/when needed
            foreach (var payload in payloads)
                _contentTypeCache.ClearDataType(payload.Id);

            foreach (var payload in payloads)
                LogHelper.Debug<FacadeService>("Notified {0} for data type {1}".FormatWith(payload.Removed ? "Removed" : "Refreshed", payload.Id));

            // fixme - so we've cleared out internal cache BUT what about content?
            // will change ONLY when a content is changed and THEN we'll have an issue because of bad REFRESH of content type?!
            // we DONT need to reload content from database because it has not changed BUT we need to update it anyways!!
            throw new NotImplementedException("this is bad");

            // fixme XmlStore says... BUT what's refreshing the caches then?!
            // ignore media and member types - we're not caching them

            // ???
            //Facade.Current.Resync();
        }
        
        #endregion

        #region Manage change

        private void RefreshContentTypes(IEnumerable<int> ids)
        {
            // fixme locks

            var contentService = ApplicationContext.Current.Services.ContentService as ContentService; // fixme inject
            if (contentService == null) throw new Exception("oops");

            _contentStore.Freeze(true);
            try
            {
                foreach (var id in ids)
                {
                    var capture = id;
                    contentService.WithReadLocked(repository =>
                    {
                        var contents = contentService.GetContentOfContentType(capture); // fixme - use repository query IN (ids)
                        foreach (var content in contents)
                            LoadContent(content);
                    });
                }
            }
            finally
            {
                _contentStore.Freeze(false);
            }
        }

        private void RefreshMediaTypes(IEnumerable<int> ids)
        {
            // fixme locks

            var mediaService = ApplicationContext.Current.Services.MediaService as MediaService; // fixme inject
            if (mediaService == null) throw new Exception("oops");

            _mediaStore.Freeze(true);
            try
            {
                foreach (var id in ids)
                {
                    var capture = id;
                    mediaService.WithReadLocked(repository =>
                    {
                        var medias = mediaService.GetMediaOfMediaType(capture); // fixme - use repository query IN (ids)
                        foreach (var media in medias)
                            LoadMedia(media);
                    });
                }
            }
            finally
            {
                _mediaStore.Freeze(false);
            }
        }

        #endregion

        #region Create, Get Facade

        // use weak refs so nothing prevents the views from being GC
        private readonly WeakReference<ContentView> _contentViewRef = new WeakReference<ContentView>(null);
        private readonly WeakReference<ContentView> _mediaViewRef = new WeakReference<ContentView>(null);
        private ICacheProvider _snapshotCache;

        public override IPublishedCaches CreatePublishedCaches(string previewToken)
        {
            var preview = previewToken.IsNullOrWhiteSpace() == false;
            return new Facade(this, preview);
        }

        public Facade.FacadeElements GetElements(bool defaultPreview)
        {
            ContentView contentView, mediaView;
            ICacheProvider snapshotCache;
            lock (_storesLock)
            {
                contentView = _contentStore.GetView();
                mediaView = _mediaStore.GetView();
                snapshotCache = _snapshotCache;

                // create a new snapshot cache if the views have been GC, or have changed
                ContentView prevContentView, prevMediaView;
                if (_contentViewRef.TryGetTarget(out prevContentView) == false
                    || ReferenceEquals(prevContentView, contentView) == false
                    || _mediaViewRef.TryGetTarget(out prevMediaView) == false
                    || ReferenceEquals(prevMediaView, mediaView) == false)
                {
                    _contentViewRef.SetTarget(contentView);
                    _mediaViewRef.SetTarget(mediaView);
                    snapshotCache = _snapshotCache = new ObjectCacheRuntimeCacheProvider();
                }
            }

            return new Facade.FacadeElements
            {
                ContentCache = new ContentCache(defaultPreview, contentView),
                MediaCache = new MediaCache(defaultPreview, mediaView),
                MemberCache = new MemberCache(_memberService, _dataTypeService, _contentTypeCache), // fixme preview?!
                SnapshotCache = snapshotCache
            };
        }

        #endregion

        #region Preview

        public override string EnterPreview(IUser user, int contentId)
        {
            return "preview"; // anything
        }

        public override void RefreshPreview(string previewToken, int contentId)
        {
            // nothing
        }

        public override void ExitPreview(string previewToken)
        {
            // nothing
        }

        #endregion
    }
}
