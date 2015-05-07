using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Umbraco.Core;
using Umbraco.Core.Cache;
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

        // fixme - just so that the rest builds, then kill
        #region Buckets

        private readonly ContentBucket _publishedBucket;
        private readonly ContentBucket _draftBucket;
        private readonly ContentBucket _mediaBucket;
        private readonly object _bucketsLock = new object();

        public ContentBucket PublishedBucket { get { return _publishedBucket; } }
        public ContentBucket DraftBucket { get { return _draftBucket; } }
        public ContentBucket MediaBucket { get { return _mediaBucket; } }

        #endregion


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
                LoadContent(content);
        }

        private void LoadContent(IContent content)
        {
            var contentType = _contentTypeCache.Get(PublishedItemType.Content, content.ContentTypeId);
            
            var contentData = new ContentData
            {
                Name = content.Name,
                Published = content.Published,
                Version = content.Version,
                VersionDate = content.UpdateDate,
                WriterId =  content.WriterId,
                TemplateId = content.Template == null ? -1 : content.Template.Id,
                Properties = GetPropertyValues(content)
            };

            ContentData draftData = null, publishedData = null;

            if (content.Published)
            {
                publishedData = contentData;
            }
            else
            {
                draftData = contentData;
                if (content.HasPublishedVersion)
                {
                    var content2 = ApplicationContext.Current.Services.ContentService.GetByVersion(content.PublishedVersionGuid);
                    publishedData = new ContentData
                    {
                        Name = content2.Name,
                        Published = content2.Published,
                        Version = content2.Version,
                        VersionDate = content2.UpdateDate,
                        WriterId =  content2.WriterId,
                        TemplateId = content2.Template == null ? -1 : content2.Template.Id,
                        Properties = GetPropertyValues(content2)
                    };
                }
            }

            var contentNode = new ContentNode(content.Id, contentType,
                content.Level, content.Path, content.SortOrder,
                content.ParentId, content.CreateDate, content.CreatorId,
                draftData, publishedData);

            _contentStore.Set(contentNode);

            foreach (var child in content.Children())
                LoadContent(child);
        }

        private void LoadMedia()
        {
            // fixme
            // service should be injected
            // content should be locked
            // but really we should have our own cmsContentNu table
            var rootMedia = ApplicationContext.Current.Services.MediaService.GetRootMedia();
            foreach (var media in rootMedia)
                LoadMedia(media);
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

            foreach (var child in media.Children())
                LoadMedia(child);
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

        // fixme - builds but Completely Broken
        #region Maintain Buckets

        // fixme
        // at the moment these methods are not plugged anywhere so they won't run
        //  must plug them into the right events
        // at the moment these methods are not really efficient for example when listing
        //  children... we may want to have something better (one single SQL query...)

        // fixme - need to make sense of events here ;-((((

        private void PlugEvents()
        {
            // handle medias save/delete + move
            // beware! refreshByJson has an "operation" parameter which indicates whether the media was refreshed, trashed or deleted
            // fixme: moves? sorts? can be sort-of erratic?
            // fixme: in addition, MediaCache is Completely Broken, still using some old code from Library + XML, must rewrite
            MediaCacheRefresher.CacheUpdated += (sender, args) => Debugger.Break();

            // fixme - migrate to ContentCacheRefresher
            // handle page save/...
            // fixme - when exactly?
            //UnpublishedPageCacheRefresher.CacheUpdated += (sender, args) =>
            //{
            //    UnpublishedPageCacheUpdated(sender, args);
            //    System.Diagnostics.Debugger.Break();
            //};

            // fixme - migrate to ContentCacheRefresher
            // handle page publish/unpublish + move
            // move: triggers for all moved children
            // fixme: recurse for publish?
            // fixme: what about sort?
            //PageCacheRefresher.CacheUpdated += (sender, args) =>
            //{
            //    PageCacheUpdated(sender, args);
            //    System.Diagnostics.Debugger.Break();

            //};

            MediaCacheRefresher.CacheUpdated += (sender, args) =>
            {
                MediaCacheUpdated(sender, args);
                Debugger.Break();
            };

            // should only trigger a rebuild of the published content, not its inner content data
            // at the moment, a content type change also triggers a full PageCache refresh (why?)
            ContentTypeCacheRefresher.CacheUpdated += (sender, args) => Debugger.Break();

            // should only trigger a rebuild of the published content, not its inner content data
            // also triggers when prevalues change
            DataTypeCacheRefresher.CacheUpdated += (sender, args) => Debugger.Break();
        }

        void Save(IContent content)
        {
            //lock (_bucketsLock)
            //{
            //    // fixme wtf?
            //    //if (content.Published)
            //    //    throw new InvalidOperationException("Cannot save a published content.");

            //    var contentType = _contentTypeCache.GetPublishedContentTypeById(content.ContentTypeId);
            //    var publishedContent = new PublishedContent(content, contentType /*, content.Children().Select(x => x.Id).ToArray()*/);
            //    _draftBucket.Set(publishedContent);
            //}
        }

        void Save(IMedia media)
        {
            //lock (_bucketsLock)
            //{
            //    var publishedContent = new PublishedContent(media, media.Children().Select(x => x.Id).ToArray());
            //    _mediaBucket.Set(publishedContent);
            //}
        }

        void PublishContent(int id)
        {
            lock (_bucketsLock)
            {
                var content = _draftBucket.Get(id);
                if (content == null) return;
                _draftBucket.Remove(id);
                _publishedBucket.Set(content);

            }
        }

        void UnPublishContent(int id)
        {
            lock (_bucketsLock)
            {
                var content = _publishedBucket.Get(id);
                if (content == null) return;
                _publishedBucket.Remove(content.Id);
                _draftBucket.Set(content);
            }
        }

        void RemoveContent(int id)
        {
            lock (_bucketsLock)
            {
                _publishedBucket.Remove(id);
                _draftBucket.Remove(id);
            }
        }

        void RemoveMedia(int id)
        {
            lock (_bucketsLock)
            {
                _mediaBucket.Remove(id);
            }
        }

        public override void Notify(ContentCacheRefresher.JsonPayload[] payloads, out bool draftChanged, out bool publishedChanged)
        {
            throw new NotImplementedException();
        }

        public override void Notify(MediaCacheRefresher.JsonPayload[] payloads, out bool anythingChanged)
        {
            throw new NotImplementedException();
        }

        public override void Notify(ContentTypeCacheRefresher.JsonPayload[] payloads)
        {
            throw new NotImplementedException();
        }

        public override void Notify(DataTypeCacheRefresher.JsonPayload[] payloads)
        {
            throw new NotImplementedException();
        }
        
        #endregion

        // fixme - builds but Completely Broken
        #region Handle Distributed Events for cache management

        // fixme - what about medias?!

        private void UnpublishedPageCacheUpdated(object sender, CacheRefresherEventArgs args)
        {
            switch (args.MessageType)
            {
                case MessageType.RefreshAll:
                    // ignore
                    break;
                case MessageType.RefreshById:
                    var refreshedId = (int)args.MessageObject;
                    var content = ApplicationContext.Current.Services.ContentService.GetById(refreshedId);
                    if (content != null)
                        Save(content);
                    break;
                case MessageType.RefreshByInstance:
                    var refreshedInstance = (IContent)args.MessageObject;
                    Save(refreshedInstance);
                    break;
                case MessageType.RefreshByJson:
                    var json = (string)args.MessageObject;
                    //var contentService = ApplicationContext.Current.Services.ContentService;
                    //foreach (var c in UnpublishedPageCacheRefresher.DeserializeFromJsonPayload(json)
                    //    .Select(x => contentService.GetById(x.Id))
                    //    .Where(x => x != null))
                    //{
                    //    Save(c);
                    //}
                    break;
                case MessageType.RemoveById:
                    var removedId = (int)args.MessageObject;
                    RemoveContent(removedId);
                    break;
                case MessageType.RemoveByInstance:
                    var removedInstance = (IContent)args.MessageObject;
                    RemoveContent(removedInstance.Id);
                    break;
            }
        }

        private void PageCacheUpdated(object sender, CacheRefresherEventArgs args)
        {
            switch (args.MessageType)
            {
                case MessageType.RefreshAll:
                    // fixme - not supported at the moment
                    break;
                case MessageType.RefreshById:
                    var refreshedId = (int)args.MessageObject;
                    PublishContent(refreshedId);
                    break;
                case MessageType.RefreshByInstance:
                    var refreshedInstance = (IContent)args.MessageObject;
                    PublishContent(refreshedInstance.Id);
                    break;
                case MessageType.RefreshByJson:
                    // not implemented - not a JSON cache refresher
                    break;
                case MessageType.RemoveById:
                    var removedId = (int)args.MessageObject;
                    UnPublishContent(removedId);
                    break;
                case MessageType.RemoveByInstance:
                    var removedInstance = (IContent)args.MessageObject;
                    UnPublishContent(removedInstance.Id);
                    break;
            }
        }

        private void MediaCacheUpdated(MediaCacheRefresher sender, CacheRefresherEventArgs args)
        {
            switch (args.MessageType)
            {
                case MessageType.RefreshAll:
                    // fixme - not supported at the moment
                    break;
                case MessageType.RefreshById:
                    var refreshedId = (int)args.MessageObject;
                    var media = ApplicationContext.Current.Services.MediaService.GetById(refreshedId);
                    if (media != null)
                        Save(media);
                    Save(media);
                    break;
                case MessageType.RefreshByInstance:
                    var refreshedInstance = (IMedia)args.MessageObject;
                    Save(refreshedInstance);
                    break;
                case MessageType.RefreshByJson:
                    // not implemented - not a JSON cache refresher
                    break;
                case MessageType.RemoveById:
                    var removedId = (int)args.MessageObject;
                    RemoveMedia(removedId);
                    break;
                case MessageType.RemoveByInstance:
                    var removedInstance = (IContent)args.MessageObject;
                    RemoveMedia(removedInstance.Id);
                    break;
            }
        }

        #endregion

        #region Create, Get PublishedCaches

        // fixme keeping a ref on latest view = bad?!
        private ContentView _contentView;
        private ContentView _mediaView;
        private ICacheProvider _snapshotCache;

        public override IPublishedCaches CreatePublishedCaches(string previewToken)
        {
            var preview = previewToken.IsNullOrWhiteSpace() == false;
            
            ContentView contentView, mediaView;
            lock (_storesLock)
            {
                contentView = _contentStore.GetView();
                mediaView = _mediaStore.GetView();

                if (ReferenceEquals(contentView, _contentView) == false || ReferenceEquals(mediaView, _mediaView) == false)
                {
                    _snapshotCache = new ObjectCacheRuntimeCacheProvider();
                    _contentView = contentView;
                    _mediaView = mediaView;
                }
            }

            var memberCache = new MemberCache(_memberService, _dataTypeService, _contentTypeCache);
            return new Facade(preview, memberCache, contentView, mediaView, _snapshotCache);
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
