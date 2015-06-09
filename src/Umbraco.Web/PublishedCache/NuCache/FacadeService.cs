using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Xml.Linq;
using Newtonsoft.Json;
using Umbraco.Core;
using Umbraco.Core.Cache;
using Umbraco.Core.Logging;
using Umbraco.Core.Models;
using Umbraco.Core.Models.Membership;
using Umbraco.Core.Models.Rdbms;
using Umbraco.Core.ObjectResolution;
using Umbraco.Core.Persistence;
using Umbraco.Core.Persistence.DatabaseModelDefinitions;
using Umbraco.Core.Persistence.Querying;
using Umbraco.Core.Persistence.Repositories;
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
        private readonly ServiceContext _serviceContext;
        private readonly DataSource.Database _dataSource;
        private readonly ILogger _logger;

        private readonly ContentStore _contentStore;
        private readonly ContentStore _mediaStore;
        private readonly object _storesLock = new object();

        // FIXME obviously temp! *** WHY?!
        private PublishedContentTypeCache _contentTypeCache;

        public FacadeService(ServiceContext serviceContext, DatabaseContext databaseContext, ILogger logger)
        {
            _serviceContext = serviceContext;
            _dataSource = new DataSource.Database(databaseContext);
            _logger = logger;

            _contentStore = new ContentStore();
            _mediaStore = new ContentStore();

            // fixme is that ok?
            Resolution.Frozen += (sender, args) =>
            {
                // fixme temp
                _contentTypeCache = new PublishedContentTypeCache(
                    _serviceContext.ContentTypeService,
                    _serviceContext.MediaTypeService,
                    _serviceContext.MemberTypeService);

                InitializeRepositoryEvents();

                lock (_storesLock)
                {
                    LoadContentFromDatabase();
                    LoadMediaFromDatabase();
                }
            };
        }

        private void InitializeRepositoryEvents()
        {
            // plug repository event handlers
            // these trigger within the transaction to ensure consistency
            // and are used to maintain the central, database-level XML cache
            ContentRepository.RemovedEntity += OnContentRemovedEntity;
            //ContentRepository.RemovedVersion += OnContentRemovedVersion;
            ContentRepository.RefreshedEntity += OnContentRefreshedEntity;
            MediaRepository.RemovedEntity += OnMediaRemovedEntity;
            //MediaRepository.RemovedVersion += OnMediaRemovedVersion;
            MediaRepository.RefreshedEntity += OnMediaRefreshedEntity;
            MemberRepository.RemovedEntity += OnMemberRemovedEntity;
            //MemberRepository.RemovedVersion += OnMemberRemovedVersion;
            MemberRepository.RefreshedEntity += OnMemberRefreshedEntity;

            // plug
            ContentTypeService.TxEntityRefreshed += OnContentTypeEntityRefreshed;
            MediaTypeService.TxEntityRefreshed += OnMediaTypeEntityRefreshed;
            MemberTypeService.TxEntityRefreshed += OnMemberTypeEntityRefreshed;

            // mostly to be sure - each node should have been deleted beforehand
            ContentRepository.EmptiedRecycleBin += OnEmptiedRecycleBin;
            MediaRepository.EmptiedRecycleBin += OnEmptiedRecycleBin;
        }

        #region Populate Stores

        // fixme - needs to be improved of course!
        // what happens if rebuilding while loading? fine because rebuilding is in a trx and we do RepeatableRead (not ReadCommited)
        // or... is it really OK? what if everything is removed before I read it => won't see it?!

        private void LoadContentFromDatabase()
        {
            // locks:
            // fixme - document

            using (_contentStore.Frozen)
            {
                var contentService = _serviceContext.ContentService as ContentService;
                if (contentService == null) throw new Exception("oops");

                contentService.WithReadLocked(_ => LoadContentFromDatabaseLocked());
            }
        }

        private void LoadContentFromDatabaseLocked()
        {
            // locks:
            // contentStore is frozen (1 thread)
            // content (and types) are read-locked
            // ResetFrozen will upgr. lock contentStore fixme why? frozen!
            // and then write-lock on each, so that it stil is possible (in theory) to readN

            // prefetch all the types because we cannot hit the database while reading dtos
            // and CreateContentNode gets the types from _contentTypeCache
            _contentTypeCache.PrefetchAll(PublishedItemType.Content);

            var dtos = _dataSource.GetAllContentSources();
            _contentStore.ResetFrozen(dtos.Select(CreateContentNode));
        }

        // keep these around - might be useful

        //private void LoadContentBranch(IContent content)
        //{
        //    LoadContent(content);

        //    foreach (var child in content.Children())
        //        LoadContentBranch(child);
        //}

        //private void LoadContent(IContent content)
        //{
        //    var contentService = _serviceContext.ContentService as ContentService;
        //    if (contentService == null) throw new Exception("oops");
        //    var newest = content;
        //    var published = newest.Published
        //        ? newest
        //        : (newest.HasPublishedVersion ? contentService.GetByVersion(newest.PublishedVersionGuid) : null);

        //    var contentNode = CreateContentNode(newest, published);
        //    _contentStore.Set(contentNode);
        //}

        private void LoadMediaFromDatabase()
        {
            // locks & notes: see content

            using (_mediaStore.Frozen)
            {
                var mediaService = _serviceContext.MediaService as MediaService;
                if (mediaService == null) throw new Exception("oops");

                mediaService.WithReadLocked(_ => LoadMediaFromDatabaseLocked());
            }
        }

        private void LoadMediaFromDatabaseLocked()
        {
            // locks & notes: see content

            // prefetch all the types because we cannot hit the database while reading dtos
            // and CreateMediaNode gets the types from _contentTypeCache
            _contentTypeCache.PrefetchAll(PublishedItemType.Media);

            var dtos = _dataSource.GetAllMediaSources();
            _mediaStore.ResetFrozen(dtos.Select(CreateMediaNode));
        }

        // keep these around - might be useful

        //private void LoadMediaBranch(IMedia media)
        //{
        //    LoadMedia(media);

        //    foreach (var child in media.Children())
        //        LoadMediaBranch(child);
        //}

        //private void LoadMedia(IMedia media)
        //{
        //    var mediaType = _contentTypeCache.Get(PublishedItemType.Media, media.ContentTypeId);

        //    var mediaData = new ContentData
        //    {
        //        Name = media.Name,
        //        Published = true,
        //        Version = media.Version,
        //        VersionDate = media.UpdateDate,
        //        WriterId = media.CreatorId, // what else?
        //        TemplateId = -1, // have none
        //        Properties = GetPropertyValues(media)
        //    };

        //    var mediaNode = new ContentNode(media.Id, mediaType,
        //        media.Level, media.Path, media.SortOrder,
        //        media.ParentId, media.CreateDate, media.CreatorId,
        //        null, mediaData);

        //    _mediaStore.Set(mediaNode);
        //}

        //private Dictionary<string, object> GetPropertyValues(IContentBase content)
        //{
        //    var propertyEditorResolver = PropertyEditorResolver.Current; // should inject

        //    return content
        //        .Properties
        //        .Select(property =>
        //        {
        //            var e = propertyEditorResolver.GetByAlias(property.PropertyType.PropertyEditorAlias);
        //            var v = e == null
        //                ? property.Value
        //                : e.ValueEditor.ConvertDbToString(property, property.PropertyType, _serviceContext.DataTypeService);
        //            return new KeyValuePair<string, object>(property.Alias, v);
        //        })
        //        .ToDictionary(x => x.Key, x => x.Value);
        //}

        //private ContentData CreateContentData(IContent content)
        //{
        //    return new ContentData
        //    {
        //        Name = content.Name,
        //        Published = content.Published,
        //        Version = content.Version,
        //        VersionDate = content.UpdateDate,
        //        WriterId = content.WriterId,
        //        TemplateId = content.Template == null ? -1 : content.Template.Id,
        //        Properties = GetPropertyValues(content)
        //    };
        //}

        //private ContentNode CreateContentNode(IContent newest, IContent published)
        //{
        //    var contentType = _contentTypeCache.Get(PublishedItemType.Content, newest.ContentTypeId);

        //    var draftData = newest.Published
        //        ? null
        //        : CreateContentData(newest);

        //    var publishedData = newest.Published
        //        ? CreateContentData(newest)
        //        : (published == null ? null : CreateContentData(published));

        //    var contentNode = new ContentNode(newest.Id, contentType,
        //        newest.Level, newest.Path, newest.SortOrder,
        //        newest.ParentId, newest.CreateDate, newest.CreatorId,
        //        draftData, publishedData);

        //    return contentNode;
        //}

        private ContentNode CreateContentNode(ContentSourceDto dto)
        {
            if (dto.DraftVersion != Guid.Empty && dto.DraftData == null)
                throw new Exception();

            ContentData d = null;
            ContentData p = null;

            if (dto.DraftVersion != Guid.Empty)
            {
                if (dto.DraftData == null)
                    throw new Exception("oops");
                d = new ContentData
                {
                    Name = dto.DraftName,
                    Published = false,
                    TemplateId = dto.DraftTemplateId,
                    Version = dto.DraftVersion,
                    VersionDate = dto.DraftVersionDate,
                    WriterId = dto.DraftWriterId,
                    Properties = JsonConvert.DeserializeObject<Dictionary<string, object>>(dto.DraftData)
                };
            }

            if (dto.PubVersion != Guid.Empty)
            {
                if (dto.PubData == null)
                    throw new Exception("oops");
                p = new ContentData
                {
                    Name = dto.PubName,
                    Published = true,
                    TemplateId = dto.PubTemplateId,
                    Version = dto.PubVersion,
                    VersionDate = dto.PubVersionDate,
                    WriterId = dto.PubWriterId,
                    Properties = JsonConvert.DeserializeObject<Dictionary<string, object>>(dto.PubData)
                };
            }

            var contentType = _contentTypeCache.Get(PublishedItemType.Content, dto.ContentTypeId);

            var n = new ContentNode(dto.Id,
                contentType,
                dto.Level, dto.Path, dto.SortOrder, dto.ParentId, dto.CreateDate, dto.CreatorId,
                d, p);

            return n;
        }

        private ContentNode CreateMediaNode(ContentSourceDto dto)
        {
            if (dto.PubData == null)
                throw new Exception("No data for media " + dto.Id);

            var p = new ContentData
                {
                    Name = dto.PubName,
                    Published = true,
                    TemplateId = -1,
                    Version = dto.PubVersion,
                    VersionDate = dto.PubVersionDate,
                    WriterId = dto.CreatorId, // what-else?
                    Properties = JsonConvert.DeserializeObject<Dictionary<string, object>>(dto.PubData)
                };

            var contentType = _contentTypeCache.Get(PublishedItemType.Media, dto.ContentTypeId);

            var n = new ContentNode(dto.Id,
                contentType,
                dto.Level, dto.Path, dto.SortOrder, dto.ParentId, dto.CreateDate, dto.CreatorId,
                null, p);

            return n;
        }

        #endregion

        #region Maintain Stores

        public override void Notify(ContentCacheRefresher.JsonPayload[] payloads, out bool draftChanged, out bool publishedChanged)
        {
            using (_contentStore.Frozen)
            {
                NotifyFrozen(payloads, out draftChanged, out publishedChanged);
            }

            if (draftChanged || publishedChanged)
                Facade.Current.Resync();
        }

        private void NotifyFrozen(IEnumerable<ContentCacheRefresher.JsonPayload> payloads, out bool draftChanged, out bool publishedChanged)
        {
            publishedChanged = false;
            draftChanged = false;

            var contentService = _serviceContext.ContentService as ContentService;
            if (contentService == null) throw new Exception("oops");

            // locks:
            // content (and content types) are read-locked while reading content
            // contentStore is frozen (so readable, only no new views)
            // and it can be frozen by 1 thread only at a time
            //
            // content is write-locked only when setting individual items so in
            // theory it is still possible to read content, but in practice the locker
            // being ReaderWriterLockSlim favorites writer so we might have some
            // sort of contention / reader starvation?

            // fixme
            // issue with content types HOW?

            foreach (var payload in payloads)
            {
                _logger.Debug<FacadeService>("Notified {0} for content {1}".FormatWith(payload.ChangeTypes, payload.Id));

                if (payload.ChangeTypes.HasType(TreeChangeTypes.RefreshAll))
                {
                    contentService.WithReadLocked(repository =>
                    {
                        var contents = _dataSource.GetAllContentSources().Select(CreateContentNode);
                        _contentStore.ResetFrozen(contents);
                    });

                    draftChanged = publishedChanged = true;
                    continue;
                }

                if (payload.ChangeTypes.HasType(TreeChangeTypes.Remove))
                {
                    if (_contentStore.Has(payload.Id))
                        draftChanged = publishedChanged = true;                        
                    _contentStore.Clear(payload.Id);
                    continue;
                }

                if (payload.ChangeTypes.HasTypesNone(TreeChangeTypes.RefreshNode | TreeChangeTypes.RefreshBranch))
                {
                    // ?!
                    continue;
                }

                var capture = payload;
                contentService.WithReadLocked(repository =>
                {
                    if (capture.ChangeTypes.HasType(TreeChangeTypes.RefreshBranch))
                    {
                        // fixme - should we do some RV check here?
                        var dtos = _dataSource.GetBranchContentSources(capture.Id);
                        _contentStore.Clear(capture.Id);
                        foreach (var dto in dtos)
                            _contentStore.Set(CreateContentNode(dto));
                    }
                    else
                    {
                        // fixme - should we do some RV check here?
                        var dto = _dataSource.GetContentSource(capture.Id);
                        _contentStore.Set(CreateContentNode(dto));
                    }
                });

                // fixme - cannot tell really because we're not doing RV checks
                draftChanged = publishedChanged = true;
            }
        }

        public override void Notify(MediaCacheRefresher.JsonPayload[] payloads, out bool anythingChanged)
        {
            using (_mediaStore.Frozen)
            {
                NotifyFrozen(payloads, out anythingChanged);
            }

            if (anythingChanged)
                Facade.Current.Resync();
        }

        private void NotifyFrozen(IEnumerable<MediaCacheRefresher.JsonPayload> payloads, out bool anythingChanged)
        {
            anythingChanged = false;

            var mediaService = _serviceContext.MediaService as MediaService;
            if (mediaService == null) throw new Exception("oops");

            // locks:
            // see notes for content cache refresher

            foreach (var payload in payloads)
            {
                _logger.Debug<FacadeService>("Notified {0} for media {1}".FormatWith(payload.ChangeTypes, payload.Id));

                if (payload.ChangeTypes.HasType(TreeChangeTypes.RefreshAll))
                {
                    mediaService.WithReadLocked(repository =>
                    {
                        var medias = _dataSource.GetAllMediaSources().Select(CreateMediaNode);
                        _mediaStore.ResetFrozen(medias);
                    });

                    anythingChanged = true;
                    continue;
                }

                if (payload.ChangeTypes.HasType(TreeChangeTypes.Remove))
                {
                    if (_mediaStore.Has(payload.Id))
                        anythingChanged = true;
                    _mediaStore.Clear(payload.Id);
                    continue;
                }

                if (payload.ChangeTypes.HasTypesNone(TreeChangeTypes.RefreshNode | TreeChangeTypes.RefreshBranch))
                {
                    // ?!
                    continue;
                }

                var capture = payload;
                mediaService.WithReadLocked(repository =>
                {
                    if (capture.ChangeTypes.HasType(TreeChangeTypes.RefreshBranch))
                    {
                        // fixme - should we do some RV check here?
                        var dtos = _dataSource.GetBranchMediaSources(capture.Id);
                        _mediaStore.Clear(capture.Id);
                        foreach (var dto in dtos)
                            _mediaStore.Set(CreateMediaNode(dto));
                    }
                    else
                    {
                        // fixme - should we do some RV check here?
                        var dto = _dataSource.GetMediaSource(capture.Id);
                        _mediaStore.Set(CreateMediaNode(dto));
                    }
                });

                // fixme - cannot tell really because we're not doing RV checks
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
            // locks:
            // content (and content types) are read-locked while reading content
            // contentStore is frozen (so readable, only no new views)
            // and it can be frozen by 1 thread only at a time

            var contentService = _serviceContext.ContentService as ContentService;
            if (contentService == null) throw new Exception("oops");

            using (_contentStore.Frozen)
            {
                contentService.WithReadLocked(repository =>
                {
                    var dtos = _dataSource.GetTypeContentSources(ids);
                    foreach (var dto in dtos)
                        _contentStore.Set(CreateContentNode(dto));
                });
            }
        }

        private void RefreshMediaTypes(IEnumerable<int> ids)
        {
            // locks:
            // media (and content types) are read-locked while reading media
            // mediaStore is frozen (so readable, only no new views)
            // and it can be frozen by 1 thread only at a time

            var mediaService = _serviceContext.MediaService as MediaService;
            if (mediaService == null) throw new Exception("oops");

            using (_mediaStore.Frozen)
            {
                mediaService.WithReadLocked(repository =>
                {
                    var dtos = _dataSource.GetTypeMediaSources(ids);
                    foreach (var dto in dtos)
                        _mediaStore.Set(CreateMediaNode(dto));
                });
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
                MemberCache = new MemberCache(_serviceContext.MemberService, _serviceContext.DataTypeService, _contentTypeCache), // fixme preview?!
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

        #region Handle Repository Events For Database PreCache

        // we need them to be "repository" events ie to trigger from within the repository transaction,
        // because they need to be consistent with the content that is being refreshed/removed - and that
        // should be guaranteed by a DB transaction

        private void OnContentRemovedEntity(object sender, ContentRepository.EntityChangeEventArgs args)
        {
            OnRemovedEntity(args.UnitOfWork.Database, args.Entities);
        }

        private void OnMediaRemovedEntity(object sender, MediaRepository.EntityChangeEventArgs args)
        {
            OnRemovedEntity(args.UnitOfWork.Database, args.Entities);
        }

        private void OnMemberRemovedEntity(object sender, MemberRepository.EntityChangeEventArgs args)
        {
            OnRemovedEntity(args.UnitOfWork.Database, args.Entities);
        }

        private void OnRemovedEntity(UmbracoDatabase db, IEnumerable<IContentBase> items)
        {
            foreach (var item in items)
            {
                var parms = new { id = item.Id };
                db.Execute("DELETE FROM cmsContentNu WHERE nodeId=@id", parms);
            }

            // note: could be optimized by using "WHERE nodeId IN (...)" delete clauses
        }

        private static readonly string[] PropertiesImpactingAllVersions = { "SortOrder", "ParentId", "Level", "Path", "Trashed" };

        private static bool HasChangesImpactingAllVersions(IContent icontent)
        {
            var content = (Content)icontent;

            // UpdateDate will be dirty
            // Published may be dirty if saving a Published entity
            // so cannot do this (would always be true):
            //return content.IsEntityDirty();

            // have to be more precise & specify properties
            return PropertiesImpactingAllVersions.Any(content.IsPropertyDirty);
        }

        private void OnContentRefreshedEntity(VersionableRepositoryBase<int, IContent> sender, ContentRepository.EntityChangeEventArgs args)
        {
            var db = args.UnitOfWork.Database;

            foreach (var c in args.Entities)
            {
                OnRepositoryRefreshed(db, c, false);

                // if unpublishing, remove from table
                if (((Content)c).PublishedState == PublishedState.Unpublishing)
                {
                    db.Execute("DELETE FROM cmsContentNu WHERE nodeId=@id AND published=1", new { id = c.Id });
                    continue;
                }

                // need to update the published data if we're saving the published version,
                // or having an impact on that version - we update the published data even when masked

                IContent pc = null;
                if (c.Published)
                {
                    // saving the published version = update data
                    pc = c;
                }
                else
                {
                    // saving the non-published version, but there is a published version
                    // check whether we have changes that impact the published version (move...)
                    if (c.HasPublishedVersion && HasChangesImpactingAllVersions(c))
                        pc = sender.GetByVersion(c.PublishedVersionGuid);
                }

                if (pc == null)
                    continue;

                OnRepositoryRefreshed(db, pc, true);
            }
        }

        private void OnMediaRefreshedEntity(object sender, MediaRepository.EntityChangeEventArgs args)
        {
            var db = args.UnitOfWork.Database;

            foreach (var m in args.Entities)
            {
                // for whatever reason we delete some data when the media is trashed
                // at least that's what the MediaService implementation did
                if (m.Trashed)
                    db.Execute("DELETE FROM cmsContentXml WHERE nodeId=@id", new { id = m.Id });

                OnRepositoryRefreshed(db, m, true);
            }
        }

        private void OnMemberRefreshedEntity(object sender, MemberRepository.EntityChangeEventArgs args)
        {
            var db = args.UnitOfWork.Database;

            foreach (var m in args.Entities)
            {
                OnRepositoryRefreshed(db, m, true);
            }
        }

        private void OnRepositoryRefreshed(UmbracoDatabase db, IContentBase content, bool published)
        {
            // use a custom SQL to update row version on each update
            //db.InsertOrUpdate(dto);

            var dto = GetDto(content, published);
            db.InsertOrUpdate(dto,
                "SET data=@data, rv=rv+1 WHERE nodeId=@id AND published=@published",
                new
                {
                    data = dto.Data,
                    id = dto.NodeId,
                    published = dto.Published
                });
        }

        private void OnEmptiedRecycleBin(object sender, ContentRepository.RecycleBinEventArgs args)
        {
            OnEmptiedRecycleBin(args.UnitOfWork.Database, args.NodeObjectType);
        }

        private void OnEmptiedRecycleBin(object sender, MediaRepository.RecycleBinEventArgs args)
        {
            OnEmptiedRecycleBin(args.UnitOfWork.Database, args.NodeObjectType);
        }

        // mostly to be sure - each node should have been deleted beforehand
        private void OnEmptiedRecycleBin(UmbracoDatabase db, Guid nodeObjectType)
        {
            // required by SQL-CE
            const string sql = @"DELETE FROM cmsContentNu
WHERE cmsContentNu.nodeId IN (
    SELECT id FROM umbracoNode
    WHERE trashed=1 AND nodeObjectType=@nodeObjectType
)";

            var parms = new { /*@nodeObjectType =*/ nodeObjectType };
            db.Execute(sql, parms);
        }

        private void OnDeletedContent(object sender, global::umbraco.cms.businesslogic.Content.ContentDeleteEventArgs args)
        {
            var db = args.Database;
            var parms = new { @nodeId = args.Id };
            db.Execute("DELETE FROM cmsContentNu WHERE nodeId=@nodeId", parms);
        }

        private void OnContentTypeEntityRefreshed(ContentTypeServiceBase<IContentType> sender, ContentTypeServiceBase<IContentType>.Change.EventArgs args)
        {
            // handling a transaction event that does not play well with cache...
            RepositoryBase.SetCacheEnabledForCurrentRequest(false);

            const ContentTypeServiceBase.ChangeTypes types // only for those that have been refreshed
                = ContentTypeServiceBase.ChangeTypes.RefreshMain | ContentTypeServiceBase.ChangeTypes.RefreshOther;
            var contentTypeIds = args.Changes.Where(x => x.ChangeTypes.HasTypesAny(types)).Select(x => x.Item.Id).ToArray();
            if (contentTypeIds.Any())
                RebuildContentDbCache(contentTypeIds: contentTypeIds);
        }

        private void OnMediaTypeEntityRefreshed(ContentTypeServiceBase<IMediaType> sender, ContentTypeServiceBase<IMediaType>.Change.EventArgs args)
        {
            // handling a transaction event that does not play well with cache...
            RepositoryBase.SetCacheEnabledForCurrentRequest(false);

            const ContentTypeServiceBase.ChangeTypes types // only for those that have been refreshed
                = ContentTypeServiceBase.ChangeTypes.RefreshMain | ContentTypeServiceBase.ChangeTypes.RefreshOther;
            var mediaTypeIds = args.Changes.Where(x => x.ChangeTypes.HasTypesAny(types)).Select(x => x.Item.Id).ToArray();
            if (mediaTypeIds.Any())
                RebuildMediaDbCache(contentTypeIds: mediaTypeIds);
        }

        private void OnMemberTypeEntityRefreshed(ContentTypeServiceBase<IMemberType> sender, ContentTypeServiceBase<IMemberType>.Change.EventArgs args)
        {
            // handling a transaction event that does not play well with cache...
            RepositoryBase.SetCacheEnabledForCurrentRequest(false);

            const ContentTypeServiceBase.ChangeTypes types // only for those that have been refreshed
                = ContentTypeServiceBase.ChangeTypes.RefreshMain | ContentTypeServiceBase.ChangeTypes.RefreshOther;
            var memberTypeIds = args.Changes.Where(x => x.ChangeTypes.HasTypesAny(types)).Select(x => x.Item.Id).ToArray();
            if (memberTypeIds.Any())
                RebuildMemberDbCache(contentTypeIds: memberTypeIds);
        }

        private ContentNuDto GetDto(IContentBase content, bool published)
        {
            var data = new Dictionary<string, object>();
            foreach (var prop in content.Properties)
                data[prop.Alias] = prop.Value;

            var dto = new ContentNuDto
            {
                NodeId = content.Id,
                Published = published,
                Data = JsonConvert.SerializeObject(data)
            };

            return dto;
        }

        #endregion

        #region Rebuild Database PreCache

        public void RebuildContentDbCache(int groupSize = 5000, IEnumerable<int> contentTypeIds = null)
        {
            var svc = _serviceContext.ContentService as ContentService;
            if (svc == null) throw new Exception("oops");
            svc.WithWriteLocked(repository => RebuildContentDbCacheLocked(repository, groupSize, contentTypeIds));
        }

        // assumes content tree lock
        private void RebuildContentDbCacheLocked(ContentRepository repository, int groupSize, IEnumerable<int> contentTypeIds)
        {
            var contentTypeIdsA = contentTypeIds == null ? null : contentTypeIds.ToArray();
            var contentObjectType = Guid.Parse(Constants.ObjectTypes.Document);
            var db = repository.UnitOfWork.Database;

            // remove all - if anything fails the transaction will rollback
            if (contentTypeIds == null || contentTypeIdsA.Length == 0)
            {
                // must support SQL-CE
                db.Execute(@"DELETE FROM cmsContentNu
WHERE cmsContentNu.nodeId IN (
    SELECT id FROM umbracoNode WHERE umbracoNode.nodeObjectType=@objType
)",
                    new { objType = contentObjectType });
            }
            else
            {
                // assume number of ctypes won't blow IN(...)
                // must support SQL-CE
                db.Execute(@"DELETE FROM cmsContentNu
WHERE cmsContentNu.nodeId IN (
    SELECT id FROM umbracoNode
    JOIN cmsContent ON cmsContent.nodeId=umbracoNode.id
    WHERE umbracoNode.nodeObjectType=@objType
    AND cmsContent.contentType IN (@ctypes) 
)",
                    new { objType = contentObjectType, ctypes = contentTypeIdsA });
            }

            // insert back - if anything fails the transaction will rollback
            var query = Query<IContent>.Builder;
            if (contentTypeIds != null && contentTypeIdsA.Length > 0)
                query = query.WhereIn(x => x.ContentTypeId, contentTypeIdsA); // assume number of ctypes won't blow IN(...)

            long pageIndex = 0;
            long processed = 0;
            long total;
            do
            {
                // .GetPagedResultsByQuery implicitely adds (cmsDocument.newest = 1)
                var descendants = repository.GetPagedResultsByQuery(query, pageIndex++, groupSize, out total, "Path", Direction.Ascending);
                var items = new List<ContentNuDto>();
                var guids = new List<Guid>();
                foreach (var c in descendants)
                {
                    items.Add(GetDto(c, c.Published));
                    if (c.Published == false && c.HasPublishedVersion)
                        guids.Add(c.PublishedVersionGuid);
                }
                items.AddRange(guids.Select(x => GetDto(repository.GetByVersion(x), true)));

                db.BulkInsertRecords(items, null, false); // run within the current transaction and do NOT commit
                processed += items.Count;
            } while (processed < total);
        }

        public void RebuildMediaDbCache(int groupSize = 5000, IEnumerable<int> contentTypeIds = null)
        {
            var svc = _serviceContext.MediaService as MediaService;
            if (svc == null) throw new Exception("oops");
            svc.WithWriteLocked(repository => RebuildMediaDbCacheLocked(repository, groupSize, contentTypeIds));
        }

        // assumes media tree lock
        public void RebuildMediaDbCacheLocked(MediaRepository repository, int groupSize, IEnumerable<int> contentTypeIds)
        {
            var contentTypeIdsA = contentTypeIds == null ? null : contentTypeIds.ToArray();
            var mediaObjectType = Guid.Parse(Constants.ObjectTypes.Media);
            var db = repository.UnitOfWork.Database;

            // remove all - if anything fails the transaction will rollback
            if (contentTypeIds == null || contentTypeIdsA.Length == 0)
            {
                // must support SQL-CE
                db.Execute(@"DELETE FROM cmsContentNu
WHERE cmsContentNu.nodeId IN (
    SELECT id FROM umbracoNode WHERE umbracoNode.nodeObjectType=@objType
)",
                    new { objType = mediaObjectType });
            }
            else
            {
                // assume number of ctypes won't blow IN(...)
                // must support SQL-CE
                db.Execute(@"DELETE FROM cmsContentNu
WHERE cmsContentNu.nodeId IN (
    SELECT id FROM umbracoNode
    JOIN cmsContent ON cmsContent.nodeId=umbracoNode.id
    WHERE umbracoNode.nodeObjectType=@objType
    AND cmsContent.contentType IN (@ctypes) 
)",
                    new { objType = mediaObjectType, ctypes = contentTypeIdsA });
            }

            // insert back - if anything fails the transaction will rollback
            var query = Query<IMedia>.Builder;
            if (contentTypeIds != null && contentTypeIdsA.Length > 0)
                query = query.WhereIn(x => x.ContentTypeId, contentTypeIdsA); // assume number of ctypes won't blow IN(...)

            long pageIndex = 0;
            long processed = 0;
            long total;
            do
            {
                var descendants = repository.GetPagedResultsByQuery(query, pageIndex++, groupSize, out total, "Path", Direction.Ascending);
                var items = descendants.Select(m => GetDto(m, true)).ToArray();
                db.BulkInsertRecords(items, null, false); // run within the current transaction and do NOT commit
                processed += items.Length;
            } while (processed < total);
        }

        public void RebuildMemberDbCache(int groupSize = 5000, IEnumerable<int> contentTypeIds = null)
        {
            var svc = _serviceContext.MemberService as MemberService;
            if (svc == null) throw new Exception("oops");
            svc.WithWriteLocked(repository => RebuildMemberDbCacheLocked(repository, groupSize, contentTypeIds));
        }

        // assumes member tree lock
        public void RebuildMemberDbCacheLocked(MemberRepository repository, int groupSize, IEnumerable<int> contentTypeIds)
        {
            var contentTypeIdsA = contentTypeIds == null ? null : contentTypeIds.ToArray();
            var memberObjectType = Guid.Parse(Constants.ObjectTypes.Member);
            var db = repository.UnitOfWork.Database;

            // remove all - if anything fails the transaction will rollback
            if (contentTypeIds == null || contentTypeIdsA.Length == 0)
            {
                // must support SQL-CE
                db.Execute(@"DELETE FROM cmsContentNu
WHERE cmsContentNu.nodeId IN (
    SELECT id FROM umbracoNode WHERE umbracoNode.nodeObjectType=@objType
)",
                    new { objType = memberObjectType });
            }
            else
            {
                // assume number of ctypes won't blow IN(...)
                // must support SQL-CE
                db.Execute(@"DELETE FROM cmsContentNu
WHERE cmsContentNu.nodeId IN (
    SELECT id FROM umbracoNode
    JOIN cmsContent ON cmsContent.nodeId=umbracoNode.id
    WHERE umbracoNode.nodeObjectType=@objType
    AND cmsContent.contentType IN (@ctypes) 
)",
                    new { objType = memberObjectType, ctypes = contentTypeIdsA });
            }

            // insert back - if anything fails the transaction will rollback
            var query = Query<IMember>.Builder;
            if (contentTypeIds != null && contentTypeIdsA.Length > 0)
                query = query.WhereIn(x => x.ContentTypeId, contentTypeIdsA); // assume number of ctypes won't blow IN(...)

            long pageIndex = 0;
            long processed = 0;
            long total;
            do
            {
                var descendants = repository.GetPagedResultsByQuery(query, pageIndex++, groupSize, out total, "Path", Direction.Ascending);
                var items = descendants.Select(m => GetDto(m, true)).ToArray();
                db.BulkInsertRecords(items, null, false); // run within the current transaction and do NOT commit
                processed += items.Length;
            } while (processed < total);
        }

        public bool VerifyContentDbCache()
        {
            var svc = _serviceContext.ContentService as ContentService;
            if (svc == null) throw new Exception("oops");
            return svc.WithReadLocked(x => VerifyContentDbCacheLocked(x));
        }

        // assumes content tree lock
        private bool VerifyContentDbCacheLocked(ContentRepository repository)
        {
            // every published content item should have a corresponding row in cmsContentXml
            // every content item should have a corresponding row in cmsPreviewXml

            var contentObjectType = Guid.Parse(Constants.ObjectTypes.Document);
            var db = repository.UnitOfWork.Database;

            var count = db.ExecuteScalar<int>(@"SELECT COUNT(*)
FROM umbracoNode
JOIN cmsDocument ON (umbracoNode.id=cmsDocument.nodeId AND (cmsDocument.newest=1 OR cmsDocument.published=1))
LEFT JOIN cmsContentNu ON (umbracoNode.id=cmsContentNu.nodeId AND cmsContentNu.published=cmsDocument.published)
WHERE umbracoNode.nodeObjectType=@objType
AND cmsContentNu.nodeId IS NULL;"
                , new { objType = contentObjectType });

            return count == 0;
        }

        public bool VerifyMediaDbCache()
        {
            var svc = _serviceContext.MediaService as MediaService;
            if (svc == null) throw new Exception("oops");
            return svc.WithReadLocked(x => VerifyMediaDbCacheLocked(x));
        }

        // assumes media tree lock
        public bool VerifyMediaDbCacheLocked(MediaRepository repository)
        {
            // every non-trashed media item should have a corresponding row in cmsContentXml

            var mediaObjectType = Guid.Parse(Constants.ObjectTypes.Media);
            var db = repository.UnitOfWork.Database;

            var count = db.ExecuteScalar<int>(@"SELECT COUNT(*)
FROM umbracoNode
JOIN cmsDocument ON (umbracoNode.id=cmsDocument.nodeId AND cmsDocument.published=1)
LEFT JOIN cmsContentNu ON (umbracoNode.id=cmsContentNu.nodeId AND cmsContentNu.published=1)
WHERE umbracoNode.nodeObjectType=@objType
AND cmsContentNu.nodeId IS NULL
", new { objType = mediaObjectType });

            return count == 0;
        }

        public bool VerifyMemberDbCache()
        {
            var svc = _serviceContext.MemberService as MemberService;
            if (svc == null) throw new Exception("oops");
            return svc.WithReadLocked(VerifyMemberDbCacheLocked);
        }

        // assumes member tree lock
        public bool VerifyMemberDbCacheLocked(MemberRepository repository)
        {
            // every member item should have a corresponding row in cmsContentXml

            var memberObjectType = Guid.Parse(Constants.ObjectTypes.Member);
            var db = repository.UnitOfWork.Database;

            var count = db.ExecuteScalar<int>(@"SELECT COUNT(*)
FROM umbracoNode
LEFT JOIN cmsContentNu ON (umbracoNode.id=cmsContentNu.nodeId AND cmsContentNu.published=1)
WHERE umbracoNode.nodeObjectType=@objType
AND cmsContentNu.nodeId IS NULL
", new { objType = memberObjectType });

            return count == 0;
        }

        #endregion
    }
}
