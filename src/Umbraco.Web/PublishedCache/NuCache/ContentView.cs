using System;
using System.Collections.Generic;
using System.Linq;
using Umbraco.Core;
using Umbraco.Core.Models;
using Umbraco.Core.Models.PublishedContent;

namespace Umbraco.Web.PublishedCache.NuCache
{
    // represents a snapshot view of a ContentStore
    internal class ContentView
    {
        private readonly ContentStore _store;
        private readonly bool _hasContent;
        private readonly int[] _rootContentIds;
        private readonly Dictionary<int, PublishedContentType> _contentTypes;
        private ContentView _parentView;
        private Dictionary<int, ContentNode> _viewContent;
        private bool _zombie;

        #region Constructors

        // initializes a new instance of a view
        public ContentView(ContentStore store)
        {
            if (store == null)
                throw new ArgumentNullException("store");
            _store = store;

            // views are immutable so these will not change
            // this is safe because the ctor is called from within a lock
            _rootContentIds = _store.GetRootContent();
            _hasContent = _rootContentIds.Length > 0;
            _contentTypes = store.GetContentTypes();
        }

        #endregion

        #region Get, Has

        // gets a content (else null)
        public ContentNode Get(int id)
        {
            _store.Locker.EnterReadLock();
            EnsureNotZombie();
            try
            {
                var view = this;
                while (true) // will return on top view
                {
                    ContentNode content;
                    if (view._viewContent != null && view._viewContent.TryGetValue(id, out content))
                        return content;
                    if (view._parentView == null) // is top view
                        return _store.AllContent.TryGetValue(id, out content) ? content : null;
                    view = view._parentView;
                }
            }
            finally
            {
                _store.Locker.ExitReadLock();
            }
        }

        // get content at root (in sort order)
        public IEnumerable<ContentNode> GetAtRoot()
        {
            // FIXME refactor
            // - currently storing an unordered int[] so need to fetch & sort each time
            // - the int[] is created by store, changes ONLY if a content is added/removed
            // - this is efficient, memory-wise, but what about perfs?
            // - if the store manages an ordered int[] then it needs to refresh ALSO when sortOrder changes
            //   but the cost is only on write, and can boost reads
            // - if the store manages an ordered IPublishedContent[] then it CANNOT be used for draft/pub
            //   so we need two so that's bad so - we should do everything with IDs
            // - in the end the question is... where should sort take place
            // - should be consistent with how we manage children (int[], sorted?)
            return _rootContentIds.Select(Get).OrderBy(x => x.SortOrder);
        }

        // gets a content type
        public PublishedContentType GetContentType(int id)
        {
            PublishedContentType contentType;
            _contentTypes.TryGetValue(id, out contentType); // else null
            return contentType;
        }

        // gets a content type
        public PublishedContentType GetContentType(string alias)
        {
            // fixme: could optimize & use an index?
            return _contentTypes.Values.FirstOrDefault(x => x.Alias.InvariantEquals(alias));
        }

        // gets a value indicating whether the store has content (for this view)
        public bool HasContent
        {
            get { return _hasContent; }
        }

        // gets a value indicating whether the view has local content
        public bool HasLocalContent { get { return _viewContent != null && _viewContent.Count > 0; } }

        #endregion

        #region Set, Clear

        // sets a content
        // only ContentStore can set a content
        // not thread-safe, invoked from within a lock
        public void Set(ContentNode content)
        {
            if (content == null)
                throw new ArgumentNullException("content");
            if (_parentView != null)
                throw new InvalidOperationException("Not the top view.");

            EnsureNotZombie();
            EnsureViewContent();
            if (_viewContent.ContainsKey(content.Id) == false)
                _viewContent[content.Id] = content;
        }

        // clears a content
        // only ContentStore can clear a content
        // not thread-safe, invoked from within a lock
        public void Clear(int id)
        {
            if (_parentView != null)
                throw new InvalidOperationException("Not the top view.");

            EnsureNotZombie();
            EnsureViewContent();
            if (_viewContent.ContainsKey(id) == false)
                _viewContent[id] = null;
        }

        #endregion

        #region View

        // pushes the view under a new parent view
        // only ContentStore can push the top view under a new top view
        // once pushed, it is illegal to change anything
        // not thread-safe, invoked from within a lock
        public void Push(ContentView topView)
        {
            if (topView == null)
                throw new ArgumentNullException("topView");
            EnsureNotZombie();
            _parentView = topView;
        }

        // ensures the view content dictionary exists
        // not thread-safe, invoked from within a lock
        private void EnsureViewContent()
        {
            if (_viewContent == null)
                _viewContent = new Dictionary<int, ContentNode>();
        }

        // ensures the view has not been killed
        // not thread-safe, invoked from within a lock
        private void EnsureNotZombie()
        {
            if (_zombie)
                throw new InvalidOperationException("View is stale.");
        }

        #endregion

        #region Instrumentation

        public void Kill()
        {
            _zombie = true;
        }

        #endregion
    }
}
