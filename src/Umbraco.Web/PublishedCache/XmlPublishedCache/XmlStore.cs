using System;
using System.Collections.Generic;
using System.Diagnostics.Eventing.Reader;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using umbraco;
using umbraco.cms.businesslogic;
using umbraco.cms.businesslogic.web;
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
using Umbraco.Core.Services;
using Umbraco.Core.Sync;
using Umbraco.Web.Cache;

namespace Umbraco.Web.PublishedCache.XmlPublishedCache
{
    // fixme - what about routes cache?

    /// <summary>
    /// Represents the Xml storage for the Xml published cache.
    /// </summary>
    /// <remarks>
    /// <para>One instance of <see cref="XmlStore"/> is instanciated by the <see cref="PublishedCachesService"/> and
    /// then passed to all <see cref="PublishedContentCache"/> instances that are created (one per request).</para>
    /// <para>This class should *not* be public.</para>
    /// </remarks>
    class XmlStore
    {
        private readonly Func<IContent, XElement> _xmlContentSerializer;
        private readonly Func<IMember, XElement> _xmlMemberSerializer;
        private readonly Func<IMedia, XElement> _xmlMediaSerializer;

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="XmlStore"/> class.
        /// </summary>
        /// <remarks>The default constructor will boot the cache, load data from file or database,
        /// wire events in order to manage changes, etc.</remarks>
        public XmlStore()
        {
            EnsureConfigurationIsValid();

            // initialize
            // fixme - svcs should come from constructor
            var svcs = ApplicationContext.Current.Services;
            var exs = new EntityXmlSerializer();            
            _xmlContentSerializer = c => exs.Serialize(svcs.ContentService, svcs.DataTypeService, svcs.UserService, c);
            _xmlMemberSerializer = m => exs.Serialize(svcs.DataTypeService, m);
            _xmlMediaSerializer = m => exs.Serialize(svcs.MediaService, svcs.DataTypeService, svcs.UserService, m);

            // need to wait for resolution to be frozen
            Resolution.Frozen += (sender, args) =>
            {
                // plug event handlers
                PageCacheRefresher.CacheUpdated += PageCacheUpdated;
                UnpublishedPageCacheRefresher.CacheUpdated += UnpublishedPageCacheUpdated;

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

                // and populate the cache
                lock (XmlLock)
                {
                    bool registerXmlChange;
                    _xml = LoadXmlLocked(out registerXmlChange);
                    if (registerXmlChange) RegisterXmlChange();
                }
            };
        }

        // fixme - should we plug event handlers for tests?

        // internal for unit tests
        // initialize with an xml document
        internal XmlStore(XmlDocument xmlDocument)
        {
            if (xmlDocument == null)
                throw new ArgumentNullException("xmlDocument");
            _xmlDocument = xmlDocument;
            _xmlFileEnabled = false;
        }

        // internal for unit tests
        // initialize with a function returning an xml document
        internal XmlStore(Func<XmlDocument> getXmlDocument)
        {
            if (getXmlDocument == null)
                throw new ArgumentNullException("getXmlDocument");
            GetXmlDocument = getXmlDocument;
            _xmlFileEnabled = false;
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
                LogHelper.Warn<XmlStore>("Running with CloneXmlContent being false is a bad idea.");

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

        private XmlDocument _xmlDocument; // supplied xml document (for tests)
        private volatile XmlDocument _xml; // master xml document
        private static readonly object XmlLock = new object(); // protects _xml
        private DateTime _lastXmlChange; // last time Xml was reported as changed

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
                    return _xmlDocument = GetXmlDocument();

                return XmlInternal;
            }
        }

        // gets or sets the master XML document
        // takes care of refreshing from/to disk
        // no lock here, locking at a higher level (so, _xml is volatile)
        private XmlDocument XmlInternal
        {
            get
            {
                ReloadXmlFromFileIfChanged();
                return _xml;
            }
            set
            {
                _xml = value;
                RegisterXmlChange();
            }
        }

        private static XmlDocument Clone(XmlDocument xmlDoc)
        {
            return xmlDoc == null ? null : (XmlDocument)xmlDoc.CloneNode(true);
        }

        // fixme - test that it works
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

        private static void RefreshSchema(XmlDocument xml)
        {
            // remove current doctype
            var n = xml.FirstChild;
            while (n.NodeType != XmlNodeType.DocumentType && n.NextSibling != null)
                n = n.NextSibling;
            if (n.NodeType == XmlNodeType.DocumentType)
                xml.RemoveChild(n);

            // set new doctype
            var dtd = ApplicationContext.Current.Services.ContentTypeService.GetContentTypesDtd();
            var doctype = xml.CreateDocumentType("root", null, null, dtd);
            xml.InsertAfter(doctype, xml.FirstChild);
        }

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

        // assumes lock (XmlLock)
        private XmlDocument LoadXmlLocked(out bool registerXmlChange)
        {
            LogHelper.Debug<XmlStore>("Loading Xml...");

            XmlDocument xml;

            // try to get it from the file
            if (XmlFileEnabled && XmlFileExists && (xml = LoadXmlFromFile()) != null)
            {
                registerXmlChange = false;
                return xml;
            }

            // get it from the database, and register
            xml = LoadXmlFromDatabase();

            registerXmlChange = true;
            return xml;
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

            var xmlDtos = ApplicationContext.Current.DatabaseContext.Database.Query<XmlDto>(sql, // fixme inject
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

            var xmlDtos = ApplicationContext.Current.DatabaseContext.Database.Query<XmlDto>(sql, // fixme inject
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
JOIN cmsDocument ON (cmsDocument.nodeId=umbracoNode.id AND cmsPreviewXml.versionId=cmsDocument.versionId)
WHERE umbracoNode.nodeObjectType = @nodeObjectType AND cmsDocument.newest=1
AND (umbracoNode.id=@id)";

            var xmlDtos = ApplicationContext.Current.DatabaseContext.Database.Query<XmlDto>(sql, // fixme inject
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
            var contentService = ApplicationContext.Current.Services.ContentService; // fixme inject
            var content = contentService.GetById(contentId);

            var doc = (XmlDocument)Xml.Clone();
            if (content == null) return doc;

            var sql = ReadCmsPreviewXmlSql1;
            if (includeSubs) sql += " OR umbracoNode.path LIKE concat(@path, ',%')";
            sql += ReadCmsPreviewXmlSql2;
            var xmlDtos = ApplicationContext.Current.DatabaseContext.Database.Query<XmlDto>(sql, // fixme inject
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
                AppendDocumentXml(doc, xmlDto.Id, xmlDto.Level, xmlDto.ParentId, xml);
            }
            return doc;
        }

        #endregion

        #region File

        private static readonly object XmlFileLock = new object(); // protects the file
        private readonly string _xmlFileName = IOHelper.MapPath(SystemFiles.ContentCacheXml);
        private DateTime _lastFileRead; // last time the file was read
        private DateTime _nextFileCheck; // last time we checked whether the file was changed

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

        private void RegisterXmlChange()
        {
            if (SyncToXmlFile == false) return;

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
                // make sure we save an immutable xml document
                SaveXmlToFile(XmlIsImmutable ? _xml : Clone(_xml));
                return;
            }

            _lastXmlChange = DateTime.UtcNow;
        }

        // internal - used by content.PersistXmlToFile()
        internal void SaveXmlToFile()
        {
            SaveXmlToFile(XmlIsImmutable ? _xml : Clone(_xml));
        }

        private void SaveXmlToFile(XmlDocument xml)
        {
            LogHelper.Info<XmlStore>("Save Xml to file...");

            lock (XmlFileLock)
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
                    xml.Save(_xmlFileName);

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

        private XmlDocument LoadXmlFromFile()
        {
            LogHelper.Info<XmlStore>("Load Xml from file...");
            var xml = new XmlDocument();
            lock (XmlFileLock)
            {
                try
                {
                    xml.Load(_xmlFileName);
                    _lastFileRead = DateTime.UtcNow;
                }
                catch (Exception e)
                {
                    LogHelper.Error<XmlStore>("Failed to load Xml from file.", e);
                    DeleteXmlFileLocked();
                    return null;
                }
            }
            LogHelper.Info<XmlStore>("Successfully loaded Xml from file.");
            return xml;
        }

        //private void DeleteXmlFile()
        //{
        //    lock (XmlFileLock)
        //    {
        //        DeleteXmlFileLocked();
        //    }
        //}

        // assumes lock (XmlFileLock)
        private void DeleteXmlFileLocked()
        {
            if (System.IO.File.Exists(_xmlFileName) == false) return;
            System.IO.File.SetAttributes(_xmlFileName, FileAttributes.Normal);
            System.IO.File.Delete(_xmlFileName);
        }

        // internal - used by PublishedCachesService
        internal void Flush()
        {
            if (_lastXmlChange <= XmlFileLastWriteTime) return;

            // make sure we save an immutable xml document
            SaveXmlToFile(XmlIsImmutable ? _xml : Clone(_xml));
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
            lock (XmlLock)
            {
                // set _xml and not Xml because we don't want to trigger a file write
                // if content has just been loaded from the file - only if loading from database.
                bool registerXmlChange;
                _xml = LoadXmlLocked(out registerXmlChange); // updates _lastFileRead
                if (registerXmlChange) RegisterXmlChange();
            }

            if (content.FireEvents)
                content.FireAfterRefreshContent(new RefreshContentEventArgs());
        }

        #endregion

        #region Database

        private static readonly object DbLock = new object(); // ensure no concurrency on db

        const string ReadCmsContentXmlSql = @"SELECT umbracoNode.id, umbracoNode.parentId, umbracoNode.sortOrder, umbracoNode.Level,
cmsContentXml.xml, cmsDocument.published
FROM umbracoNode 
JOIN cmsContentXml ON (cmsContentXml.nodeId=umbracoNode.id)
JOIN cmsDocument ON (cmsDocument.nodeId=umbracoNode.id)
WHERE umbracoNode.nodeObjectType = @nodeObjectType AND cmsDocument.published=1
ORDER BY umbracoNode.level, umbracoNode.sortOrder";

        const string ReadMoreCmsContentXmlSql = @"SELECT umbracoNode.id, umbracoNode.parentId, umbracoNode.sortOrder, umbracoNode.Level,
cmsContentXml.xml, 1 AS published
FROM umbracoNode 
JOIN cmsContentXml ON (cmsContentXml.nodeId=umbracoNode.id)
WHERE umbracoNode.nodeObjectType = @nodeObjectType
ORDER BY umbracoNode.level, umbracoNode.sortOrder";

        const string ReadCmsPreviewXmlSql1 = @"SELECT umbracoNode.id, umbracoNode.parentId, umbracoNode.sortOrder, umbracoNode.Level,
cmsPreviewXml.xml, cmsDocument.published
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
            public int ParentId { get; set; }
            public int SortOrder { get; set; }
            public int Level { get; set; }
            public string Xml { get; set; }
            public bool Published { get; set; }
            // ReSharper restore UnusedAutoPropertyAccessor.Local
        }

        private static XmlDocument LoadXmlFromDatabase()
        {
            lock (DbLock) // fixme - only 1 at a time BUT should be protected by LoadXml anyway?!
            {
                return LoadXmlFromDatabaseLocked();
            }
        }

        private static XmlDocument LoadXmlFromDatabaseLocked()
        {
            LogHelper.Info<XmlStore>("Load Xml from database...");

            var hierarchy = new Dictionary<int, List<int>>();
            var nodeIndex = new Dictionary<int, XmlNode>();
            var xmlDoc = new XmlDocument();

            try
            {
                // get xml
                var xmlDtos = ApplicationContext.Current.DatabaseContext.Database.Query<XmlDto>(ReadCmsContentXmlSql,
                    new { @nodeObjectType = new Guid(Constants.ObjectTypes.Document) });

                foreach (var xmlDto in xmlDtos)
                {
                    // fix sortOrder - see notes in UpdateSortOrder
                    var tmp = new XmlDocument();
                    tmp.LoadXml(xmlDto.Xml);
                    if (tmp.DocumentElement == null) throw new Exception("oops");
                    var attr = tmp.DocumentElement.GetAttributeNode("sortOrder");
                    if (attr == null) throw new Exception("oops");
                    attr.Value = xmlDto.SortOrder.ToInvariantString();
                    xmlDto.Xml = tmp.InnerXml;

                    // Call the eventhandler to allow modification of the string
                    var e1 = new ContentCacheLoadNodeEventArgs();
                    var xml = xmlDto.Xml;
                    content.FireAfterContentCacheDatabaseLoadXmlString(ref xml, e1);
                    xmlDto.Xml = xml;
                    // check if a listener has canceled the event
                    if (e1.Cancel) continue;

                    // and parse it into a DOM node
                    xmlDoc.LoadXml(xmlDto.Xml);
                    var node = xmlDoc.FirstChild;
                    // same event handler loader form the xml node
                    var e2 = new ContentCacheLoadNodeEventArgs();
                    content.FireAfterContentCacheLoadNodeFromDatabase(node, e2);
                    // and checking if it was canceled again
                    if (e2.Cancel) continue;

                    nodeIndex.Add(xmlDto.Id, node);

                    // verify if either of the handlers canceled the children to load
                    if (e1.CancelChildren || e2.CancelChildren) continue;

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

                LogHelper.Debug<XmlStore>("Loaded data from database, now preparing Xml...");
            }
            catch (Exception e)
            {
                LogHelper.Error<XmlStore>(string.Format("Failed to load Xml from database ({0} parents, {1} nodes).", hierarchy.Count, nodeIndex.Count), e);

                // An error of some sort must have stopped us from successfully generating
                // the content tree, so lets return null signifying there is no content available
                return null;
            }

            try
            {
                // If we got to here we must have successfully retrieved the content from the DB so
                // we can safely initialise and compose the final content DOM. 
                // Note: We are reusing the XmlDocument used to create the xml nodes above so 
                // we don't have to import them into a new XmlDocument

                // Lets cache the DTD to save on the DB hit on the subsequent use
                var dtd = ApplicationContext.Current.Services.ContentTypeService.GetDtd(); // fixme inject

                // Initialise the document ready for the final composition of content
                InitializeXml(xmlDoc, dtd);

                // Start building the content tree recursively from the root (-1) node
                PopulateXml(hierarchy, nodeIndex, -1, xmlDoc.DocumentElement);
            }
            catch (Exception e)
            {
                LogHelper.Error<XmlStore>("Failed to prepare Xml from database.", e);

                // An error of some sort must have stopped us from successfully generating
                // the content tree, so lets return null signifying there is no content available
                return null;
            }

            LogHelper.Debug<XmlStore>("Successfully loaded Xml from database.");
            return xmlDoc;
        }

        private XmlDocument LoadMoreXmlFromDatabase(Guid nodeObjectType)
        {
            var hierarchy = new Dictionary<int, List<int>>();
            var nodeIndex = new Dictionary<int, XmlNode>();

            var xmlDoc = new XmlDocument();

            // get xml
            var xmlDtos = ApplicationContext.Current.DatabaseContext.Database.Query<XmlDto>(ReadMoreCmsContentXmlSql,
                new { @nodeObjectType = nodeObjectType });

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
            var e = new RefreshContentEventArgs();
            content.FireBeforeRefreshContent(e);
            if (e.Cancel) return;

            lock (XmlLock) // nobody should work on the Xml while we load
            {
                // setting Xml registers an Xml change
                XmlInternal = LoadXmlFromDatabase();
            }
        }

        #endregion

        #region Handle Distributed Events for Memory Xml

        // nothing should cause changes to the cache
        // it's the cache that subscribes to events and manages itself
        // except for requests for cache reset

        // fixme - check that NO other event touches the cache!
        // fixme - what about the "refresh" .aspx that can rebuilt the xml too?

        private void UnpublishedPageCacheUpdated(UnpublishedPageCacheRefresher sender, CacheRefresherEventArgs args)
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
                        RefreshSortOrder(content);
                    break;
                case MessageType.RefreshByInstance:
                    var refreshedInstance = (IContent)args.MessageObject;
                    RefreshSortOrder(refreshedInstance);
                    break;
                case MessageType.RefreshByJson:
                    var json = (string)args.MessageObject;
                    var contentService = ApplicationContext.Current.Services.ContentService;
                    foreach (var c in UnpublishedPageCacheRefresher.DeserializeFromJsonPayload(json)
                        .Select(x => contentService.GetById(x.Id))
                        .Where(x => x != null))
                    {
                        RefreshSortOrder(c);
                    }
                    break;
                case MessageType.RemoveById:
                    // ignore
                    break;
                case MessageType.RemoveByInstance:
                    // ignore
                    break;
            }
        }

        private void PageCacheUpdated(PageCacheRefresher sender, CacheRefresherEventArgs args)
        {
            switch (args.MessageType)
            {
                case MessageType.RefreshAll:
                    ReloadXmlFromDatabase();
                    break;
                case MessageType.RefreshById:
                    var refreshedId = (int)args.MessageObject;
                    Refresh(ApplicationContext.Current.Services.ContentService.GetPublishedVersion(refreshedId));
                    break;
                case MessageType.RefreshByInstance:
                    var refreshedInstance = (IContent)args.MessageObject;
                    Refresh(refreshedInstance);
                    break;
                case MessageType.RefreshByJson:
                    // not implemented - not a JSON cache refresher
                    break;
                case MessageType.RemoveById:
                    var removedId = (int)args.MessageObject;
                    Remove(removedId);
                    break;
                case MessageType.RemoveByInstance:
                    var removedInstance = (IContent)args.MessageObject;
                    Remove(removedInstance.Id);
                    break;
            }
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

        private void SafeExecute(Action<XmlDocument> change)
        {
            // modify a clone of the cache because even though we're into the write-lock
            // we may have threads reading at the same time. why is this an option?
            var wip = XmlIsImmutable ? Clone(Xml) : Xml;
            change(wip);
            XmlInternal = wip;
        }

        private void SafeExecute(Func<XmlDocument, bool> change)
        {
            // modify a clone of the cache because even though we're into the write-lock
            // we may have threads reading at the same time. why is this an option?
            var wip = XmlIsImmutable ? Clone(Xml) : Xml;
            if (change(wip))
                XmlInternal = wip;
        }

        public void Refresh(Document d) // fixme - does it need to be a document vs a IContent
        {
            // mapping to document and back to content
            // but not enabled by default?

            DocumentCacheEventArgs e = null;

            if (content.FireEvents)
            {
                e = new DocumentCacheEventArgs();
                content.FireBeforeUpdateDocumentCache(d, e);
                if (e.Cancel) return;
            }

            var c = ApplicationContext.Current.Services.ContentService.GetPublishedVersion(d.Id);

            // lock the xml cache so no other thread can write to it at the same time
            // note that some threads could read from it while we hold the lock, though
            lock (XmlLock)
            {
                SafeExecute(xml => Refresh(xml, c));
                ResyncCurrentPublishedCaches();
            }

            // fixme - should NOT be done here
            var cachedFieldKeyStart = string.Format("{0}{1}_", CacheKeys.ContentItemCacheKey, d.Id);
            ApplicationContext.Current.ApplicationCache.ClearCacheByKeySearch(cachedFieldKeyStart);

            if (content.FireEvents)
                content.FireAfterUpdateDocumentCache(d, e);
        }

        public void Refresh(IContent content)
        {
            // ensure it is published
            if (content.Published == false) return;

            // lock the xml cache so no other thread can write to it at the same time
            // note that some threads could read from it while we hold the lock, though
            lock (XmlLock)
            {
                SafeExecute(xml => Refresh(xml, content));
                ResyncCurrentPublishedCaches();
            }
        }

        public void Refresh(IEnumerable<IContent> contents)
        {
            // lock the xml cache so no other thread can write to it at the same time
            // note that some threads could read from it while we hold the lock, though
            lock (XmlLock)
            {
                SafeExecute(xml =>
                {
                    foreach (var content in contents.Where(x => x.Published))
                        Refresh(xml, content);
                });

                ResyncCurrentPublishedCaches();
            }
        }

        public void Refresh(XmlDocument xml, IContent content)
        {
            // ensure it is published
            if (content.Published == false) return;

            var parentId = content.ParentId; // -1 if no parent

            // fix sortOrder - see note in UpdateSortOrder
            var node = ImportContent(xml, content);
            var attr = ((XmlElement)node).GetAttributeNode("sortOrder");
            if (attr == null) throw new Exception("oops");
            attr.Value = content.SortOrder.ToInvariantString();

            AppendDocumentXml(xml, content.Id, content.Level, parentId, node);
        }

        public void Remove(int contentId)
        {
            DocumentCacheEventArgs e = null;
            Document d = null;

            if (content.FireEvents)
            {
                e = new DocumentCacheEventArgs();
                d = new Document(contentId);
                content.FireBeforeClearDocumentCache(d, e);
                if (e.Cancel) return;
            }

            // content.ClearDocumentCache used to also do
            // doc.XmlRemoveFromDB to remove the node from cmsContentXml
            // fail to see the point - not doing it anymore

            // lock the xml cache so no other thread can write to it at the same time
            // note that some threads could read from it while we hold the lock, though
            lock (XmlLock)
            {
                SafeExecute(xml =>
                {
                    var node = xml.GetElementById(contentId.ToInvariantString());
                    if (node == null) return false;
                    if (node.ParentNode == null) throw new Exception("oops");
                    node.ParentNode.RemoveChild(node);
                    return true;
                });

                ResyncCurrentPublishedCaches();
            }

            if (content.FireEvents)
                content.FireAfterClearDocumentCache(d, e);
        }

        private void RefreshSortOrder(IContent c)
        {
            if (c == null) throw new ArgumentNullException("c");

            // the XML in database is updated only when content is published, and then
            // it contains the sortOrder value at the time the XML was generated. when
            // a document with unpublished changes is sorted, then it is simply saved
            // (see ContentService) and so the sortOrder has changed but the XML has
            // not been updated accordingly.

            // this updates the published cache to take care of the situation
            // without ContentService having to ... what exactly?

            // no need to do it if the content is published without unpublished changes,
            // though, because in that case the XML will get re-generated with the
            // correct sort order.
            if (c.Published) return;

            // lock the xml cache so no other thread can write to it at the same time
            // note that some threads could read from it while we hold the lock, though
            lock (XmlLock)
            {
                SafeExecute(xml =>
                {
                    var node = xml.GetElementById(c.Id.ToInvariantString());
                    if (node == null) return false;
                    var attr = node.GetAttributeNode("sortOrder");
                    if (attr == null) throw new Exception("oops");
                    var sortOrder = c.SortOrder.ToInvariantString();

                    // only if node was actually modified
                    if (attr.Value == sortOrder) return false;
                    attr.Value = sortOrder;
                    return true;
                });
            }
        }

        public void SortChildren(int parentId)
        {
            var childNodesXPath = UmbracoConfig.For.UmbracoSettings().Content.UseLegacyXmlSchema
                ? "./node"
                : "./* [@id]";

            // lock the xml cache so no other thread can write to it at the same time
            // note that some threads could read from it while we hold the lock, though
            lock (XmlLock)
            {
                SafeExecute(xml =>
                {
                    var parentNode = parentId == -1
                        ? xml.DocumentElement
                        : xml.GetElementById(parentId.ToInvariantString());

                    if (parentNode == null) return false;

                    var sorted = XmlHelper.SortNodesIfNeeded(
                        parentNode,
                        childNodesXPath,
                        x => x.AttributeValue<int>("sortOrder"));

                    return sorted;
                });

                ResyncCurrentPublishedCaches();
            }
        }

        // appends a node (docNode) into a cache (xmlContentCopy)
        // and returns a cache (not necessarily the original one)
        private static void AppendDocumentXml(XmlDocument xml, int id, int level, int parentId, XmlNode docNode)
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

            // define xpath for getting the children nodes (not properties) of a node
            var childNodesXPath = UmbracoConfig.For.UmbracoSettings().Content.UseLegacyXmlSchema
                ? "./node"
                : "./* [@id]";

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
                    var children = currentNode.SelectNodes(childNodesXPath);
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
            XmlHelper.SortNode(parentNode, childNodesXPath, currentNode, x => x.AttributeValue<int>("sortOrder"));
        }

        private static void TransferValuesFromDocumentXmlToPublishedXml(XmlNode documentNode, XmlNode publishedNode)
        {
            var xpath = UseLegacySchema ? "./data" : "./* [not(@id)]";

            // remove all attributes from the published node
            if (publishedNode.Attributes == null) throw new Exception("oops");
            publishedNode.Attributes.RemoveAll();

            // remove all data nodes from the published node
            var dataNodes = publishedNode.SelectNodes(xpath);
            if (dataNodes == null) throw new Exception("oops");
            foreach (XmlNode n in dataNodes)
                publishedNode.RemoveChild(n);

            // append all attributes from the document node to the published node
            if (documentNode.Attributes == null) throw new Exception("oops");
            foreach (XmlAttribute att in documentNode.Attributes)
                ((XmlElement)publishedNode).SetAttribute(att.Name, att.Value);

            // append all data nodes from the document node to the published node
            dataNodes = documentNode.SelectNodes(xpath);
            if (dataNodes == null) throw new Exception("oops");
            foreach (XmlNode n in dataNodes)
            {
                if (publishedNode.OwnerDocument == null) throw new Exception("oops");
                var imported = publishedNode.OwnerDocument.ImportNode(n, true);
                publishedNode.AppendChild(imported);
            }
        }

        private XmlNode ImportContent(XmlDocument xml, IContent content)
        {
            var xelt = _xmlContentSerializer(content);
            var node = xelt.GetXmlNode();
            return xml.ImportNode(node, true);
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

        private void OnRemovedVersion(UmbracoDatabase db, IEnumerable<System.Tuple<int, Guid>> items)
        {
            foreach (var item in items)
            {
                var parms = new { id = item.Item1, versionId = item.Item2 };
                db.Execute("DELETE FROM cmsPreviewXml WHERE nodeId=@id and versionId=@versionId", parms);
            }

            // note: could be optimized by using "WHERE nodeId IN (...)" delete clauses
        }

        private void OnContentRefreshedEntity(object sender, ContentRepository.EntityChangeEventArgs args)
        {
            var db = args.UnitOfWork.Database;
            var now = DateTime.Now;

            foreach (var c in args.Entities)
            {
                var xml = _xmlContentSerializer(c).ToString(SaveOptions.None);

                // fixme - shouldn't we just have one row per content in cmsPreviewXml?
                // change below to write only one row - must also change wherever we're reading, though
                var exists1 = db.ExecuteScalar<int>("SELECT COUNT(*) FROM cmsPreviewXml WHERE nodeId=@id AND versionId=@version", new { id = c.Id, version = c.Version.ToString() }) > 0;
                var dto1 = new PreviewXmlDto { NodeId = c.Id, Timestamp = now, VersionId = c.Version, Xml = xml };
                OnRepositoryRefreshed(db, dto1, exists1);

                // if the content that is saved is not published, then no need to update cmsContentXml
                // but if it's just been unpublished, remove it from the table
                if (c.Published == false)
                {
                    if (c.IsPropertyDirty("Published") && ((Core.Models.Content)c).PublishedState == PublishedState.Unpublished)
                        db.Execute("DELETE FROM cmsContentXml WHERE nodeId=@id", new { id = c.Id });
                    continue;
                }

                var exists2 = db.ExecuteScalar<int>("SELECT COUNT(*) FROM cmsContentXml WHERE nodeId=@id", new { id = c.Id }) > 0;
                var dto2 = new ContentXmlDto { NodeId = c.Id, Xml = xml };
                OnRepositoryRefreshed(db, dto2, exists2);
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

                var exists1 = db.ExecuteScalar<int>("SELECT COUNT(*) FROM cmsContentXml WHERE nodeId=@id", new { id = m.Id }) > 0;
                var dto1 = new ContentXmlDto { NodeId = m.Id, Xml = xml };
                OnRepositoryRefreshed(db, dto1, exists1);

                // kill
                //// generate preview for blame history?
                //if (GlobalPreviewStorageEnabled == false) continue;

                //var now = DateTime.Now;
                //var exists2 = db.ExecuteScalar<int>("SELECT COUNT(*) FROM cmsPreviewXml WHERE nodeId=@id AND versionId=@version", new { id = m.Id, version = m.Version.ToString() }) > 0;
                //var dto2 = new PreviewXmlDto { NodeId = m.Id, Timestamp = now, VersionId = m.Version, Xml = xml };
                //OnRepositoryRefreshed(db, dto2, exists2);
            }
        }

        private void OnMemberRefreshedEntity(object sender, MemberRepository.EntityChangeEventArgs args)
        {
            var db = args.UnitOfWork.Database;

            foreach (var m in args.Entities)
            {
                var xml = _xmlMemberSerializer(m).ToString(SaveOptions.None);

                var exists1 = db.ExecuteScalar<int>("SELECT COUNT(*) FROM cmsContentXml WHERE nodeId=@id", new { id = m.Id }) > 0;
                var dto1 = new ContentXmlDto { NodeId = m.Id, Xml = xml };
                OnRepositoryRefreshed(db, dto1, exists1);

                // kill
                //// generate preview for blame history?
                //if (GlobalPreviewStorageEnabled == false) continue;

                //var now = DateTime.Now;
                //var exists2 = db.ExecuteScalar<int>("SELECT COUNT(*) FROM cmsPreviewXml WHERE nodeId=@id AND versionId=@version", new { id = m.Id, version = m.Version.ToString() }) > 0;
                //var dto2 = new PreviewXmlDto { NodeId = m.Id, Timestamp = now, VersionId = m.Version, Xml = xml };
                //OnRepositoryRefreshed(db, dto2, exists2);
            }
        }

        private void OnRepositoryRefreshed(UmbracoDatabase db, ContentXmlDto dto, bool exists)
        {
            if (exists)
                db.Update(dto);
            else
                db.Insert(dto);
        }

        private void OnRepositoryRefreshed(UmbracoDatabase db, PreviewXmlDto dto, bool exists)
        {
            if (exists)
            {
                // cannot simply update because of PetaPoco handling of the composite key ;-(
                //db.Update(dto);
                db.Update<PreviewXmlDto>(
                    "SET xml=@xml, timestamp=@timestamp WHERE nodeId=@id AND versionId=@versionId",
                    new
                    {
                        xml = dto.Xml,
                        timestamp = dto.Timestamp,
                        id = dto.NodeId,
                        versionId = dto.VersionId
                    });
            }
            else
            {
                db.Insert(dto);
            }
        }

        #endregion

        #region Rebuild Database Xml

        public void RebuildContentAndPreviewXml(int groupSize = 5000, IEnumerable<int> contentTypeIds = null)
        {
            RebuildContentXml(groupSize, contentTypeIds);
            RebuildPreviewXml(groupSize, contentTypeIds);
        }

        public void RebuildContentXml(int groupSize = 5000, IEnumerable<int> contentTypeIds = null)
        {
            var contentTypeIdsA = contentTypeIds == null ? null : contentTypeIds.ToArray();
            var contentObjectType = Guid.Parse(Constants.ObjectTypes.Document);

            // fixme - where should it come from?
            //var db = ApplicationContext.Current.DatabaseContext.Database;
            var svc = ApplicationContext.Current.Services.ContentService as ContentService;
            var repo = svc.GetContentRepository() as ContentRepository;
            var db = repo.UnitOfWork.Database;

            // fixme - transaction level issue?
            // the transaction is, by default, ReadCommited meaning that we do not lock the whole tables
            // so nothing prevents another transaction from messing with the content while we run?!

            // need to remove the data and re-insert it, in one transaction
            using (var tr = db.GetTransaction())
            {
                // remove all - if anything fails the transaction will rollback
                if (contentTypeIds == null || contentTypeIdsA.Length == 0)
                {
                    db.Execute(@"DELETE cmsContentXml 
FROM cmsContentXml 
JOIN umbracoNode ON (cmsContentXml.nodeId=umbracoNode.Id) 
WHERE umbracoNode.nodeObjectType=@objType",
                        new { objType = contentObjectType });
                }
                else
                {
                    db.Execute(@"DELETE cmsContentXml 
FROM cmsContentXml 
JOIN umbracoNode ON (cmsContentXml.nodeId=umbracoNode.Id) 
JOIN cmsContent ON (cmsContentXml.nodeId=cmsContent.nodeId)
WHERE umbracoNode.nodeObjectType=@objType
AND cmsContent.contentType IN (@ctypes)", // assume number of ctypes won't blow IN(...)
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
                    var descendants = repo.GetPagedResultsByQuery2(query, pageIndex++, groupSize, out total, "Path", Direction.Ascending);
                    var items = descendants.Select(c => new ContentXmlDto { NodeId = c.Id, Xml = _xmlContentSerializer(c).ToString(SaveOptions.None) }).ToArray();
                    db.BulkInsertRecords(items, tr);
                    processed += items.Length;
                } while (processed < total);
                
                tr.Complete();
            }
        }

        public void RebuildPreviewXml(int groupSize = 5000, IEnumerable<int> contentTypeIds = null)
        {
            var contentTypeIdsA = contentTypeIds == null ? null : contentTypeIds.ToArray();
            var contentObjectType = Guid.Parse(Constants.ObjectTypes.Document);

            // fixme - where should it come from?
            //var db = ApplicationContext.Current.DatabaseContext.Database;
            var svc = ApplicationContext.Current.Services.ContentService as ContentService;
            var repo = svc.GetContentRepository() as ContentRepository;
            var db = repo.UnitOfWork.Database;

            // need to remove the data and re-insert it, in one transaction
            using (var tr = db.GetTransaction())
            {
                // remove all - if anything fails the transaction will rollback
                if (contentTypeIds == null || contentTypeIdsA.Length == 0)
                {
                    db.Execute(@"DELETE cmsPreviewXml 
FROM cmsPreviewXml 
JOIN umbracoNode ON (cmsPreviewXml.nodeId=umbracoNode.Id) 
WHERE umbracoNode.nodeObjectType=@objType",
                        new { objType = contentObjectType });
                }
                else
                {
                    db.Execute(@"DELETE cmsPreviewXml 
FROM cmsPreviewXml 
JOIN umbracoNode ON (cmsPreviewXml.nodeId=umbracoNode.Id) 
JOIN cmsContent ON (cmsPreviewXml.nodeId=cmsContent.nodeId)
WHERE umbracoNode.nodeObjectType=@objType
AND cmsContent.contentType IN (@ctypes)", // assume number of ctypes won't blow IN(...)
                        new { objType = contentObjectType, ctypes = contentTypeIdsA });
                }

                // insert back - if anything fails the transaction will rollback
                var query = Query<IContent>.Builder;
                if (contentTypeIds != null && contentTypeIdsA.Length > 0)
                    query = query.WhereIn(x => x.ContentTypeId, contentTypeIdsA); // assume number of ctypes won't blow IN(...)

                var pageIndex = 0;
                var processed = 0;
                int total;
                var now = DateTime.Now;
                do
                {
                    // fixme - massive fail here, as we should generate PreviewXmlDto for EACH version?!

                    // .GetPagedResultsByQuery implicitely adds (cmsDocument.newest = 1) which
                    // is what we want for preview (ie latest version of a content, published or not)
                    var descendants = repo.GetPagedResultsByQuery(query, pageIndex++, groupSize, out total, "Path", Direction.Ascending);
                    var items = descendants.Select(c => new PreviewXmlDto
                    {
                        NodeId = c.Id,
                        Xml = _xmlContentSerializer(c).ToString(SaveOptions.None),
                        Timestamp = now, // fixme - what's the use?
                        VersionId = c.Version // fixme - though what's the point of a version?
                    }).ToArray();
                    db.BulkInsertRecords(items, tr);
                    processed += items.Length;
                } while (processed < total);

                tr.Complete();
            }
        }

        public void RebuildMediaXml(int groupSize = 5000, IEnumerable<int> contentTypeIds = null)
        {
            var contentTypeIdsA = contentTypeIds == null ? null : contentTypeIds.ToArray();
            var mediaObjectType = Guid.Parse(Constants.ObjectTypes.Media);

            // fixme - where should it come from?
            //var db = ApplicationContext.Current.DatabaseContext.Database;
            var svc = ApplicationContext.Current.Services.MediaService as MediaService;
            var repo = svc.GetMediaRepository() as MediaRepository;
            var db = repo.UnitOfWork.Database;

            // need to remove the data and re-insert it, in one transaction
            using (var tr = db.GetTransaction())
            {
                // remove all - if anything fails the transaction will rollback
                if (contentTypeIds == null || contentTypeIdsA.Length == 0)
                {
                    db.Execute(@"DELETE cmsContentXml 
FROM cmsContentXml 
JOIN umbracoNode ON (cmsContentXml.nodeId=umbracoNode.Id) 
WHERE umbracoNode.nodeObjectType=@objType",
                        new { objType = mediaObjectType });
                }
                else
                {
                    db.Execute(@"DELETE cmsContentXml 
FROM cmsContentXml 
JOIN umbracoNode ON (cmsContentXml.nodeId=umbracoNode.Id) 
JOIN cmsContent ON (cmsContentXml.nodeId=cmsContent.nodeId)
WHERE umbracoNode.nodeObjectType=@objType
AND cmsContent.contentType IN (@ctypes)", // assume number of ctypes won't blow IN(...)
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
                    var descendants = repo.GetPagedResultsByQuery(query, pageIndex++, groupSize, out total, "Path", Direction.Ascending);
                    var items = descendants.Select(m => new ContentXmlDto { NodeId = m.Id, Xml = _xmlMediaSerializer(m).ToString(SaveOptions.None) }).ToArray();
                    db.BulkInsertRecords(items, tr);
                    processed += items.Length;
                } while (processed < total);

                tr.Complete();
            }
        }

        public void RebuildMemberXml(int groupSize = 5000, IEnumerable<int> contentTypeIds = null)
        {
            var contentTypeIdsA = contentTypeIds == null ? null : contentTypeIds.ToArray();
            var memberObjectType = Guid.Parse(Constants.ObjectTypes.Member);

            // fixme - where should it come from?
            //var db = ApplicationContext.Current.DatabaseContext.Database;
            var svc = ApplicationContext.Current.Services.MemberService as MemberService;
            var repo = svc.GetMemberRepository() as MemberRepository;
            var db = repo.UnitOfWork.Database;

            // need to remove the data and re-insert it, in one transaction
            using (var tr = db.GetTransaction())
            {
                // remove all - if anything fails the transaction will rollback
                if (contentTypeIds == null || contentTypeIdsA.Length == 0)
                {
                    db.Execute(@"DELETE cmsContentXml 
FROM cmsContentXml 
JOIN umbracoNode ON (cmsContentXml.nodeId=umbracoNode.Id) 
WHERE umbracoNode.nodeObjectType=@objType",
                        new { objType = memberObjectType });
                }
                else
                {
                    db.Execute(@"DELETE cmsContentXml 
FROM cmsContentXml 
JOIN umbracoNode ON (cmsContentXml.nodeId=umbracoNode.Id) 
JOIN cmsContent ON (cmsContentXml.nodeId=cmsContent.nodeId)
WHERE umbracoNode.nodeObjectType=@objType
AND cmsContent.contentType IN (@ctypes)", // assume number of ctypes won't blow IN(...)
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
                    var descendants = repo.GetPagedResultsByQuery(query, pageIndex++, groupSize, out total, "Path", Direction.Ascending);
                    var items = descendants.Select(m => new ContentXmlDto { NodeId = m.Id, Xml = _xmlMemberSerializer(m).ToString(SaveOptions.None) }).ToArray();
                    db.BulkInsertRecords(items, tr);
                    processed += items.Length;
                } while (processed < total);

                tr.Complete();
            }
        }

        public bool VerifyContentAndPreviewXml()
        {
            // every published content item should have a corresponding row in cmsContentXml
            // every content item should have a corresponding row in cmsPreviewXml

            var contentObjectType = Guid.Parse(Constants.ObjectTypes.Document);

            // fixme - where should it come from?
            //var db = ApplicationContext.Current.DatabaseContext.Database;
            var svc = ApplicationContext.Current.Services.ContentService as ContentService;
            var repo = svc.GetContentRepository() as ContentRepository;
            var db = repo.UnitOfWork.Database;

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
            // every non-trashed media item should have a corresponding row in cmsContentXml

            var mediaObjectType = Guid.Parse(Constants.ObjectTypes.Media);

            // fixme - where should it come from?
            //var db = ApplicationContext.Current.DatabaseContext.Database;
            var svc = ApplicationContext.Current.Services.MediaService as MediaService;
            var repo = svc.GetMediaRepository() as MediaRepository;
            var db = repo.UnitOfWork.Database;

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
            // every member item should have a corresponding row in cmsContentXml

            var memberObjectType = Guid.Parse(Constants.ObjectTypes.Member);

            // fixme - where should it come from?
            //var db = ApplicationContext.Current.DatabaseContext.Database;
            var svc = ApplicationContext.Current.Services.MemberService as MemberService;
            var repo = svc.GetMemberRepository() as MemberRepository;
            var db = repo.UnitOfWork.Database;

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
