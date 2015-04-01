using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Hosting;
using System.Xml;
using System.Xml.Linq;
using Umbraco.Core;
using Umbraco.Core.Cache;
using Umbraco.Core.Configuration;
using Umbraco.Core.IO;
using Umbraco.Core.Logging;
using Umbraco.Core.Models;
using Umbraco.Core.Models.Rdbms;
using Umbraco.Core.ObjectResolution;
using Umbraco.Core.Persistence;
using Umbraco.Core.Persistence.DatabaseModelDefinitions;
using Umbraco.Core.Persistence.Querying;
using Umbraco.Core.Persistence.Repositories;
using Umbraco.Core.Profiling;
using Umbraco.Core.Services;
using Umbraco.Core.Sync;
using Umbraco.Web.Cache;
using Umbraco.Web.Scheduling;

namespace Umbraco.Web.PublishedCache.XmlPublishedCache
{
    /// <summary>
    /// Represents the Xml storage for the Xml published cache.
    /// </summary>
    /// <remarks>
    /// <para>One instance of <see cref="XmlStore"/> is instanciated by the <see cref="PublishedCachesService"/> and
    /// then passed to all <see cref="PublishedContentCache"/> instances that are created (one per request).</para>
    /// <para>This class should *not* be public.</para>
    /// </remarks>
    class XmlStore : IDisposable
    {
        private Func<IContent, XElement> _xmlContentSerializer;
        private Func<IMember, XElement> _xmlMemberSerializer;
        private Func<IMedia, XElement> _xmlMediaSerializer;
        private XmlStoreFilePersister _persisterTask;
        private BackgroundTaskRunner<XmlStoreFilePersister> _filePersisterRunner;
        private bool _withRepositoryEvents;
        private bool _withOtherEvents;

        private readonly RoutesCache _routesCache;
        private readonly ServiceContext _serviceContext;
        private readonly DatabaseContext _databaseContext;

        // NOTES
        //
        // LOCKS
        //
        //  we make use of various locking strategies in XmlStore:
        //
        //  _xmlLock is an AsyncLock that is used to protect _xml ie the master xml document
        //    from concurrent accesses. It is an AsyncLock because saving xml to file can be
        //    an async operation. It is never used directly but only through GetSafeXmlReader
        //    and GetSafeXmlWriter which provide safe, locked, clean access to _xml.
        //
        // there is no lock around the xml file specifically, because all accesses to that
        //    file happen while holding the _xmlLock lock.
        //
        // there is not lock around writes to cmsContentXml and cmsPreviewXml as these are
        //    supposed to happen in the context of the original ContentRepository transaction
        //    and therefore concurrency is already taken care of.
        //
        // there is no lock around the database, because writes are already protected (see
        //    above) and reads are atomic (only 1 query).
        //
        // FIXME
        //    when 'rebuilding all' we should somehow ensure that content does not change
        //    while rebuilding - but that's a ContentService-level lock? How can we do it?
        //    in addition at one point in time, we're going to remove all XML and then
        //    recreate it, we should ensure that NO other thread, even in a distributed
        //    environment, can read the XML tables at that time - so we should have a
        //    global, distributed lock on everything...

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="XmlStore"/> class.
        /// </summary>
        /// <remarks>The default constructor will boot the cache, load data from file or database,
        /// wire events in order to manage changes, etc.</remarks>
        public XmlStore(ServiceContext serviceContext, DatabaseContext databaseContext, RoutesCache routesCache)
            : this(serviceContext, databaseContext, routesCache, false, false)
        { }

        // internal for unit tests
        // no file nor db, no config check
        internal XmlStore(ServiceContext serviceContext, DatabaseContext databaseContext, RoutesCache routesCache,
            bool testing, bool enableRepositoryEvents)
        {
            if (testing == false)
                EnsureConfigurationIsValid();

            _serviceContext = serviceContext;
            _databaseContext = databaseContext;
            _routesCache = routesCache;

            // initialize file lock
            // ApplicationId will look like "/LM/W3SVC/1/Root/AppName"
            // name is system-wide and must be less than 260 chars
            //
            // From MSDN C++ CreateSemaphore doc:
            // "The name can have a "Global\" or "Local\" prefix to explicitly create the object in
            // the global or session namespace. The remainder of the name can contain any character 
            // except the backslash character (\). For more information, see Kernel Object Namespaces."
            //
            // From MSDN "Kernel object namespaces" doc:
            // "The separate client session namespaces enable multiple clients to run the same 
            // applications without interfering with each other. For processes started under 
            // a client session, the system uses the session namespace by default. However, these 
            // processes can use the global namespace by prepending the "Global\" prefix to the object name."
            //
            // just use "default" (whatever it is) for now - ie, no prefix
            //
            var name = HostingEnvironment.ApplicationID + "/XmlStore/XmlFile";
            _fileLock = new AsyncLock(name);

            InitializeSerializers();

            if (testing)
            {
                _xmlFileEnabled = false;
            }
            else
            {
                InitializeFilePersister();
            }

            // need to wait for resolution to be frozen
            if (Resolution.IsFrozen)
                OnResolutionFrozen(testing, enableRepositoryEvents);
            else
                Resolution.Frozen += (sender, args) => OnResolutionFrozen(testing, enableRepositoryEvents);
        }

        // internal for unit tests
        // initialize with an xml document
        // no events, no file nor db, no config check
        internal XmlStore(XmlDocument xmlDocument)
        {
            _xmlDocument = xmlDocument;
            _xmlFileEnabled = false;

            // do not plug events, we may not have what it takes to handle them
        }

        // internal for unit tests
        // initialize with a function returning an xml document
        // no events, no file nor db, no config check
        internal XmlStore(Func<XmlDocument> getXmlDocument)
        {
            if (getXmlDocument == null)
                throw new ArgumentNullException("getXmlDocument");
            GetXmlDocument = getXmlDocument;
            _xmlFileEnabled = false;

            // do not plug events, we may not have what it takes to handle them
        }

        private void InitializeSerializers()
        {
            var exs = new EntityXmlSerializer();
            _xmlContentSerializer = c => exs.Serialize(_serviceContext.ContentService, _serviceContext.DataTypeService, _serviceContext.UserService, c);
            _xmlMemberSerializer = m => exs.Serialize(_serviceContext.DataTypeService, m);
            _xmlMediaSerializer = m => exs.Serialize(_serviceContext.MediaService, _serviceContext.DataTypeService, _serviceContext.UserService, m);
        }

        private void InitializeFilePersister()
        {
            if (SyncToXmlFile == false) return;

            var logger = LoggerResolver.Current.Logger;
            _filePersisterRunner = new BackgroundTaskRunner<XmlStoreFilePersister>(new BackgroundTaskRunnerOptions
            {
                LongRunning = true,
                KeepAlive = true
            }, logger);

            // create (and add to runner)
            _persisterTask = new XmlStoreFilePersister(_filePersisterRunner, this, logger);
        }

        private void OnResolutionFrozen(bool testing, bool enableRepositoryEvents)
        {
            if (testing == false || enableRepositoryEvents)
                InitializeRepositoryEvents();
            if (testing)
                return;
            InitializeOtherEvents();
            InitializeContent();
        }

        // plug events
        private void InitializeRepositoryEvents()
        {
            // plug repository event handlers
            // these trigger within the transaction to ensure consistency
            // and are used to maintain the central, database-level XML cache
            ContentRepository.RemovedEntity += OnContentRemovedEntity;
            ContentRepository.RemovedVersion += OnContentRemovedVersion;
            ContentRepository.RefreshedEntity += OnContentRefreshedEntity;
            MediaRepository.RemovedEntity += OnMediaRemovedEntity;
            MediaRepository.RemovedVersion += OnMediaRemovedVersion;
            MediaRepository.RefreshedEntity += OnMediaRefreshedEntity;
            MemberRepository.RemovedEntity += OnMemberRemovedEntity;
            MemberRepository.RemovedVersion += OnMemberRemovedVersion;
            MemberRepository.RefreshedEntity += OnMemberRefreshedEntity;

            // plug
            // note: there's no REMOVE because content will be DELETED beforehand (no xml to handle)
            ContentTypeServiceBase.TransactionRefreshedEntity += OnContentTypeRefreshedEntity;

            // mostly to be sure - each node should have been deleted beforehand
            ContentRepository.EmptiedRecycleBin += OnEmptiedRecycleBin;
            MediaRepository.EmptiedRecycleBin += OnEmptiedRecycleBin;

            _withRepositoryEvents = true;
        }

        private void InitializeOtherEvents()
        {
            // plug event handlers
            // distributed events
            ContentTypeCacheRefresher.CacheUpdated += ContentTypeCacheUpdated; // same refresher for content, media & member

            // temp - until we get rid of Content
            global::umbraco.cms.businesslogic.Content.DeletedContent += OnDeletedContent;

            _withOtherEvents = true;
        }

        private void ClearEvents()
        {
            ContentTypeCacheRefresher.CacheUpdated -= ContentTypeCacheUpdated; // same refresher for content, media & member

            ContentRepository.RemovedEntity -= OnContentRemovedEntity;
            ContentRepository.RemovedVersion -= OnContentRemovedVersion;
            ContentRepository.RefreshedEntity -= OnContentRefreshedEntity;
            MediaRepository.RemovedEntity -= OnMediaRemovedEntity;
            MediaRepository.RemovedVersion -= OnMediaRemovedVersion;
            MediaRepository.RefreshedEntity -= OnMediaRefreshedEntity;
            MemberRepository.RemovedEntity -= OnMemberRemovedEntity;
            MemberRepository.RemovedVersion -= OnMemberRemovedVersion;
            MemberRepository.RefreshedEntity -= OnMemberRefreshedEntity;

            ContentTypeServiceBase.TransactionRefreshedEntity -= OnContentTypeRefreshedEntity;

            ContentRepository.EmptiedRecycleBin -= OnEmptiedRecycleBin;
            MediaRepository.EmptiedRecycleBin -= OnEmptiedRecycleBin;

            global::umbraco.cms.businesslogic.Content.DeletedContent -= OnDeletedContent;

            _withRepositoryEvents = false;
            _withOtherEvents = false;
        }

        private void InitializeContent()
        {
            // and populate the cache
            using (var safeXml = GetSafeXmlWriter(false))
            {
                bool registerXmlChange;
                LoadXmlLocked(safeXml, out registerXmlChange);
                safeXml.Commit(registerXmlChange);
            }
        }

        public void Dispose()
        {
            ClearEvents();
        }

        #endregion

        #region Configuration

        // gathering configuration options here to document what they mean

        private readonly bool _xmlFileEnabled = true;

        // whether the disk cache is enabled
        private bool XmlFileEnabled
        {
            get { return _xmlFileEnabled && UmbracoConfig.For.UmbracoSettings().Content.XmlCacheEnabled; }
        }

        // whether the disk cache is enabled and to update the disk cache when xml changes
        private bool SyncToXmlFile
        {
            get { return XmlFileEnabled && UmbracoConfig.For.UmbracoSettings().Content.ContinouslyUpdateXmlDiskCache; }
        }

        // whether the disk cache is enabled and to reload from disk cache if it changes
        private bool SyncFromXmlFile
        {
            get { return XmlFileEnabled && UmbracoConfig.For.UmbracoSettings().Content.XmlContentCheckForDiskChanges; }
        }

        // whether _xml is immutable or not (achieved by cloning before changing anything)
        private static bool XmlIsImmutable
        {
            get { return UmbracoConfig.For.UmbracoSettings().Content.CloneXmlContent; }
        }

        // whether to use the legacy schema
        private static bool UseLegacySchema
        {
            get { return UmbracoConfig.For.UmbracoSettings().Content.UseLegacyXmlSchema; }
        }

        // whether to keep version of everything (incl. medias & members) in cmsPreviewXml
        // for audit purposes - false by default, not in umbracoSettings.config
        // whether to... no idea what that one does
        // it is false by default and not in UmbracoSettings.config anymore - ignoring
        /*
        private static bool GlobalPreviewStorageEnabled
        {
            get { return UmbracoConfig.For.UmbracoSettings().Content.GlobalPreviewStorageEnabled; }
        }
        */

        // ensures config is valid
        private void EnsureConfigurationIsValid()
        {
            if (SyncToXmlFile && SyncFromXmlFile)
                throw new Exception("Cannot run with both ContinouslyUpdateXmlDiskCache and XmlContentCheckForDiskChanges being true.");

            if (XmlIsImmutable == false)
                //LogHelper.Warn<XmlStore>("Running with CloneXmlContent being false is a bad idea.");
                LogHelper.Warn<XmlStore>("CloneXmlContent is false - ignored, we always clone.");

            // note: if SyncFromXmlFile then we should also disable / warn that local edits are going to cause issues...
        }

        #endregion

        #region Xml

        /// <summary>
        /// Gets or sets the delegate used to retrieve the Xml content, used for unit tests, else should
        /// be null and then the default content will be used. For non-preview content only.
        /// </summary>
        /// <remarks>
        /// The default content ONLY works when in the context an Http Request mostly because the 
        /// 'content' object heavily relies on HttpContext, SQL connections and a bunch of other stuff
        /// that when run inside of a unit test fails.
        /// </remarks>
        public Func<XmlDocument> GetXmlDocument { get; set; }

        private readonly XmlDocument _xmlDocument; // supplied xml document (for tests)
        private volatile XmlDocument _xml; // master xml document
        private readonly AsyncLock _xmlLock = new AsyncLock(); // protects _xml
        //private DateTime _lastXmlChange; // last time Xml was reported as changed

        // XmlLock is to be used to ensure that only 1 thread at a time is editing
        // the Xml - so that's a higher level than just protecting _xml, which is
        // volatile anyway.

        // to be used by PublishedContentCache only, and by content.Instance
        // for non-preview content only
        public XmlDocument Xml
        {
            get
            {
                if (_xmlDocument != null)
                    return _xmlDocument;
                if (GetXmlDocument != null)
                    return GetXmlDocument();

                ReloadXmlFromFileIfChanged();
                return _xml;
            }
        }

        private void SetXmlLocked(XmlDocument xml, bool registerXmlChange)
        {
            // this is the ONLY place where we write to _xml
            _xml = xml;

            if (_routesCache != null)
                _routesCache.Clear(); // anytime we set _xml

            if (registerXmlChange == false || SyncToXmlFile == false)
                return;

            // assuming we know that UmbracoModule.PostRequestHandlerExecute will not
            // run and signal the cache, so we know it's time to save the XML, we should
            // save the file right now - in the past we checked for an HttpContext...
            //
            // keep doing it that way although it's dirty - ppl not running within the
            // UmbracoModule should signal the cache by themselves
            //
            var syncNow = UmbracoContext.Current == null || UmbracoContext.Current.HttpContext == null;

            if (syncNow)
            {
                SaveXmlToFileLocked(xml);
                return;
            }

            //_lastXmlChange = DateTime.UtcNow;
            _persisterTask = _persisterTask.Touch(); // _persisterTask != null because SyncToXmlFile == true
        }

        private static XmlDocument Clone(XmlDocument xmlDoc)
        {
            return xmlDoc == null ? null : (XmlDocument) xmlDoc.CloneNode(true);
        }

        private static void EnsureSchema(string contentTypeAlias, XmlDocument xml)
        {
            string subset = null;

            // get current doctype
            var n = xml.FirstChild;
            while (n.NodeType != XmlNodeType.DocumentType && n.NextSibling != null)
                n = n.NextSibling;
            if (n.NodeType == XmlNodeType.DocumentType)
                subset = ((XmlDocumentType)n).InternalSubset;

            // ensure it contains the content type
            if (subset != null && subset.Contains(string.Format("<!ATTLIST {0} id ID #REQUIRED>", contentTypeAlias)))
                return;

            // remove current doctype
            xml.RemoveChild(n);

            // set new doctype
            subset = string.Format("<!ELEMENT {1} ANY>{0}<!ATTLIST {1} id ID #REQUIRED>{0}{2}", Environment.NewLine, contentTypeAlias, subset);
            var doctype = xml.CreateDocumentType("root", null, null, subset);
            xml.InsertAfter(doctype, xml.FirstChild);
        }

        //private void RefreshSchema(XmlDocument xml)
        //{
        //    // remove current doctype
        //    var n = xml.FirstChild;
        //    while (n.NodeType != XmlNodeType.DocumentType && n.NextSibling != null)
        //        n = n.NextSibling;
        //    if (n.NodeType == XmlNodeType.DocumentType)
        //        xml.RemoveChild(n);

        //    // set new doctype
        //    var dtd = _svcs.ContentTypeService.GetContentTypesDtd();
        //    var doctype = xml.CreateDocumentType("root", null, null, dtd);
        //    xml.InsertAfter(doctype, xml.FirstChild);
        //}

        private static void InitializeXml(XmlDocument xml, string dtd)
        {
            // prime the xml document with an inline dtd and a root element
            xml.LoadXml(String.Format("<?xml version=\"1.0\" encoding=\"utf-8\" ?>{0}{1}{0}<root id=\"-1\"/>", Environment.NewLine, dtd));
        }

        private static void PopulateXml(IDictionary<int, List<int>> hierarchy, IDictionary<int, XmlNode> nodeIndex, int parentId, XmlNode parentNode)
        {
            List<int> children;
            if (hierarchy.TryGetValue(parentId, out children) == false) return;

            foreach (var childId in children)
            {
                // append child node to parent node and recursively take care of the child
                var childNode = nodeIndex[childId];
                parentNode.AppendChild(childNode);
                PopulateXml(hierarchy, nodeIndex, childId, childNode);
            }
        }

        /// <summary>
        /// Generates the complete (simplified) XML DTD.
        /// </summary>
        /// <returns>The DTD as a string</returns>
        private string GetDtd()
        {
            var dtd = new StringBuilder();
            dtd.AppendLine("<!DOCTYPE root [ ");

            if (UmbracoConfig.For.UmbracoSettings().Content.UseLegacyXmlSchema)
            {
                dtd.AppendLine("<!ELEMENT node ANY> <!ATTLIST node id ID #REQUIRED>  <!ELEMENT data ANY>");
            }
            else
            {
                // that whole thing does not make real sense? how could it fail?
                try
                {
                    var dtdInner = new StringBuilder();
                    var contentTypes = _serviceContext.ContentTypeService.GetAllContentTypes();
                    // though aliases should be safe and non null already?
                    var aliases = contentTypes.Select(x => x.Alias.ToSafeAlias()).WhereNotNull();
                    foreach (var alias in aliases)
                    {
                        dtdInner.AppendLine(String.Format("<!ELEMENT {0} ANY>", alias));
                        dtdInner.AppendLine(String.Format("<!ATTLIST {0} id ID #REQUIRED>", alias));
                    }
                    dtd.Append(dtdInner);
                }
                catch (Exception exception)
                {
                    LogHelper.Error<ContentTypeService>("Failed to build a DTD for the Xml cache.", exception);
                }
            }

            dtd.AppendLine("]>");
            return dtd.ToString();
        }

        // try to load from file, otherwise database
        // not locking anything - assumes correct locking
        private void LoadXmlLocked(SafeXmlWriter safeXml, out bool registerXmlChange)
        {
            LogHelper.Debug<XmlStore>("Loading Xml...");

            // try to get it from the file
            if (XmlFileEnabled && XmlFileExists && (safeXml.Xml = LoadXmlFromFileLocked()) != null)
            {
                registerXmlChange = false;
                return;
            }

            // get it from the database, and register
            LoadXmlTreeFromDatabaseLocked(safeXml);
            registerXmlChange = true;
        }

        public XmlNode GetMediaXmlNode(int mediaId)
        {
            // there's only one version for medias

            const string sql = @"SELECT umbracoNode.id, umbracoNode.parentId, umbracoNode.sortOrder, umbracoNode.Level,
cmsContentXml.xml, 1 AS published
FROM umbracoNode 
JOIN cmsContentXml ON (cmsContentXml.nodeId=umbracoNode.id)
WHERE umbracoNode.nodeObjectType = @nodeObjectType
AND (umbracoNode.id=@id)";

            var xmlDtos = _databaseContext.Database.Query<XmlDto>(sql,
                new
                {
                    @nodeObjectType = new Guid(Constants.ObjectTypes.Media),
                    @id = mediaId
                });
            var xmlDto = xmlDtos.FirstOrDefault();
            if (xmlDto == null) return null;

            var doc = new XmlDocument();
            var xml = doc.ReadNode(XmlReader.Create(new StringReader(xmlDto.Xml)));
            return xml;
        }

        public XmlNode GetMemberXmlNode(int memberId)
        {
            // there's only one version for members

            const string sql = @"SELECT umbracoNode.id, umbracoNode.parentId, umbracoNode.sortOrder, umbracoNode.Level,
cmsContentXml.xml, 1 AS published
FROM umbracoNode 
JOIN cmsContentXml ON (cmsContentXml.nodeId=umbracoNode.id)
WHERE umbracoNode.nodeObjectType = @nodeObjectType
AND (umbracoNode.id=@id)";

            var xmlDtos = _databaseContext.Database.Query<XmlDto>(sql,
                new
                {
                    @nodeObjectType = new Guid(Constants.ObjectTypes.Member),
                    @id = memberId
                });
            var xmlDto = xmlDtos.FirstOrDefault();
            if (xmlDto == null) return null;

            var doc = new XmlDocument();
            var xml = doc.ReadNode(XmlReader.Create(new StringReader(xmlDto.Xml)));
            return xml;
        }

        public XmlNode GetPreviewXmlNode(int contentId)
        {
            const string sql = @"SELECT umbracoNode.id, umbracoNode.parentId, umbracoNode.sortOrder, umbracoNode.Level,
cmsPreviewXml.xml, cmsDocument.published
FROM umbracoNode 
JOIN cmsPreviewXml ON (cmsPreviewXml.nodeId=umbracoNode.id)
JOIN cmsDocument ON (cmsDocument.nodeId=umbracoNode.id)
WHERE umbracoNode.nodeObjectType = @nodeObjectType AND cmsDocument.newest=1
AND (umbracoNode.id=@id)";

            var xmlDtos = _databaseContext.Database.Query<XmlDto>(sql,
                new
                {
                    @nodeObjectType = new Guid(Constants.ObjectTypes.Document),
                    @id = contentId
                });
            var xmlDto = xmlDtos.FirstOrDefault();
            if (xmlDto == null) return null;

            var doc = new XmlDocument();
            var xml = doc.ReadNode(XmlReader.Create(new StringReader(xmlDto.Xml)));
            if (xml == null || xml.Attributes == null) return null;

            if (xmlDto.Published == false)
                xml.Attributes.Append(doc.CreateAttribute("isDraft"));
            return xml;
        }

        public XmlDocument GetMediaXml()
        {
            // this is not efficient at all, not cached, nothing
            // just here to replicate what uQuery was doing and show it can be done
            // but really - should not be used

            return LoadMoreXmlFromDatabase(new Guid(Constants.ObjectTypes.Media));
        }

        public XmlDocument GetMemberXml()
        {
            // this is not efficient at all, not cached, nothing
            // just here to replicate what uQuery was doing and show it can be done
            // but really - should not be used

            return LoadMoreXmlFromDatabase(new Guid(Constants.ObjectTypes.Member));
        }

        public XmlDocument GetPreviewXml(int contentId, bool includeSubs)
        {
            var content = _serviceContext.ContentService.GetById(contentId);

            var doc = (XmlDocument)Xml.Clone();
            if (content == null) return doc;

            var sql = ReadCmsPreviewXmlSql1;
            if (includeSubs) sql += " OR umbracoNode.path LIKE concat(@path, ',%')";
            sql += ReadCmsPreviewXmlSql2;
            var xmlDtos = _databaseContext.Database.Query<XmlDto>(sql,
                new
                {
                    @nodeObjectType = new Guid(Constants.ObjectTypes.Document),
                    @path = content.Path,
                });
            foreach (var xmlDto in xmlDtos)
            {
                var xml = doc.ReadNode(XmlReader.Create(new StringReader(xmlDto.Xml)));
                if (xml == null || xml.Attributes == null) continue;
                if (xmlDto.Published == false)
                    xml.Attributes.Append(doc.CreateAttribute("isDraft"));
                AddOrUpdateXmlNode(doc, xmlDto.Id, xmlDto.Level, xmlDto.ParentId, xml);
            }
            return doc;
        }

        // gets a locked safe read accses to the main xml
        private SafeXmlReader GetSafeXmlReader()
        {
            var releaser = _xmlLock.Lock();
            return new SafeXmlReader(this, releaser);
        }

        // gets a locked safe read accses to the main xml
        private async Task<SafeXmlReader> GetSafeXmlReaderAsync()
        {
            var releaser = await _xmlLock.LockAsync();
            return new SafeXmlReader(this, releaser);
        }

        // gets a locked safe write access to the main xml (cloned)
        private SafeXmlWriter GetSafeXmlWriter(bool auto = true)
        {
            var releaser = _xmlLock.Lock();
            return new SafeXmlWriter(this, releaser, auto);
        }

        private class SafeXmlReader : IDisposable
        {
            private IDisposable _releaser;

            public SafeXmlReader(XmlStore store, IDisposable releaser)
            {
                _releaser = releaser;
                Xml = store.Xml;
            }

            public XmlDocument Xml { get; private set; }

            public void Dispose()
            {
                if (_releaser == null)
                    return;

                _releaser.Dispose();
                _releaser = null;
            }
        }

        private class SafeXmlWriter : IDisposable
        {
            private readonly XmlStore _store;
            private IDisposable _releaser;
            private readonly bool _auto;
            private bool _committed;

            public SafeXmlWriter(XmlStore store, IDisposable releaser, bool auto)
            {
                _store = store;
                _auto = auto;

                _releaser = releaser;

                // modify a clone of the cache because even though we're into the write-lock
                // we may have threads reading at the same time. why is this even an option?
                //Xml = XmlIsImmutable ? Clone(_store.Xml) : _store.Xml;

                // is not an option anymore
                Xml = Clone(_store.Xml);
            }

            public XmlDocument Xml { get; set; }

            public void Commit(bool registerXmlChange = true)
            {
                _store.SetXmlLocked(Xml, registerXmlChange);
                _committed = true;
            }

            public void Dispose()
            {
                if (_releaser == null)
                    return;
                if (_auto && _committed == false)
                    Commit();
                _releaser.Dispose();
                _releaser = null;
            }
        }

        private static string ChildNodesXPath
        {
            get
            {
                return UmbracoConfig.For.UmbracoSettings().Content.UseLegacyXmlSchema
                    ? "./node"
                    : "./* [@id]";
            }
        }

        private static string DataNodesXPath
        {
            get
            {
                return UmbracoConfig.For.UmbracoSettings().Content.UseLegacyXmlSchema
                    ? "./data"
                    : "./* [not(@id)]";
            }
        }

        #endregion

        #region File

        private readonly string _xmlFileName = IOHelper.MapPath(SystemFiles.ContentCacheXml);
        private DateTime _lastFileRead; // last time the file was read
        private DateTime _nextFileCheck; // last time we checked whether the file was changed
        private readonly AsyncLock _fileLock;

        private bool XmlFileExists
        {
            get
            {
                // check that the file exists and has content (is not empty)
                var fileInfo = new FileInfo(_xmlFileName);
                return fileInfo.Exists && fileInfo.Length > 0;
            }
        }

        private DateTime XmlFileLastWriteTime
        {
            get
            {
                var fileInfo = new FileInfo(_xmlFileName);
                return fileInfo.Exists ? fileInfo.LastWriteTimeUtc : DateTime.MinValue;
            }
        }

        internal void SaveXmlToFile()
        {
            using (var safeXml = GetSafeXmlReader())
            {
                SaveXmlToFileLocked(safeXml.Xml);
            }
        }

        private void SaveXmlToFileLocked(XmlDocument xml)
        {
            try
            {
                LogHelper.Info<XmlStore>("Save Xml to file...");

                // delete existing file, if any
                DeleteXmlFileLocked();

                // ensure cache directory exists
                var directoryName = Path.GetDirectoryName(_xmlFileName);
                if (directoryName == null)
                    throw new Exception(string.Format("Invalid XmlFileName \"{0}\".", _xmlFileName));
                if (System.IO.File.Exists(_xmlFileName) == false && Directory.Exists(directoryName) == false)
                    Directory.CreateDirectory(directoryName);

                // save
                using (_fileLock.Lock()) // ensure no other Umbraco is using the file
                using (var fs = new FileStream(_xmlFileName, FileMode.Create, FileAccess.Write, FileShare.Read, bufferSize: 4096, useAsync: true))
                {
                    var bytes = Encoding.UTF8.GetBytes(SaveXmlToString(xml));
                    fs.Write(bytes, 0, bytes.Length);
                }                  

                LogHelper.Debug<XmlStore>("Saved Xml to file.");
            }
            catch (Exception e)
            {
                // if something goes wrong remove the file
                DeleteXmlFileLocked();

                LogHelper.Error<XmlStore>("Failed to save Xml to file.", e);
            }
        }

        internal async System.Threading.Tasks.Task SaveXmlToFileAsync()
        {
            LogHelper.Info<XmlStore>("Save Xml to file...");

            // lock xml while we save
            using (var safeXml = await GetSafeXmlReaderAsync())
            { 
                try
                {
                    // delete existing file, if any
                    DeleteXmlFileLocked();

                    // ensure cache directory exists
                    var directoryName = Path.GetDirectoryName(_xmlFileName);
                    if (directoryName == null)
                        throw new Exception(string.Format("Invalid XmlFileName \"{0}\".", _xmlFileName));
                    if (System.IO.File.Exists(_xmlFileName) == false && Directory.Exists(directoryName) == false)
                        Directory.CreateDirectory(directoryName);

                    // save
                    using (await _fileLock.LockAsync()) // ensure no other Umbraco is using the file
                    using (var fs = new FileStream(_xmlFileName, FileMode.Create, FileAccess.Write, FileShare.Read, bufferSize: 4096, useAsync: true))
                    {
                        var bytes = Encoding.UTF8.GetBytes(SaveXmlToString(safeXml.Xml));
                        await fs.WriteAsync(bytes, 0, bytes.Length);
                    }                  

                    LogHelper.Debug<XmlStore>("Saved Xml to file.");
                }
                catch (Exception e)
                {
                    // if something goes wrong remove the file
                    DeleteXmlFileLocked();

                    LogHelper.Error<XmlStore>("Failed to save Xml to file.", e);
                }
            }
        }

        private string SaveXmlToString(XmlDocument xml)
        {
            // using that one method because we want to have proper indent
            // and in addition, writing async is never fully async because
            // althouth the writer is async, xml.WriteTo() will not async

            // that one almost works but... "The elements are indented as long as the element 
            // does not contain mixed content. Once the WriteString or WriteWhitespace method
            // is called to write out a mixed element content, the XmlWriter stops indenting. 
            // The indenting resumes once the mixed content element is closed." - says MSDN
            // about XmlWriterSettings.Indent

            // so ImportContent must also make sure of ignoring whitespaces!

            var sb = new StringBuilder();
            using (var xmlWriter = XmlWriter.Create(sb, new XmlWriterSettings
            {
                Indent = true,
                Encoding = Encoding.UTF8,
                //OmitXmlDeclaration = true
            }))
            {
                //xmlWriter.WriteProcessingInstruction("xml", "version=\"1.0\" encoding=\"utf-8\"");
                xml.WriteTo(xmlWriter); // already contains the xml declaration
            }
            return sb.ToString();
        }

        // not locking anything - assumes correct locking
        private XmlDocument LoadXmlFromFileLocked()
        {
            LogHelper.Info<XmlStore>("Load Xml from file...");
            var xml = new XmlDocument();

            try
            {
                using (_fileLock.Lock()) // ensure no other Umbraco is using the file
                using (var fs = new FileStream(_xmlFileName, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    xml.Load(fs);
                }
                _lastFileRead = DateTime.UtcNow;
            }
            catch (Exception e)
            {
                LogHelper.Error<XmlStore>("Failed to load Xml from file.", e);
                DeleteXmlFileLocked();
                return null;
            }

            LogHelper.Info<XmlStore>("Successfully loaded Xml from file.");
            return xml;
        }

        // assumes lock
        private void DeleteXmlFileLocked()
        {
            if (System.IO.File.Exists(_xmlFileName) == false) return;

            using (_fileLock.Lock())
            {
                System.IO.File.SetAttributes(_xmlFileName, FileAttributes.Normal);
                System.IO.File.Delete(_xmlFileName);
            }
        }

        private void ReloadXmlFromFileIfChanged()
        {
            if (SyncFromXmlFile == false) return;

            var now = DateTime.UtcNow;
            if (now < _nextFileCheck) return;

            // time to check
            _nextFileCheck = now.AddSeconds(1); // check every 1s
            if (XmlFileLastWriteTime <= _lastFileRead) return;

            LogHelper.Debug<XmlStore>("Xml file change detected, reloading.");

            // time to read

            using (var safeXml = GetSafeXmlWriter(false))
            {
                bool registerXmlChange;
                LoadXmlLocked(safeXml, out registerXmlChange); // updates _lastFileRead
                safeXml.Commit(registerXmlChange);
            }
        }

        #endregion

        #region Database

        const string ReadTreeCmsContentXmlSql = @"SELECT
    umbracoNode.id, umbracoNode.parentId, umbracoNode.sortOrder, umbracoNode.level, umbracoNode.path,
    cmsContentXml.xml, cmsContentXml.rv, cmsDocument.published
FROM umbracoNode 
JOIN cmsContentXml ON (cmsContentXml.nodeId=umbracoNode.id)
JOIN cmsDocument ON (cmsDocument.nodeId=umbracoNode.id)
WHERE umbracoNode.nodeObjectType = @nodeObjectType AND cmsDocument.published=1
ORDER BY umbracoNode.level, umbracoNode.sortOrder";

        const string ReadBranchCmsContentXmlSql = @"SELECT
    umbracoNode.id, umbracoNode.parentId, umbracoNode.sortOrder, umbracoNode.level, umbracoNode.path,
    cmsContentXml.xml, cmsContentXml.rv, cmsDocument.published
FROM umbracoNode 
JOIN cmsContentXml ON (cmsContentXml.nodeId=umbracoNode.id)
JOIN cmsDocument ON (cmsDocument.nodeId=umbracoNode.id)
WHERE umbracoNode.nodeObjectType = @nodeObjectType AND cmsDocument.published=1 AND (umbracoNode.id = @id OR umbracoNode.path LIKE @path)
ORDER BY umbracoNode.level, umbracoNode.sortOrder";

        const string ReadCmsContentXmlForContentTypesSql = @"SELECT
    umbracoNode.id, umbracoNode.parentId, umbracoNode.sortOrder, umbracoNode.level, umbracoNode.path,
    cmsContentXml.xml, cmsContentXml.rv, cmsDocument.published
FROM umbracoNode 
JOIN cmsContentXml ON (cmsContentXml.nodeId=umbracoNode.id)
JOIN cmsDocument ON (cmsDocument.nodeId=umbracoNode.id)
JOIN cmsContent ON (cmsDocument.nodeId=cmsContent.nodeId)
WHERE umbracoNode.nodeObjectType = @nodeObjectType AND cmsDocument.published=1 AND cmsContent.contentType IN (@ids)
ORDER BY umbracoNode.level, umbracoNode.sortOrder";

        const string ReadMoreCmsContentXmlSql = @"SELECT
    umbracoNode.id, umbracoNode.parentId, umbracoNode.sortOrder, umbracoNode.level, umbracoNode.path,
    cmsContentXml.xml, cmsContentXml.rv, 1 AS published
FROM umbracoNode 
JOIN cmsContentXml ON (cmsContentXml.nodeId=umbracoNode.id)
WHERE umbracoNode.nodeObjectType = @nodeObjectType
ORDER BY umbracoNode.level, umbracoNode.sortOrder";

        const string ReadCmsPreviewXmlSql1 = @"SELECT
    umbracoNode.id, umbracoNode.parentId, umbracoNode.sortOrder, umbracoNode.level, umbracoNode.path,
    cmsPreviewXml.xml, cmsPreviewXml.rv, cmsDocument.published
FROM umbracoNode 
JOIN cmsPreviewXml ON (cmsPreviewXml.nodeId=umbracoNode.id)
JOIN cmsDocument ON (cmsDocument.nodeId=umbracoNode.id AND cmsPreviewXml.versionId=cmsDocument.versionId)
WHERE umbracoNode.nodeObjectType = @nodeObjectType AND cmsDocument.newest=1
AND (umbracoNode.path=@path OR @path LIKE concat(umbracoNode.path, ',%')";
        const string ReadCmsPreviewXmlSql2 = @")
ORDER BY umbracoNode.level, umbracoNode.sortOrder";

        // ReSharper disable once ClassNeverInstantiated.Local
        private class XmlDto
        {
            // ReSharper disable UnusedAutoPropertyAccessor.Local

            public int Id { get; set; }
            public long Rv { get; set; }
            public int ParentId { get; set; }
            //public int SortOrder { get; set; }
            public int Level { get; set; }
            public string Path { get; set; }
            public string Xml { get; set; }
            public bool Published { get; set; }

            [Ignore]
            public XmlNode XmlNode { get; set; }

            // ReSharper restore UnusedAutoPropertyAccessor.Local
        }

        private void LoadXmlTreeFromDatabaseLocked(SafeXmlWriter safeXml)
        {
            // initialise the document ready for the composition of content
            var xml = new XmlDocument();
            InitializeXml(xml, GetDtd());

            var parents = new Dictionary<int, XmlNode>();

            var dtoQuery = LoadXmlTreeDtoFromDatabaseLocked(xml);
            foreach (var dto in dtoQuery)
            {
                XmlNode parent;
                if (parents.TryGetValue(dto.ParentId, out parent) == false)
                {
                    parent = dto.ParentId == -1
                        ? xml.DocumentElement
                        : xml.GetElementById(dto.ParentId.ToInvariantString());

                    if (parent == null)
                        continue;

                    parents[dto.ParentId] = parent;
                }

                parent.AppendChild(dto.XmlNode);
            }

            safeXml.Xml = xml;
        }

        private IEnumerable<XmlDto> LoadXmlTreeDtoFromDatabaseLocked(XmlDocument xmlDoc)
        {
            // get xml
            var xmlDtos = _databaseContext.Database.Query<XmlDto>(ReadTreeCmsContentXmlSql,
                new
                {
                    @nodeObjectType = new Guid(Constants.ObjectTypes.Document)
                });

            // create nodes
            return xmlDtos.Select(x =>
            {
                // parse into a DOM node
                x.XmlNode = ImportContent(xmlDoc, x);
                return x;
            });
        }

        private IEnumerable<XmlDto> LoadXmlBranchDtoFromDatabaseLocked(XmlDocument xmlDoc, int id, string path)
        {
            // get xml
            var xmlDtos = _databaseContext.Database.Query<XmlDto>(ReadBranchCmsContentXmlSql,
                new
                {
                    @nodeObjectType = new Guid(Constants.ObjectTypes.Document),
                    @path = path + ",%",
                    /*@id =*/ id
                });

            // create nodes
            return xmlDtos.Select(x =>
            {
                // parse into a DOM node
                x.XmlNode = ImportContent(xmlDoc, x);
                return x;
            });
        }

        private XmlDocument LoadMoreXmlFromDatabase(Guid nodeObjectType)
        {
            var hierarchy = new Dictionary<int, List<int>>();
            var nodeIndex = new Dictionary<int, XmlNode>();

            var xmlDoc = new XmlDocument();

            // get xml
            var xmlDtos = _databaseContext.Database.Query<XmlDto>(ReadMoreCmsContentXmlSql,
                new { /*@nodeObjectType =*/ nodeObjectType });

            foreach (var xmlDto in xmlDtos)
            {
                // and parse it into a DOM node
                xmlDoc.LoadXml(xmlDto.Xml);
                var node = xmlDoc.FirstChild;
                nodeIndex.Add(xmlDto.Id, node);

                // Build the content hierarchy
                List<int> children;
                if (hierarchy.TryGetValue(xmlDto.ParentId, out children) == false)
                {
                    // No children for this parent, so add one
                    children = new List<int>();
                    hierarchy.Add(xmlDto.ParentId, children);
                }
                children.Add(xmlDto.Id);
            }

            // If we got to here we must have successfully retrieved the content from the DB so
            // we can safely initialise and compose the final content DOM. 
            // Note: We are reusing the XmlDocument used to create the xml nodes above so 
            // we don't have to import them into a new XmlDocument

            // Initialise the document ready for the final composition of content
            InitializeXml(xmlDoc, string.Empty);

            // Start building the content tree recursively from the root (-1) node
            PopulateXml(hierarchy, nodeIndex, -1, xmlDoc.DocumentElement);
            return xmlDoc;
        }

        // internal - used by umbraco.content.RefreshContentFromDatabase[Async]
        internal void ReloadXmlFromDatabase()
        {
            // event - cancel

            // nobody should work on the Xml while we load
            using (var safeXml = GetSafeXmlWriter())
            {
                LoadXmlTreeFromDatabaseLocked(safeXml);
            }
        }

        #endregion

        #region Handle Distributed Events for Memory Xml

        // nothing should cause changes to the cache
        // it's the cache that subscribes to events and manages itself
        // except for requests for cache reset

        public void NotifyChanges(ContentCacheRefresher.JsonPayload[] payloads, out bool draftChanged, out bool publishedChanged)
        {
            draftChanged = true; // by default - we don't track drafts
            publishedChanged = false;

            if (_withOtherEvents == false)
                return;

            // process all changes on one xml clone
            using (var safeXml = GetSafeXmlWriter(false)) // not auto-commit
            {
                foreach (var payload in payloads)
                {
                    if (payload.ChangeTypes.HasType(TreeChangeTypes.RefreshAll))
                    {
                        LoadXmlTreeFromDatabaseLocked(safeXml);
                        publishedChanged = true;
                        continue;
                    }

                    if (payload.ChangeTypes.HasType(TreeChangeTypes.Remove))
                    {
                        var toRemove = safeXml.Xml.GetElementById(payload.Id.ToInvariantString());
                        if (toRemove != null)
                        {
                            if (toRemove.ParentNode == null) throw new Exception("oops");
                            toRemove.ParentNode.RemoveChild(toRemove);
                            publishedChanged = true;
                        }
                        continue;
                    }

                    if (payload.ChangeTypes.HasTypesNone(TreeChangeTypes.RefreshNode | TreeChangeTypes.RefreshBranch))
                    { 
                        // ?!
                        continue;
                    }

                    var content = _serviceContext.ContentService.GetById(payload.Id);
                    var current = safeXml.Xml.GetElementById(payload.Id.ToInvariantString());

                    if (content.HasPublishedVersion == false || content.Trashed)
                    {
                        // no published version
                        if (current != null)
                        {
                            // remove from xml if exists
                            if (current.ParentNode == null) throw new Exception("oops");
                            current.ParentNode.RemoveChild(current);
                            publishedChanged = true;
                        }

                        continue;
                    }

                    // else we have a published version

                    // that query is yielding results so will only load what's needed
                    //
                    // 'using' the enumerator ensures that the enumeration is properly terminated even if abandonned
                    // otherwise, it would leak an open reader & an un-released database connection
                    // see PetaPoco.Query<TRet>(Type[] types, Delegate cb, string sql, params object[] args)
                    // and read http://blogs.msdn.com/b/oldnewthing/archive/2008/08/14/8862242.aspx
                    var dtoQuery = LoadXmlBranchDtoFromDatabaseLocked(safeXml.Xml, content.Id, content.Path);
                    using (var dtos = dtoQuery.GetEnumerator())
                    {
                        if (dtos.MoveNext() == false)
                        {
                            // gone fishing, remove (possible race condition)
                            if (current != null)
                            {
                                // remove from xml if exists
                                if (current.ParentNode == null) throw new Exception("oops");
                                current.ParentNode.RemoveChild(current);
                                publishedChanged = true;
                            }
                            continue;
                        }

                        if (dtos.Current.Id != content.Id)
                            throw new Exception("oops"); // first one should be 'current'
                        var currentDto = dtos.Current;

                        // note: if anything eg parentId or path or level has changed, then rv has changed too
                        var currentRv = current == null ? -1 : int.Parse(current.Attributes["rv"].Value);

                        // if exists and unchanged and not refreshing the branch, skip entirely
                        if (current != null
                            && currentRv == currentDto.Rv
                            && payload.ChangeTypes.HasType(TreeChangeTypes.RefreshBranch) == false)
                            continue;

                        // note: Examine would not be able to do the path trick below, and we cannot help for
                        // unpublished content, so it *is* possible that Examine is inconsistent for a while,
                        // though events should get it consistent eventually.

                        // note: if path has changed we must do a branch refresh, even if the event is not requiring
                        // it, otherwise we would update the local node and not its children, who would then have
                        // inconsistent level (and path) attributes.

                        var refreshBranch = current == null
                            || payload.ChangeTypes.HasType(TreeChangeTypes.RefreshBranch)
                            || current.Attributes["path"].Value != currentDto.Path;

                        if (refreshBranch)
                        {
                            // remove node if exists
                            if (current != null)
                            {
                                if (current.ParentNode == null) throw new Exception("oops");
                                current.ParentNode.RemoveChild(current);
                            }

                            // insert node
                            var newParent = currentDto.ParentId == -1
                                ? safeXml.Xml.DocumentElement
                                : safeXml.Xml.GetElementById(currentDto.ParentId.ToInvariantString());
                            if (newParent == null) continue;
                            newParent.AppendChild(currentDto.XmlNode);
                            XmlHelper.SortNode(newParent, ChildNodesXPath, currentDto.XmlNode,
                                x => x.AttributeValue<int>("sortOrder"));

                            // add branch (don't try to be clever)
                            while (dtos.MoveNext())
                            {
                                // dtos are ordered by sortOrder already
                                var dto = dtos.Current;

                                // if node is already there, somewhere, remove
                                var n = safeXml.Xml.GetElementById(dto.Id.ToInvariantString());
                                if (n != null)
                                {
                                    if (n.ParentNode == null) throw new Exception("oops");
                                    n.ParentNode.RemoveChild(n);
                                }

                                // find parent, add node
                                var p = safeXml.Xml.GetElementById(dto.ParentId.ToInvariantString()); // branch, so parentId > 0
                                if (p == null) continue; // takes care of out-of-sync & masked
                                p.AppendChild(dto.XmlNode);
                            }
                        }
                        else
                        {
                            // in-place
                            AddOrUpdateXmlNode(safeXml.Xml, currentDto.Id, currentDto.Level, currentDto.ParentId, currentDto.XmlNode);
                        }
                    }

                    publishedChanged = true;
                }

                if (publishedChanged)
                {
                    safeXml.Commit(); // not auto!
                    ResyncCurrentPublishedCaches();
                }
            }
        }

        // FIXME in order to be consistent we should have a NotifyChanges(ContentTypeCacheRefresher.JsonPayload[] payloads)

        private void ContentTypeCacheUpdated(ContentTypeCacheRefresher sender, CacheRefresherEventArgs args)
        {
            IContentType contentType;
            IMediaType mediaType;
            IMemberType memberType;

            switch (args.MessageType)
            {
                case MessageType.RefreshAll:
                    ReloadXmlFromDatabase();
                    break;
                case MessageType.RefreshById:
                    var refreshedId = (int)args.MessageObject;
                    contentType = _serviceContext.ContentTypeService.GetContentType(refreshedId);
                    if (contentType != null) RefreshContentTypes(new[] { refreshedId });
                    else
                    {
                        mediaType = _serviceContext.ContentTypeService.GetMediaType(refreshedId);
                        if (mediaType != null) RefreshMediaTypes(new[] { refreshedId });
                        else
                        {
                            memberType = _serviceContext.MemberTypeService.Get(refreshedId);
                            if (memberType != null) RefreshMemberTypes(new[] { refreshedId });
                        }
                    }
                    break;
                case MessageType.RefreshByInstance:
                    var refreshedInstance = (IContentTypeBase)args.MessageObject;
                    contentType = refreshedInstance as IContentType;
                    if (contentType != null) RefreshContentTypes(new[] { refreshedInstance.Id });
                    else
                    {
                        mediaType = refreshedInstance as IMediaType;
                        if (mediaType != null) RefreshMediaTypes(new[] { refreshedInstance.Id });
                        else
                        {
                            memberType = refreshedInstance as IMemberType;
                            if (memberType != null) RefreshMemberTypes(new[] { refreshedInstance.Id });
                        }
                    }
                    break;
                case MessageType.RefreshByJson:
                    var json = (string)args.MessageObject;
                    foreach (var payload in ContentTypeCacheRefresher.DeserializeFromJsonPayload(json))
                    {
                        // skip those that don't really change anything for us
                        if (payload.IsNew 
                            || (payload.Removed || payload.AliasChanged || payload.PropertyRemoved) == false)
                            continue;

                        switch (payload.Type)
                        {
                            // assume all descendants will be of the same type

                            case "IContentType":
                                RefreshContentTypes(FlattenIds(payload));
                                break;
                            case "IMediaType":
                                RefreshMediaTypes(FlattenIds(payload));
                                break;
                            case "IMemberType":
                                RefreshMemberTypes(FlattenIds(payload));
                                break;
                            default:
                                throw new NotSupportedException("Invalid type: " + payload.Type);
                        }
                    }
                    break;
                case MessageType.RemoveById:
                case MessageType.RemoveByInstance:
                    // do nothing - content should be removed via their own events
                    break;
            }
        }

        private static IEnumerable<int> FlattenIds(ContentTypeCacheRefresher.JsonPayload payload)
        {
            yield return payload.Id;
            foreach (var id in payload.DescendantPayloads.SelectMany(FlattenIds))
                yield return id;
        }

        #endregion

        #region Manage change

        private static void ResyncCurrentPublishedCaches()
        {
            // note: here we do not respect the isolation level of PublishedCaches.Content
            // because we do a full resync, but that maintains backward compatibility with
            // legacy cache...

            var caches = PublishedCachesServiceResolver.HasCurrent
                ? PublishedCachesServiceResolver.Current.Service.GetPublishedCaches()
                : null;
            if (caches != null)
                caches.Resync();
        }

        private void RefreshContentTypes(IEnumerable<int> ids)
        {
            // for single-refresh of one content we just re-serialize it instead of hitting the DB
            // but for mass-refresh, we want to reload what's been serialized in the DB = faster
            // so we want one big SQL that loads all the impacted xml DTO so we can update our XML
            //
            // oh and it should be an enum of content types (due to dependencies & such)

            // get xml
            var xmlDtos = _databaseContext.Database.Query<XmlDto>(ReadCmsContentXmlForContentTypesSql,
                new { @nodeObjectType = new Guid(Constants.ObjectTypes.Document), /*@ids =*/ ids });

            // fixme - still, missing plenty of locks here
            // fixme - should we run the events as we do above?
            using (var safeXml = GetSafeXmlWriter())
            {
                foreach (var xmlDto in xmlDtos)
                {
                    // fix sortOrder - see notes in UpdateSortOrder
                    /*
                    var tmp = new XmlDocument();
                    tmp.LoadXml(xmlDto.Xml);
                    if (tmp.DocumentElement == null) throw new Exception("oops");
                    var attr = tmp.DocumentElement.GetAttributeNode("sortOrder");
                    if (attr == null) throw new Exception("oops");
                    attr.Value = xmlDto.SortOrder.ToInvariantString();
                    xmlDto.Xml = tmp.InnerXml;
                    */

                    // and parse it into a DOM node
                    var doc = new XmlDocument();
                    doc.LoadXml(xmlDto.Xml);
                    var node = doc.FirstChild;
                    node = safeXml.Xml.ImportNode(node, true);

                    AddOrUpdateXmlNode(safeXml.Xml, xmlDto.Id, xmlDto.Level, xmlDto.ParentId, node);
                }
            }
        }

        private void RefreshMediaTypes(IEnumerable<int> ids)
        {
            // nothing to do, we have no cache
        }

        private void RefreshMemberTypes(IEnumerable<int> ids)
        {
            // nothing to do, we have no cache
        }

        // adds or updates a node (docNode) into a cache (xml)
        private static void AddOrUpdateXmlNode(XmlDocument xml, int id, int level, int parentId, XmlNode docNode)
        {
            // sanity checks
            if (id != docNode.AttributeValue<int>("id"))
                throw new ArgumentException("Values of id and docNode/@id are different.");
            if (parentId != docNode.AttributeValue<int>("parentID"))
                throw new ArgumentException("Values of parentId and docNode/@parentID are different.");

            // find the document in the cache
            XmlNode currentNode = xml.GetElementById(id.ToInvariantString());

            // if the document is not there already then it's a new document
            // we must make sure that its document type exists in the schema
            if (currentNode == null && UseLegacySchema == false)
                EnsureSchema(docNode.Name, xml);

            // find the parent
            XmlNode parentNode = level == 1
                ? xml.DocumentElement
                : xml.GetElementById(parentId.ToInvariantString());

            // no parent = cannot do anything
            if (parentNode == null)
                return;

            // insert/move the node under the parent
            if (currentNode == null)
            {
                // document not there, new node, append
                currentNode = docNode;
                parentNode.AppendChild(currentNode);
            }
            else
            {
                // document found... we could just copy the currentNode children nodes over under
                // docNode, then remove currentNode and insert docNode... the code below tries to
                // be clever and faster, though only benchmarking could tell whether it's worth the
                // pain...

                // first copy current parent ID - so we can compare with target parent
                var moving = currentNode.AttributeValue<int>("parentID") != parentId;

                if (docNode.Name == currentNode.Name)
                {
                    // name has not changed, safe to just update the current node
                    // by transfering values eg copying the attributes, and importing the data elements
                    TransferValuesFromDocumentXmlToPublishedXml(docNode, currentNode);

                    // if moving, move the node to the new parent
                    // else it's already under the right parent
                    // (but maybe the sort order has been updated)
                    if (moving)
                        parentNode.AppendChild(currentNode); // remove then append to parentNode
                }
                else
                {
                    // name has changed, must use docNode (with new name)
                    // move children nodes from currentNode to docNode (already has properties)
                    var children = currentNode.SelectNodes(ChildNodesXPath);
                    if (children == null) throw new Exception("oops");
                    foreach (XmlNode child in children)
                        docNode.AppendChild(child); // remove then append to docNode

                    // and put docNode in the right place - if parent has not changed, then
                    // just replace, else remove currentNode and insert docNode under the right parent
                    // (but maybe not at the right position due to sort order)
                    if (moving)
                    {
                        if (currentNode.ParentNode == null) throw new Exception("oops");
                        currentNode.ParentNode.RemoveChild(currentNode);
                        parentNode.AppendChild(docNode);
                    }
                    else
                    {
                        // replacing might screw the sort order
                        parentNode.ReplaceChild(docNode, currentNode);
                    }

                    currentNode = docNode;
                }
            }

            // if the nodes are not ordered, must sort
            // (see U4-509 + has to work with ReplaceChild too)
            //XmlHelper.SortNodesIfNeeded(parentNode, childNodesXPath, x => x.AttributeValue<int>("sortOrder"));

            // but...
            // if we assume that nodes are always correctly sorted
            // then we just need to ensure that currentNode is at the right position.
            // should be faster that moving all the nodes around.
            XmlHelper.SortNode(parentNode, ChildNodesXPath, currentNode, x => x.AttributeValue<int>("sortOrder"));
        }

        private static void TransferValuesFromDocumentXmlToPublishedXml(XmlNode documentNode, XmlNode publishedNode)
        {
            // remove all attributes from the published node
            if (publishedNode.Attributes == null) throw new Exception("oops");
            publishedNode.Attributes.RemoveAll();

            // remove all data nodes from the published node
            var dataNodes = publishedNode.SelectNodes(DataNodesXPath);
            if (dataNodes == null) throw new Exception("oops");
            foreach (XmlNode n in dataNodes)
                publishedNode.RemoveChild(n);

            // append all attributes from the document node to the published node
            if (documentNode.Attributes == null) throw new Exception("oops");
            foreach (XmlAttribute att in documentNode.Attributes)
                ((XmlElement)publishedNode).SetAttribute(att.Name, att.Value);

            // find the first child node, if any
            var childNodes = publishedNode.SelectNodes(ChildNodesXPath);
            if (childNodes == null) throw new Exception("oops");
            var firstChildNode = childNodes.Count == 0 ? null : childNodes[0];

            // append all data nodes from the document node to the published node
            dataNodes = documentNode.SelectNodes(DataNodesXPath);
            if (dataNodes == null) throw new Exception("oops");
            foreach (XmlNode n in dataNodes)
            {
                if (publishedNode.OwnerDocument == null) throw new Exception("oops");
                var imported = publishedNode.OwnerDocument.ImportNode(n, true);
                if (firstChildNode == null)
                    publishedNode.AppendChild(imported);
                else
                    publishedNode.InsertBefore(imported, firstChildNode);
            }
        }

        private static XmlNode ImportContent(XmlDocument xml, XmlDto dto)
        {
            var node = xml.ReadNode(XmlReader.Create(new StringReader(dto.Xml), new XmlReaderSettings
            {
                IgnoreWhitespace = true
            }));

            if (node == null) throw new Exception("oops");
            if (node.Attributes == null) throw new Exception("oops");
            
            var attr = xml.CreateAttribute("rv");
            attr.Value = dto.Rv.ToString(CultureInfo.InvariantCulture);
            node.Attributes.Append(attr);

            attr = xml.CreateAttribute("path");
            attr.Value = dto.Path;
            node.Attributes.Append(attr);

            return node;
        }

        #endregion

        #region Handle Repository Events For Database Xml

        // we need them to be "repository" events ie to trigger from within the repository transaction,
        // because they need to be consistent with the content that is being refreshed/removed - and that
        // should be guaranteed by a DB transaction
        // it is not the case at the moment, instead a global lock is used whenever content is modified - well,
        // almost: rollback or unpublish do not implement it - nevertheless

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
                db.Execute("DELETE FROM cmsContentXml WHERE nodeId=@id", parms);
                db.Execute("DELETE FROM cmsPreviewXml WHERE nodeId=@id", parms);
            }

            // note: could be optimized by using "WHERE nodeId IN (...)" delete clauses
        }

        private void OnContentRemovedVersion(object sender, ContentRepository.VersionChangeEventArgs args)
        {
            OnRemovedVersion(args.UnitOfWork.Database, args.Versions);
        }

        private void OnMediaRemovedVersion(object sender, MediaRepository.VersionChangeEventArgs args)
        {
            OnRemovedVersion(args.UnitOfWork.Database, args.Versions);
        }

        private void OnMemberRemovedVersion(object sender, MemberRepository.VersionChangeEventArgs args)
        {
            OnRemovedVersion(args.UnitOfWork.Database, args.Versions);
        }

        private void OnRemovedVersion(UmbracoDatabase db, IEnumerable<Tuple<int, Guid>> items)
        {
            foreach (var item in items)
            {
                var parms = new { id = item.Item1 /*, versionId = item.Item2*/ };
                db.Execute("DELETE FROM cmsPreviewXml WHERE nodeId=@id", parms);
            }

            // note: could be optimized by using "WHERE nodeId IN (...)" delete clauses
        }

        private static readonly string[] PropertiesImpactingAllVersions = { "SortOrder", "ParentId", "Level", "Path", "Trashed" };

        private static bool HasChangesImpactingAllVersions(IContent icontent)
        {
            var content = (Content) icontent;

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
                var xml = _xmlContentSerializer(c).ToString(SaveOptions.None);

                // change below to write only one row - not one per version
                var dto1 = new PreviewXmlDto
                {
                    NodeId = c.Id,
                    Xml = xml
                };
                OnRepositoryRefreshed(db, dto1);

                // if unpublishing, remove from table

                if (((Content) c).PublishedState == PublishedState.Unpublishing)
                {
                    db.Execute("DELETE FROM cmsContentXml WHERE nodeId=@id", new { id = c.Id });
                    continue;
                }

                // need to update the published xml if we're saving the published version,
                // or having an impact on that version - we update the published xml even when masked

                IContent pc = null;
                if (c.Published)
                {
                    // saving the published version = update xml
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

                xml = _xmlContentSerializer(pc).ToString(SaveOptions.None);
                var dto2 = new ContentXmlDto { NodeId = c.Id, Xml = xml };
                OnRepositoryRefreshed(db, dto2);
            }
        }

        private void OnMediaRefreshedEntity(object sender, MediaRepository.EntityChangeEventArgs args)
        {
            var db = args.UnitOfWork.Database;

            foreach (var m in args.Entities)
            {
                // for whatever reason we delete some xml when the media is trashed
                // at least that's what the MediaService implementation did
                if (m.Trashed)
                    db.Execute("DELETE FROM cmsContentXml WHERE nodeId=@id", new { id = m.Id });

                var xml = _xmlMediaSerializer(m).ToString(SaveOptions.None);

                var dto1 = new ContentXmlDto { NodeId = m.Id, Xml = xml };
                OnRepositoryRefreshed(db, dto1);
            }
        }

        private void OnMemberRefreshedEntity(object sender, MemberRepository.EntityChangeEventArgs args)
        {
            var db = args.UnitOfWork.Database;

            foreach (var m in args.Entities)
            {
                var xml = _xmlMemberSerializer(m).ToString(SaveOptions.None);

                var dto1 = new ContentXmlDto { NodeId = m.Id, Xml = xml };
                OnRepositoryRefreshed(db, dto1);
            }
        }

        private void OnRepositoryRefreshed(UmbracoDatabase db, ContentXmlDto dto)
        {
            // use a custom SQL to update row version on each update
            //db.InsertOrUpdate(dto);

            db.InsertOrUpdate(dto,
                "SET xml=@xml, rv=rv+1 WHERE nodeId=@id",
                new
                {
                    xml = dto.Xml,
                    id = dto.NodeId
                });
        }

        private void OnRepositoryRefreshed(UmbracoDatabase db, PreviewXmlDto dto)
        {
            // cannot simply update because of PetaPoco handling of the composite key ;-(
            // read http://stackoverflow.com/questions/11169144/how-to-modify-petapoco-class-to-work-with-composite-key-comprising-of-non-numeri
            // it works in https://github.com/schotime/PetaPoco and then https://github.com/schotime/NPoco but not here
            //
            // not important anymore as we don't manage version anymore,
            // but:
            //
            // also
            // use a custom SQL to update row version on each update
            //db.InsertOrUpdate(dto);

            db.InsertOrUpdate(dto, 
                "SET xml=@xml, rv=rv+1 WHERE nodeId=@id",
                new
                {
                    xml = dto.Xml,
                    id = dto.NodeId,
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
            // could use
            // var select = new Sql().Select("DISTINCT id").From<...
            // SqlSyntaxContext.SqlSyntaxProvider.GetDeleteSubquery("cmsContentXml", "nodeId", subQuery);
            // SqlSyntaxContext.SqlSyntaxProvider.GetDeleteSubquery("cmsPreviewXml", "nodeId", subQuery);
            // but let's keep it simple for now...

            // unfortunately, SQL-CE does not support this
            /*
            const string sql1 = @"DELETE cmsPreviewXml FROM cmsPreviewXml
INNER JOIN umbracoNode on cmsPreviewXml.nodeId=umbracoNode.id
WHERE umbracoNode.trashed=1 AND umbracoNode.nodeObjectType=@nodeObjectType";
            const string sql2 = @"DELETE cmsContentXml FROM cmsContentXml
INNER JOIN umbracoNode on cmsContentXml.nodeId=umbracoNode.id
WHERE umbracoNode.trashed=1 AND umbracoNode.nodeObjectType=@nodeObjectType";
            */

            // required by SQL-CE
            const string sql1 = @"DELETE FROM cmsPreviewXml
WHERE cmsPreviewXml.nodeId IN (
    SELECT id FROM umbracoNode
    WHERE trashed=1 AND nodeObjectType=@nodeObjectType
)";
            const string sql2 = @"DELETE FROM cmsContentXml
WHERE cmsContentXml.nodeId IN (
    SELECT id FROM umbracoNode
    WHERE trashed=1 AND nodeObjectType=@nodeObjectType
)";

            var parms = new { /*@nodeObjectType =*/ nodeObjectType };
            db.Execute(sql1, parms);
            db.Execute(sql2, parms);
        }

        private void OnDeletedContent(object sender, global::umbraco.cms.businesslogic.Content.ContentDeleteEventArgs args)
        {
            var db = args.Database;
            var parms = new { @nodeId = args.Id };
            db.Execute("DELETE FROM cmsPreviewXml WHERE nodeId=@nodeId", parms);
            db.Execute("DELETE FROM cmsContentXml WHERE nodeId=@nodeId", parms);
        }

        private void OnContentTypeRefreshedEntity(ContentTypeServiceBase sender, ContentTypeServiceBase.EntityChangeEventArgs args)
        {
            var contentTypeIds = args.Entities.OfType<IContentType>().Select(x => x.Id).ToArray();
            if (contentTypeIds.Any())
                RebuildContentAndPreviewXml(contentTypeIds: contentTypeIds);

            var mediaTypeIds = args.Entities.OfType<IMediaType>().Select(x => x.Id).ToArray();
            if (mediaTypeIds.Any())
                RebuildMediaXml(contentTypeIds: mediaTypeIds);

            var memberTypeIds = args.Entities.OfType<IMemberType>().Select(x => x.Id).ToArray();
            if (memberTypeIds.Any())
                RebuildMemberXml(contentTypeIds: memberTypeIds);
        }

        #endregion

        #region Rebuild Database Xml

        public void RebuildContentAndPreviewXml(int groupSize = 5000, IEnumerable<int> contentTypeIds = null)
        {
            var contentTypeIdsA = contentTypeIds == null ? null : contentTypeIds.ToArray();

            var svc = _serviceContext.ContentService as ContentService;
            if (svc == null) throw new Exception("oops");
            svc.WithWriteLocked(repository =>
            {
                RebuildContentXmlLocked(repository, groupSize, contentTypeIdsA);
                RebuildPreviewXmlLocked(repository, groupSize, contentTypeIdsA);
            });
        }

        public void RebuildContentXml(int groupSize = 5000, IEnumerable<int> contentTypeIds = null)
        {
            var svc = _serviceContext.ContentService as ContentService;
            if (svc == null) throw new Exception("oops");
            svc.WithWriteLocked(repository => RebuildContentXmlLocked(repository, groupSize, contentTypeIds));
        }

        private void RebuildContentXmlLocked(ContentRepository repository, int groupSize, IEnumerable<int> contentTypeIds)
        {
            var contentTypeIdsA = contentTypeIds == null ? null : contentTypeIds.ToArray();
            var contentObjectType = Guid.Parse(Constants.ObjectTypes.Document);
            var db = repository.UnitOfWork.Database;
            
            // remove all - if anything fails the transaction will rollback
            if (contentTypeIds == null || contentTypeIdsA.Length == 0)
            {
                // must support SQL-CE
                //                    db.Execute(@"DELETE cmsContentXml 
                //FROM cmsContentXml 
                //JOIN umbracoNode ON (cmsContentXml.nodeId=umbracoNode.Id) 
                //WHERE umbracoNode.nodeObjectType=@objType",
                db.Execute(@"DELETE FROM cmsContentXml
WHERE cmsContentXml.nodeId IN (
    SELECT id FROM umbracoNode WHERE umbracoNode.nodeObjectType=@objType
)",
                    new { objType = contentObjectType });
            }
            else
            {
                // assume number of ctypes won't blow IN(...)
                // must support SQL-CE
                //                    db.Execute(@"DELETE cmsContentXml 
                //FROM cmsContentXml 
                //JOIN umbracoNode ON (cmsContentXml.nodeId=umbracoNode.Id) 
                //JOIN cmsContent ON (cmsContentXml.nodeId=cmsContent.nodeId)
                //WHERE umbracoNode.nodeObjectType=@objType
                //AND cmsContent.contentType IN (@ctypes)",
                db.Execute(@"DELETE FROM cmsContentXml
WHERE cmsContentXml.nodeId IN (
    SELECT id FROM umbracoNode
    JOIN cmsContent ON cmsContent.nodeId=umbracoNode.id
    WHERE umbracoNode.nodeObjectType=@objType
    AND cmsContent.contentType IN (@ctypes) 
)",
                    new { objType = contentObjectType, ctypes = contentTypeIdsA });
            }

            // insert back - if anything fails the transaction will rollback
            var query = Query<IContent>.Builder.Where(x => x.Published);
            if (contentTypeIds != null && contentTypeIdsA.Length > 0)
                query = query.WhereIn(x => x.ContentTypeId, contentTypeIdsA); // assume number of ctypes won't blow IN(...)

            var pageIndex = 0;
            var processed = 0;
            int total;
            do
            {
                // must use .GetPagedResultsByQuery2 which does NOT implicitely add (cmsDocument.newest = 1)
                // because we already have the condition on the content being published
                var descendants = repository.GetPagedResultsByQuery2(query, pageIndex++, groupSize, out total, "Path", Direction.Ascending);
                var items = descendants.Select(c => new ContentXmlDto { NodeId = c.Id, Xml = _xmlContentSerializer(c).ToString(SaveOptions.None) }).ToArray();
                db.BulkInsertRecords(items, null, false); // run within the current transaction and do NOT commit
                processed += items.Length;
            } while (processed < total);
        }

        public void RebuildPreviewXml(int groupSize = 5000, IEnumerable<int> contentTypeIds = null)
        {
            var svc = _serviceContext.ContentService as ContentService;
            if (svc == null) throw new Exception("oops");
            svc.WithWriteLocked(repository => RebuildPreviewXmlLocked(repository, groupSize, contentTypeIds));
        }

        private void RebuildPreviewXmlLocked(ContentRepository repository, int groupSize, IEnumerable<int> contentTypeIds)
        {
            var contentTypeIdsA = contentTypeIds == null ? null : contentTypeIds.ToArray();
            var contentObjectType = Guid.Parse(Constants.ObjectTypes.Document);
            var db = repository.UnitOfWork.Database;

            // remove all - if anything fails the transaction will rollback
            if (contentTypeIds == null || contentTypeIdsA.Length == 0)
            {
                // must support SQL-CE
                //                    db.Execute(@"DELETE cmsPreviewXml 
                //FROM cmsPreviewXml 
                //JOIN umbracoNode ON (cmsPreviewXml.nodeId=umbracoNode.Id) 
                //WHERE umbracoNode.nodeObjectType=@objType",
                db.Execute(@"DELETE FROM cmsPreviewXml
WHERE cmsPreviewXml.nodeId IN (
    SELECT id FROM umbracoNode WHERE umbracoNode.nodeObjectType=@objType
)",
                    new { objType = contentObjectType });
            }
            else
            {
                // assume number of ctypes won't blow IN(...)
                // must support SQL-CE
                //                    db.Execute(@"DELETE cmsPreviewXml 
                //FROM cmsPreviewXml 
                //JOIN umbracoNode ON (cmsPreviewXml.nodeId=umbracoNode.Id) 
                //JOIN cmsContent ON (cmsPreviewXml.nodeId=cmsContent.nodeId)
                //WHERE umbracoNode.nodeObjectType=@objType
                //AND cmsContent.contentType IN (@ctypes)",
                db.Execute(@"DELETE FROM cmsPreviewXml
WHERE cmsPreviewXml.nodeId IN (
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

            var pageIndex = 0;
            var processed = 0;
            int total;
            do
            {
                // .GetPagedResultsByQuery implicitely adds (cmsDocument.newest = 1) which
                // is what we want for preview (ie latest version of a content, published or not)
                var descendants = repository.GetPagedResultsByQuery(query, pageIndex++, groupSize, out total, "Path", Direction.Ascending);
                var items = descendants.Select(c => new PreviewXmlDto
                {
                    NodeId = c.Id,
                    Xml = _xmlContentSerializer(c).ToString(SaveOptions.None)
                }).ToArray();
                db.BulkInsertRecords(items, null, false); // run within the current transaction and do NOT commit
                processed += items.Length;
            } while (processed < total);
        }

        public void RebuildMediaXml(int groupSize = 5000, IEnumerable<int> contentTypeIds = null)
        {
            var svc = _serviceContext.MediaService as MediaService;
            if (svc == null) throw new Exception("oops");
            svc.WithWriteLocked(repository => RebuildMediaXmlLocked(repository, groupSize, contentTypeIds));
        }

        public void RebuildMediaXmlLocked(MediaRepository repository, int groupSize, IEnumerable<int> contentTypeIds)
        {
            var contentTypeIdsA = contentTypeIds == null ? null : contentTypeIds.ToArray();
            var mediaObjectType = Guid.Parse(Constants.ObjectTypes.Media);
            var db = repository.UnitOfWork.Database;

            // remove all - if anything fails the transaction will rollback
            if (contentTypeIds == null || contentTypeIdsA.Length == 0)
            {
                // must support SQL-CE
                //                    db.Execute(@"DELETE cmsContentXml 
                //FROM cmsContentXml 
                //JOIN umbracoNode ON (cmsContentXml.nodeId=umbracoNode.Id) 
                //WHERE umbracoNode.nodeObjectType=@objType",
                db.Execute(@"DELETE FROM cmsContentXml
WHERE cmsContentXml.nodeId IN (
    SELECT id FROM umbracoNode WHERE umbracoNode.nodeObjectType=@objType
)",
                    new { objType = mediaObjectType });
            }
            else
            {
                // assume number of ctypes won't blow IN(...)
                // must support SQL-CE
                //                    db.Execute(@"DELETE cmsContentXml 
                //FROM cmsContentXml 
                //JOIN umbracoNode ON (cmsContentXml.nodeId=umbracoNode.Id) 
                //JOIN cmsContent ON (cmsContentXml.nodeId=cmsContent.nodeId)
                //WHERE umbracoNode.nodeObjectType=@objType
                //AND cmsContent.contentType IN (@ctypes)",
                db.Execute(@"DELETE FROM cmsContentXml
WHERE cmsContentXml.nodeId IN (
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

            var pageIndex = 0;
            var processed = 0;
            int total;
            do
            {
                var descendants = repository.GetPagedResultsByQuery(query, pageIndex++, groupSize, out total, "Path", Direction.Ascending);
                var items = descendants.Select(m => new ContentXmlDto { NodeId = m.Id, Xml = _xmlMediaSerializer(m).ToString(SaveOptions.None) }).ToArray();
                db.BulkInsertRecords(items, null, false); // run within the current transaction and do NOT commit
                processed += items.Length;
            } while (processed < total);
        }

        public void RebuildMemberXml(int groupSize = 5000, IEnumerable<int> contentTypeIds = null)
        {
            var svc = _serviceContext.MemberService as MemberService;
            if (svc == null) throw new Exception("oops");
            svc.WithWriteLocked(repository => RebuildMemberXmlLocked(repository, groupSize, contentTypeIds));
        }

        public void RebuildMemberXmlLocked(MemberRepository repository, int groupSize, IEnumerable<int> contentTypeIds)
        {
            var contentTypeIdsA = contentTypeIds == null ? null : contentTypeIds.ToArray();
            var memberObjectType = Guid.Parse(Constants.ObjectTypes.Member);
            var db = repository.UnitOfWork.Database;

            // remove all - if anything fails the transaction will rollback
            if (contentTypeIds == null || contentTypeIdsA.Length == 0)
            {
                // must support SQL-CE
                //                    db.Execute(@"DELETE cmsContentXml 
                //FROM cmsContentXml 
                //JOIN umbracoNode ON (cmsContentXml.nodeId=umbracoNode.Id) 
                //WHERE umbracoNode.nodeObjectType=@objType",
                db.Execute(@"DELETE FROM cmsContentXml
WHERE cmsContentXml.nodeId IN (
    SELECT id FROM umbracoNode WHERE umbracoNode.nodeObjectType=@objType
)",
                    new { objType = memberObjectType });
            }
            else
            {
                // assume number of ctypes won't blow IN(...)
                // must support SQL-CE
                //                    db.Execute(@"DELETE cmsContentXml 
                //FROM cmsContentXml 
                //JOIN umbracoNode ON (cmsContentXml.nodeId=umbracoNode.Id) 
                //JOIN cmsContent ON (cmsContentXml.nodeId=cmsContent.nodeId)
                //WHERE umbracoNode.nodeObjectType=@objType
                //AND cmsContent.contentType IN (@ctypes)",
                db.Execute(@"DELETE FROM cmsContentXml
WHERE cmsContentXml.nodeId IN (
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

            var pageIndex = 0;
            var processed = 0;
            int total;
            do
            {
                var descendants = repository.GetPagedResultsByQuery(query, pageIndex++, groupSize, out total, "Path", Direction.Ascending);
                var items = descendants.Select(m => new ContentXmlDto { NodeId = m.Id, Xml = _xmlMemberSerializer(m).ToString(SaveOptions.None) }).ToArray();
                db.BulkInsertRecords(items, null, false); // run within the current transaction and do NOT commit
                processed += items.Length;
            } while (processed < total);
        }

        public bool VerifyContentAndPreviewXml()
        {
            var svc = _serviceContext.ContentService as ContentService;
            if (svc == null) throw new Exception("oops");
            return svc.WithReadLocked(repository => VerifyContentAndPreviewXmlLocked(repository));
        }

        private bool VerifyContentAndPreviewXmlLocked(ContentRepository repository)
        {
            // every published content item should have a corresponding row in cmsContentXml
            // every content item should have a corresponding row in cmsPreviewXml

            var contentObjectType = Guid.Parse(Constants.ObjectTypes.Document);
            var db = repository.UnitOfWork.Database;

            var count = db.ExecuteScalar<int>(@"SELECT COUNT(*)
FROM umbracoNode
JOIN cmsDocument ON (umbracoNode.id=cmsDocument.nodeId and cmsDocument.published=1)
LEFT JOIN cmsContentXml ON (umbracoNode.id=cmsContentXml.nodeId)
WHERE umbracoNode.nodeObjectType=@objType
AND cmsContentXml.nodeId IS NULL
", new { objType = contentObjectType });

            if (count > 0) return false;

            count = db.ExecuteScalar<int>(@"SELECT COUNT(*)
FROM umbracoNode
LEFT JOIN cmsPreviewXml ON (umbracoNode.id=cmsPreviewXml.nodeId)
WHERE umbracoNode.nodeObjectType=@objType
AND cmsPreviewXml.nodeId IS NULL
", new { objType = contentObjectType });

            return count == 0;
        }

        public bool VerifyMediaXml()
        {
            var svc = _serviceContext.MediaService as MediaService;
            if (svc == null) throw new Exception("oops");
            return svc.WithReadLocked(repository => VerifyMediaXmlLocked(repository));
        }

        public bool VerifyMediaXmlLocked(MediaRepository repository)
        {
            // every non-trashed media item should have a corresponding row in cmsContentXml

            var mediaObjectType = Guid.Parse(Constants.ObjectTypes.Media);
            var db = repository.UnitOfWork.Database;

            var count = db.ExecuteScalar<int>(@"SELECT COUNT(*)
FROM umbracoNode
JOIN cmsDocument ON (umbracoNode.id=cmsDocument.nodeId and cmsDocument.published=1)
LEFT JOIN cmsContentXml ON (umbracoNode.id=cmsContentXml.nodeId)
WHERE umbracoNode.nodeObjectType=@objType
AND cmsContentXml.nodeId IS NULL
", new { objType = mediaObjectType });

            return count == 0;
        }

        public bool VerifyMemberXml()
        {
            var svc = _serviceContext.MemberService as MemberService;
            if (svc == null) throw new Exception("oops");
            return svc.WithReadLocked(repository => VerifyMemberXmlLocked(repository));
        }

        public bool VerifyMemberXmlLocked(MemberRepository repository)
        {
            // every member item should have a corresponding row in cmsContentXml

            var memberObjectType = Guid.Parse(Constants.ObjectTypes.Member);
            var db = repository.UnitOfWork.Database;

            var count = db.ExecuteScalar<int>(@"SELECT COUNT(*)
FROM umbracoNode
LEFT JOIN cmsContentXml ON (umbracoNode.id=cmsContentXml.nodeId)
WHERE umbracoNode.nodeObjectType=@objType
AND cmsContentXml.nodeId IS NULL
", new { objType = memberObjectType });

            return count == 0;
        }

        #endregion
    }
}
