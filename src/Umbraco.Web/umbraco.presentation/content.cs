using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Web;
using System.Xml;
using System.Xml.XPath;
using umbraco.cms.presentation;
using Umbraco.Core;
using Umbraco.Core.Cache;
using Umbraco.Core.Configuration;
using Umbraco.Core.IO;
using Umbraco.Core.Logging;
using umbraco.BusinessLogic;
using umbraco.BusinessLogic.Actions;
using umbraco.BusinessLogic.Utils;
using umbraco.cms.businesslogic;
using umbraco.cms.businesslogic.cache;
using umbraco.cms.businesslogic.web;
using Umbraco.Core.Models;
using umbraco.DataLayer;
using umbraco.presentation.nodeFactory;
using Umbraco.Web;
using Umbraco.Web.PublishedCache;
using Umbraco.Web.PublishedCache.XmlPublishedCache;
using Action = umbraco.BusinessLogic.Actions.Action;
using Node = umbraco.NodeFactory.Node;
using Umbraco.Core;
using File = System.IO.File;

namespace umbraco
{
    /// <summary>
    /// Handles umbraco content
    /// </summary>
    public class content
    {
        #region Singleton

        private static readonly Lazy<content> LazyInstance = new Lazy<content>(() => new content());

        public static content Instance
        {
            get
            {
                return LazyInstance.Value;
            }
        }

        #endregion

        #region Properties

        private static PublishedCachesService PublishedCachesService
        {
            get
            {
                var svc = PublishedCachesServiceResolver.Current.Service as PublishedCachesService;
                if (svc == null)
                    throw new NotSupportedException("Unsupported IPublishedCachesService, only the Xml one is supported.");
                return svc;
            }
        }

        private static PublishedContentCache CurrentPublishedContentCache
        {
            get
            {
                if (UmbracoContext.Current == null)
                    throw new InvalidOperationException("Cannot retrieve current IPublishedContentCache when UmbracoContext.Current is null.");
                var cache = UmbracoContext.Current.ContentCache as PublishedContentCache;
                if (cache == null)
                    throw new NotSupportedException("Unsupported IPublishedContentCache, only the Xml one is supported.");
                return cache;
            }
        }

        /// <remarks>
        /// Get content. First call to this property will initialize xmldoc
        /// subsequent calls will be blocked until initialization is done
        /// Further we cache(in context) xmlContent for each request to ensure that
        /// we always have the same XmlDoc throughout the whole request.
        /// </remarks>
        // fixme - should obsolete that one
        public XmlDocument XmlContent
        {
            get
            {
                return UmbracoContext.Current == null 
                    ? PublishedCachesService.XmlStore.Xml // the live one
                    : CurrentPublishedContentCache.GetXml(false); // the current snapshot
            }
        }

        [Obsolete("This is no longer used and will be removed in future versions.")] // not used in core
        public static XmlDocument xmlContent
        {
            get { return Instance.XmlContent; }
        }

        #endregion

        #region Public Methods

        // note: all the following methods are obsolete
        // noone should manipulate the content cache directly
        // the content cache is managed by the PublishingStrategy etc

        [Obsolete("This is no longer used and will be removed in future versions.")] // not used in core
        public virtual void RefreshContentFromDatabaseAsync()
        {
            PublishedCachesService.XmlStore.ReloadXmlFromDatabase();
        }

        [Obsolete("This is no longer used and will be removed in future versions.")] // not used in core
        public virtual void RefreshContentFromDatabase()
        {
            PublishedCachesService.XmlStore.ReloadXmlFromDatabase();
        }

        [Obsolete("This is no longer used and will be removed in future versions")] // not used in core
        public static XmlDocument PublishNodeDo(Document d, XmlDocument xmlContentCopy, bool updateSitemapProvider)
        {
            if (d.Published == false) return xmlContentCopy;
            var c = ApplicationContext.Current.Services.ContentService.GetPublishedVersion(d.Id);
            if (c == null) return xmlContentCopy;
            PublishedCachesService.XmlStore.Refresh(xmlContentCopy, c);
            return xmlContentCopy;
        }

        [Obsolete("This is no longer used and will be removed in future versions")] // not used in core
        public void SortNodes(int parentId)
        {
            PublishedCachesService.XmlStore.SortChildren(parentId);
        }

        [Obsolete("This is no longer used and will be removed in future versions")] // not used in core
        public void UpdateDocumentCache(int pageId)
        {
            var csvc = ApplicationContext.Current.Services.ContentService;
            var content = csvc.GetPublishedVersion(pageId);
            PublishedCachesService.XmlStore.Refresh(content);
        }

        [Obsolete("This is no longer used and will be removed in future versions")] // not used in core
        public void UpdateDocumentCache(Document d)
        {
            PublishedCachesService.XmlStore.Refresh(d);
        }

        [Obsolete("This is no longer used and will be removed in future versions")] // not used in core
        public void UpdateDocumentCache(List<Document> documents)
        {
            var csvc = ApplicationContext.Current.Services.ContentService;
            var contents = documents.Select(x => csvc.GetPublishedVersion(x.Id));
            PublishedCachesService.XmlStore.Refresh(contents);
        }

        [Obsolete("This is no longer used and will be removed in future versions.", true)] // not used in core
        public void UpdateDocumentCacheAsync(int documentId)
        {
            UpdateDocumentCache(documentId);
        }

        [Obsolete("This is no longer used and will be removed in future versions.", true)] // not used in core
        public void ClearDocumentCacheAsync(int documentId)
        {
            PublishedCachesService.XmlStore.Remove(documentId);
        }

        [Obsolete("This is no longer used and will be removed in future versions.")] // not used in core
        public void ClearDocumentCache(int documentId)
        {
            PublishedCachesService.XmlStore.Remove(documentId);
        }

        [Obsolete("This is no longer used and will be removed in future versions.", true)] // not used in core
        public void UnPublishNode(int documentId)
        {
            PublishedCachesService.XmlStore.Remove(documentId);
        }

        #endregion

        #region Events

        // we need to keep these here or obsolete them altogether
        // fixme obsolete! all of them should be PublishedCachesService cache-agnostic events

        public delegate void ContentCacheDatabaseLoadXmlStringEventHandler(ref string xml, ContentCacheLoadNodeEventArgs e);

        public delegate void ContentCacheLoadNodeEventHandler(XmlNode xmlNode, ContentCacheLoadNodeEventArgs e);

        public delegate void DocumentCacheEventHandler(Document sender, DocumentCacheEventArgs e);

        public delegate void RefreshContentEventHandler(Document sender, RefreshContentEventArgs e);

        /// <summary>
        /// Gets or sets a value indicating whether to trigger the legacy content events.
        /// </summary>
        /// <remarks>False by default, ie the legacy content events will not trigger.</remarks>
        public static bool FireEvents { get; set; }

        /// <summary>
        /// Occurs when a document is about to be refreshed in the cache.
        /// </summary>
        // fixme - but not if many documents are refreshed
        public static event DocumentCacheEventHandler BeforeUpdateDocumentCache;

        internal static void FireBeforeUpdateDocumentCache(Document sender, DocumentCacheEventArgs e)
        {
            if (BeforeUpdateDocumentCache != null)
                BeforeUpdateDocumentCache(sender, e);
        }

        /// <summary>
        /// Occurs when a document has been refreshed in the cache.
        /// </summary>
        // fixme - but not if many documents are refreshed
        // fixme - used by RoutesCache
        public static event DocumentCacheEventHandler AfterUpdateDocumentCache;

        internal static void FireAfterUpdateDocumentCache(Document sender, DocumentCacheEventArgs e)
        {
            if (AfterUpdateDocumentCache != null)
                AfterUpdateDocumentCache(sender, e);
        }

        /// <summary>
        /// Occurs when a document is about to be removed from the cache.
        /// </summary>
        public static event DocumentCacheEventHandler BeforeClearDocumentCache;

        internal static void FireBeforeClearDocumentCache(Document sender, DocumentCacheEventArgs e)
        {
            if (BeforeClearDocumentCache != null)
                BeforeClearDocumentCache(sender, e);
        }

        /// <summary>
        /// Occurs after a document has been removed from the cache.
        /// </summary>
        // fixme - used by RoutesCache
        public static event DocumentCacheEventHandler AfterClearDocumentCache;

        internal static void FireAfterClearDocumentCache(Document sender, DocumentCacheEventArgs e)
        {
            if (AfterClearDocumentCache != null)
                AfterClearDocumentCache(sender, e);
        }

        /// <summary>
        /// Occurs (never)
        /// </summary>
        // fixme - was triggered by RefreshContentFromDatabase
        public static event RefreshContentEventHandler BeforeRefreshContent;

        internal static void FireBeforeRefreshContent(RefreshContentEventArgs e)
        {
            if (BeforeRefreshContent != null)
                BeforeRefreshContent(null, e);
        }

        /// <summary>
        /// Occurs after the whole cache has been reloaded (initial, or disk file change).
        /// </summary>
        // fixme - was triggered by CheckXmlContentPopuplation ie when getting Xml
        // fixme - used by RoutesCache
        public static event RefreshContentEventHandler AfterRefreshContent;

        internal static void FireAfterRefreshContent(RefreshContentEventArgs e)
        {
            if (AfterRefreshContent != null)
                AfterRefreshContent(null, e);
        }

        /// <summary>
        /// Occurs when a string is about to be used as the xml string for a content.
        /// </summary>
        public static event ContentCacheDatabaseLoadXmlStringEventHandler AfterContentCacheDatabaseLoadXmlString;

        internal static void FireAfterContentCacheDatabaseLoadXmlString(ref string xml, ContentCacheLoadNodeEventArgs e)
        {
            if (AfterContentCacheDatabaseLoadXmlString != null)
                AfterContentCacheDatabaseLoadXmlString(ref xml, e);
        }

        /// <summary>
        /// Occurs (never).
        /// </summary>
        [Obsolete("This is no longer used and will be removed in future versions")] // never fires
        public static event ContentCacheLoadNodeEventHandler BeforeContentCacheLoadNode;

        //private static void FireBeforeContentCacheLoadNode(XmlNode node, ContentCacheLoadNodeEventArgs e)
        //{
        //    if (BeforeContentCacheLoadNode != null)
        //        BeforeContentCacheLoadNode(node, e);
        //}

        /// <summary>
        /// Occurs when an XmlNode is about to be used as the xml node for a content.
        /// </summary>
        public static event ContentCacheLoadNodeEventHandler AfterContentCacheLoadNodeFromDatabase;

        internal static void FireAfterContentCacheLoadNodeFromDatabase(XmlNode node, ContentCacheLoadNodeEventArgs e)
        {
            if (AfterContentCacheLoadNodeFromDatabase != null)
                AfterContentCacheLoadNodeFromDatabase(node, e);
        }

        /// <summary>
        /// Occurs (never)
        /// </summary>
        [Obsolete("This is no longer used and will be removed in future versions")] // never fires
        public static event ContentCacheLoadNodeEventHandler BeforePublishNodeToContentCache;

        //public static void FireBeforePublishNodeToContentCache(XmlNode node, ContentCacheLoadNodeEventArgs e)
        //{
        //    if (BeforePublishNodeToContentCache != null)
        //        BeforePublishNodeToContentCache(node, e);
        //}

        #endregion

        #region Protected & Private methods

        // fixme - still used!
        public void PersistXmlToFile()
        {
            // maybe it should be PublishedCachesServiceResolver.Current.Service.Flush();
            PublishedCachesService.XmlStore.SaveXmlToFile();
        }

        #endregion
    }
}