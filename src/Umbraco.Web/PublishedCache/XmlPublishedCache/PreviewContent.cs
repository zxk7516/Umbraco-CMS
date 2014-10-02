using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml;
using umbraco.cms.businesslogic.web;
using Umbraco.Core;
using Umbraco.Core.IO;
using Umbraco.Core.Logging;
using Umbraco.Core.Models;

namespace Umbraco.Web.PublishedCache.XmlPublishedCache
{
    class PreviewContent
    {
        private readonly int _userId;
        private Guid _previewSet;
        private string _previewSetPath;
        private XmlDocument _previewXml;

        /// <summary>
        /// Gets the XML document.
        /// </summary>
        /// <remarks>May return <c>null</c> if the preview content set is invalid.</remarks>
        public XmlDocument XmlContent
        {
            get
            {
                // null if invalid preview content
                if (_previewSetPath == null) return null;

                // load if not loaded yet
                if (_previewXml == null)
                {
                    _previewXml = new XmlDocument();
                    try
                    {
                        _previewXml.Load(_previewSetPath);
                    }
                    catch (Exception ex)
                    {
                        LogHelper.Error<PreviewContent>(string.Format("Could not load preview set {0} for user {1}.", _previewSet, _userId), ex);

                        ClearPreviewSet();
                        
                        _previewXml = null;
                        _previewSetPath = null; // do not try again
                        _previewSet = Guid.Empty;
                    }
                }

                return _previewXml;
            }
        }

        /// <summary>
        /// Gets the preview token.
        /// </summary>
        /// <remarks>To be stored in a cookie or wherever appropriate.</remarks>
        public string Token { get { return _userId + ":" + _previewSet; } }

        /// <summary>
        /// Initializes a new instance of the <see cref="PreviewContent"/> class for a user.
        /// </summary>
        /// <param name="userId">The user identifier.</param>
        public PreviewContent(int userId)
        {
            _userId = userId;
            _previewSet = Guid.NewGuid();
            _previewSetPath = GetPreviewSetPath(_userId, _previewSet);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="PreviewContent"/> with a preview token.
        /// </summary>
        /// <param name="token">The preview token.</param>
        public PreviewContent(string token)
        {
            if (token.IsNullOrWhiteSpace())
                throw new ArgumentException("Null or empty token.", "token");
            var parts = token.Split(new[] {':'});
            if (parts.Length != 2)
                throw new ArgumentException("Invalid token.", "token");

            if (int.TryParse(parts[0], out _userId) == false)
                throw new ArgumentException("Invalid token.", "token");
            if (Guid.TryParse(parts[1], out _previewSet) == false)
                throw new ArgumentException("Invalid token.", "token");

            _previewSetPath = GetPreviewSetPath(_userId, _previewSet);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="PreviewContent"/> class with a user, a preview set
        /// identifier, and a value indicating whether to validate the content.
        /// </summary>
        /// <param name="userId">The unique identifier of the user.</param>
        /// <param name="previewSet">The unique identifier of the preview set.</param>
        // fixme - remove
        public PreviewContent(int userId, Guid previewSet)
        {
            _userId = userId;
            _previewSet = previewSet;
            _previewSetPath = GetPreviewSetPath(_userId, _previewSet);
            //if (validate) ValidatePreviewSetPath();
        }

        // creates and saves a new preview set
        // used in 2 places and each time includeSubs is true
        // have to use the Document class at the moment because IContent does not do ToXml...
        public void CreatePreviewSet(int contentId, bool includeSubs)
        {
            _previewXml = (XmlDocument)global::umbraco.content.Instance.XmlContent.Clone();

            var contentService = ApplicationContext.Current.Services.ContentService;
            var previewNodes = new List<IContent>();
            var content = contentService.GetById(contentId);
            var parentId = content.ParentId;

            // get nodes in the path
            while (parentId > 0 && XmlContent.GetElementById(parentId.ToString(CultureInfo.InvariantCulture)) == null)
            {
                var c = contentService.GetById(parentId);
                previewNodes.Insert(0, c);
                parentId = c.ParentId;
            }
            previewNodes.Add(content);

            // replace node & every node in its path
            foreach (var c in previewNodes)
            {
                parentId = c.ParentId;
                var previewXml = (new Document(c)).ToPreviewXml(XmlContent); // ToPreviewXml on Document only
                if (c.Published == false && contentService.HasPublishedVersion(c.Id) && previewXml.Attributes != null)
                    previewXml.Attributes.Append(_previewXml.CreateAttribute("isDraft"));
                XmlStore.AppendDocumentXml(_previewXml, c.Id, c.Level, parentId, previewXml);
            }

            // inject subs if required
            if (includeSubs)
            {
                var documentObject = new Document(content); // too many things on Document only
                foreach (var prevNode in documentObject.GetNodesForPreview(true))
                {
                    var previewXml = _previewXml.ReadNode(XmlReader.Create(new StringReader(prevNode.Xml)));
                    if (previewXml == null) continue;
                    if (prevNode.IsDraft && previewXml.Attributes != null)
                        previewXml.Attributes.Append(XmlContent.CreateAttribute("isDraft"));
                    XmlStore.AppendDocumentXml(_previewXml, prevNode.NodeId, prevNode.Level, prevNode.ParentId, previewXml);
                }
            }

            // make sure the preview folder exists
            var dir = new DirectoryInfo(IOHelper.MapPath(SystemDirectories.Preview));
            if (dir.Exists == false)
                dir.Create();

            // clean old preview sets
            ClearPreviewDirectory(_userId, dir);

            // save
            _previewXml.Save(_previewSetPath);
        }

        // get the full path to the preview set
        private static string GetPreviewSetPath(int userId, Guid previewSet)
        {
            return IOHelper.MapPath(Path.Combine(SystemDirectories.Preview, userId + "_" + previewSet + ".config"));
        }

        // no need to validate, content will be null if invalid
        //
        // ensures that the preview set path matches an existing file
        // else set it to null to indicate that the preview content is invalid
        //private void ValidatePreviewSetPath()
        //{
        //    if (System.IO.File.Exists(_previewSetPath)) return;

        //    LogHelper.Debug<PreviewContent>(string.Format("Invalid preview set {0} for user {1}.", _previewSet, _userId));

        //    ClearPreviewSet(_userId, _previewSet);

        //    _previewSet = Guid.Empty;
        //    _previewSetPath = null;
        //}

        // deletes files for the user, and files accessed more than one hour ago
        private static void ClearPreviewDirectory(int userId, DirectoryInfo dir)
        {
            var now = DateTime.Now;
            var prefix = userId + "_";
            foreach (var file in dir.GetFiles("*.config")
                .Where(x => x.Name.StartsWith(prefix) || (now - x.LastAccessTime).TotalMinutes > 1))
            {
                DeletePreviewSetFile(userId, file);
            }
        }

        // delete one preview set file in a safe way
        private static void DeletePreviewSetFile(int userId, FileSystemInfo file)
        {
            try
            {
                file.Delete();
            }
            catch (Exception ex)
            {
                LogHelper.Error<PreviewContent>(string.Format("Couldn't delete preview set {0} for user {1}", file.Name, userId), ex);
            }
        }

        /// <summary>
        /// Deletes the preview set in a safe way.
        /// </summary>
        public void ClearPreviewSet()
        {
            if (_previewSetPath == null) return;
            var previewSetFile = new FileInfo(_previewSetPath);
            DeletePreviewSetFile(_userId, previewSetFile);
        }

        // we do not handle preview token here
        //public void ActivatePreviewCookie()
        //{
        //    StateHelper.Cookies.Preview.SetValue(_previewSet.ToString());
        //}

        // we do not handle preview token here
        //public static void ClearPreviewCookie()
        //{
        //    if (global::umbraco.presentation.UmbracoContext.Current.UmbracoUser != null)
        //    {
        //        if (StateHelper.Cookies.Preview.HasValue)
        //        {
        //            var userId = global::umbraco.presentation.UmbracoContext.Current.UmbracoUser.Id;
        //            var previewSet = new Guid(StateHelper.Cookies.Preview.GetValue());
        //            var previewSetPath = GetPreviewSetPath(userId, previewSet);
        //            var previewSetFile = new FileInfo(previewSetPath);
        //            DeletePreviewSetFile(userId, previewSetFile);
        //        }
        //    }
        //    StateHelper.Cookies.Preview.Clear();
        //}
    }   
}
