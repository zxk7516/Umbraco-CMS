using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Umbraco.Core.Logging;
using Umbraco.Core.Models.PublishedContent;

namespace Umbraco.Web.PublishedCache.NuCache
{
    // stores content
    internal class ContentStore
    {
        // internal, to be accessed from ContentView (only!)
        internal readonly ReaderWriterLockSlim Locker = new ReaderWriterLockSlim();
        internal readonly Dictionary<int, ContentNode> AllContent = new Dictionary<int, ContentNode>();

        private readonly ILogger _logger;
        private readonly FrozenLock _freezeLock;
        private bool _frozen;

        private readonly List<int> _rootContentIds = new List<int>(); 
        private int[] _rootContentIdsSnap = {};
        private bool _rootContentIdsDirty;

        private readonly Dictionary<int, PublishedContentTypeRef> _contentTypes = new Dictionary<int, PublishedContentTypeRef>();
        private Dictionary<int, PublishedContentType> _contentTypesSnap = new Dictionary<int, PublishedContentType>();
        private bool _contentTypesDirty;

        private class PublishedContentTypeRef
        {
            public PublishedContentTypeRef(PublishedContentType contentType)
            {
                RefCount = 0;
                ContentType = contentType;
            }

            public int RefCount;
            public PublishedContentType ContentType;
        }

        private ContentView _topView;
        private readonly int _minViewsInterval;
        private readonly int _viewsCollectInterval;
        private readonly bool _trackViews;
        private readonly SynchronizedCollection<WeakReference> _views;
        private volatile Task _collectTask;
        private DateTime _lastCollect = DateTime.MinValue;
        private DateTime _lastViewTime;

        #region Constructors

        public ContentStore(ILogger logger)
            : this(logger, new Options())
        { }

        public ContentStore(ILogger logger, Options options)
        {
            _logger = logger;
            _freezeLock = new FrozenLock(this);

            _minViewsInterval = options.MinViewsInterval;
            _viewsCollectInterval = options.ViewsCollectInternal;
            _trackViews = options.TrackViews;
            if (_trackViews == false) return;
            _views = new SynchronizedCollection<WeakReference>();
        }

        public class Options
        {
            // note: what is the appropriate MinViewsInterval?
            //
            // "0" means we create a lot of views, so we might thing about setting
            // it to something like 250ms... but if new views are created it's because
            // something has changed, and anytime something changes we want a view
            // anyway because eg the DefaultUrlProvider needs the content in the cache
            // in order to build its url... so in the end, we'd need a mechanism
            // to force a new views on those situations.
            //
            // so... MinViewsInterval better be "0" after all.
            public int MinViewsInterval = 0;

            // beware! tracking views has an impact on performances!

            public bool TrackViews = false;
            public int ViewsCollectInternal = 1000;
        }

        #endregion

        /*#region Has

        public bool Has(int id)
        {
            Locker.EnterReadLock();
            try
            {
                return AllContent.ContainsKey(id);
            }
            finally
            {
                
                Locker.ExitReadLock();
            }
        }

        #endregion*/

        #region Set, Clear

        public void Set(ContentNode content)
        {
            if (content == null)
                throw new ArgumentNullException("content");
            if (content.ChildContentIds.Count > 0)
                throw new ArgumentException("Content cannot have children.");

            _logger.Debug<ContentStore>("Set content ID:" + content.Id);

            Locker.EnterWriteLock();
            try
            {
                // get existing
                ContentNode existing;
                AllContent.TryGetValue(content.Id, out existing); // else null

                // manage content type
                if (EnsureContentType(content) == false)
                    throw new Exception("Invalid content type object.");
                if (existing == null)
                    AddContentTypeReference(content.ContentType.Id);

                // moving?
                var moving = existing != null && existing.ParentContentId != content.ParentContentId;

                // update top view (to mask change) and set
                if (_topView != null)
                {
                    if (existing == null)
                        _topView.SetNull(content.Id);
                    else if (moving)
                        CopyBranch(content, false); // the whole branch
                    else
                        _topView.Set(existing); // just that one
                }
                AllContent[content.Id] = content;

                // manage children
                if (existing != null)
                    content.ChildContentIds = existing.ChildContentIds;

                // manage the tree
                if (existing == null)
                {
                    // new, add to parent
                    AddToParent(content);
                }
                else if (moving)
                {
                    // moved, remove existing from its parent, add content to its parent
                    RemoveFromParent(existing);
                    AddToParent(content);
                }
            }
            finally
            {
                Locker.ExitWriteLock();
            }
        }

        public bool Clear(int id)
        {
            Locker.EnterWriteLock();
            try
            {
                // try to find the content
                // if it is not there, nothing to do
                ContentNode content;
                AllContent.TryGetValue(id, out content); // else null
                if (content == null) return false;

                _logger.Debug<ContentStore>("Clear content ID:" + content.Id);

                // update top view (to mask change) then remove
                CopyBranch(content, true);

                // manage the tree
                RemoveFromParent(content);

                return true;
            }
            finally
            {
                Locker.ExitWriteLock();
            }
        }

        // reload the store with new content for a branch
        public void SetBranch(int rootContentId, ContentNodeStruct[] contentStructs)
        {
            if (_frozen == false)
                throw new InvalidOperationException("Store is not frozen.");

            _logger.Debug<ContentStore>("Set branch ID:" + rootContentId);

            Locker.EnterWriteLock();
            try
            {
                // cannot accept a branch with out-of-sync content types
                if (contentStructs.Any(x => EnsureContentType(x.Node) == false))
                    throw new Exception("Invalid content type object.");

                // get existing
                ContentNode existing;
                AllContent.TryGetValue(rootContentId, out existing); // else null

                // if existing, shadow the branch
                if (_topView != null && existing != null)
                    CopyBranch(existing, true);

                // manage tree
                if (existing != null)
                    RemoveFromParent(existing);

                // now add them all back
                foreach (var s in contentStructs)
                {
                    AddContentTypeReference(s.ContentTypeId);
                    if (_topView != null) // take care of orphans
                        _topView.SetNull(s.Node.Id); // if not already there
                    AllContent[s.Node.Id] = s.Node;
                    AddToParent(s.Node);
                }
            }
            finally
            {
                Locker.ExitWriteLock();
            }
        }

        // reload the store entirely with new content
        public void SetAll(ContentNodeStruct[] contentStructs)
        {
            if (_frozen == false)
                throw new InvalidOperationException("Not frozen.");

            _logger.Debug<ContentStore>("Set all.");

            Locker.EnterWriteLock();
            try
            {
                // first, just shadow everything
                foreach (var rootContent in _rootContentIds.Select(x => AllContent[x]))
                    CopyBranch(rootContent, false);
                AllContent.Clear();

                _contentTypes.Clear();
                _contentTypesDirty = true;
                _rootContentIds.Clear();
                _rootContentIdsDirty = true;

                foreach (var s in contentStructs)
                {
                    EnsureContentType(s.Node); // rebuilding it all
                    AddContentTypeReference(s.ContentTypeId);
                    if (_topView != null) // take care of orphans
                        _topView.SetNull(s.Node.Id); // if not already there
                    AllContent[s.Node.Id] = s.Node;
                    AddToParent(s.Node);
                }
            }
            finally
            {
                Locker.ExitWriteLock();
            }
        }

        // reload the store with new content for changed content types
        public void SetTypes(IEnumerable<int> contentTypeIds, ContentNodeStruct[] contentStructs)
        {
            if (_frozen == false)
                throw new InvalidOperationException("Store is not frozen.");

            _logger.Debug<ContentStore>("Set types.");

            Locker.EnterWriteLock();
            try
            {
                // ensure we are replacing all contents else we have a problem
                var orphans = AllContent.Values
                    .Where(x => contentTypeIds.Contains(x.ContentType.Id))
                    .Select(x => x.Id)
                    .ToList();
                foreach (var s in contentStructs)
                    orphans.Remove(s.Node.Id);
                if (orphans.Count > 0)
                    throw new Exception("Orphans.");

                // shadow content
                foreach (var s in contentStructs)
                {
                    ContentNode existing;
                    if (AllContent.TryGetValue(s.Node.Id, out existing))
                        CopyBranch(existing, true);
                }

                // clear, all refs to be recalculated
                foreach (var id in contentTypeIds)
                    _contentTypes.Remove(id);
                _contentTypesDirty = true;

                foreach (var s in contentStructs)
                {
                    ContentNode existing;
                    AllContent.TryGetValue(s.Node.Id, out existing); // else null

                    EnsureContentType(s.Node); // rebuilding it all
                    AddContentTypeReference(s.ContentTypeId);
                    if (_topView != null) // take care of new
                        _topView.SetNull(s.Node.Id); // if not already there
                    AllContent[s.Node.Id] = s.Node;

                    if (existing == null)
                    {
                        AddToParent(s.Node);
                    }
                    else if (existing.ParentContentId != s.Node.ParentContentId)
                    {
                        RemoveFromParent(existing);
                        AddToParent(s.Node);
                    }
                }
            }
            finally
            {
                Locker.ExitWriteLock();                
            }
        }

        private void RemoveFromParent(ContentNode content)
        {
            // remove from root content index,
            // or parent's children index
            if (content.ParentContentId < 0)
            {
                _rootContentIdsDirty = true;
                _rootContentIds.Remove(content.Id);
            }
            else
            {
                var parent = CopyParent(content);
                parent.ChildContentIds.Remove(content.Id);
            }
        }

        private void AddToParent(ContentNode content)
        {
            // add to root content index,
            // or parent's children index
            if (content.ParentContentId < 0)
            {
                _rootContentIdsDirty = true;
                _rootContentIds.Add(content.Id);
            }
            else
            {
                var parent = CopyParent(content);
                parent.ChildContentIds.Add(content.Id);
            }
        }

        private bool EnsureContentType(ContentNode content)
        {
            PublishedContentTypeRef contentTypeRef;
            if (_contentTypes.TryGetValue(content.ContentType.Id, out contentTypeRef))
                return ReferenceEquals(content.ContentType, contentTypeRef.ContentType);

            _contentTypes[content.ContentType.Id] = new PublishedContentTypeRef(content.ContentType);
            _contentTypesDirty = true;
            return true;
        }

        private void AddContentTypeReference(int contentTypeId)
        {
            var contentTypeRef = _contentTypes[contentTypeId];
            contentTypeRef.RefCount += 1;
        }

        private void RemoveContentTypeReference(int contentTypeId)
        {
            var contentTypeRef = _contentTypes[contentTypeId];
            contentTypeRef.RefCount -= 1;
            if (contentTypeRef.RefCount == 0)
            {
                _contentTypes.Remove(contentTypeId);
                _contentTypesDirty = true;
            }
        }

        private void CopyBranch(ContentNode content, bool clear)
        {
            if (_topView != null)
                _topView.Set(content);

            if (clear)
            {
                AllContent.Remove(content.Id);
                RemoveContentTypeReference(content.ContentType.Id);
            }

            foreach (var childId in content.ChildContentIds)
            {
                ContentNode child;
                if (AllContent.TryGetValue(childId, out child) == false) continue;
                CopyBranch(child, clear);
            }
        }

        private ContentNode CopyParent(ContentNode content)
        {
            // get parent
            // update top view (to mask changes) & clone
            // set the new parent
            ContentNode parent;
            if (AllContent.TryGetValue(content.ParentContentId, out parent) == false)
                throw new Exception("oops: no parent.");

            // if there is no top view then it is safe to modify the original parent
            if (_topView == null)
                return parent;

            // if it was already shadowed in the view then it is also fine to
            // modify the parent we have because it is local only
            if (_topView.Set(parent) == false)
                return parent;

            // else we need to shadow it in the view and create a new one locally
            parent = parent.CloneParent();
            AllContent[parent.Id] = parent;
            return parent;
        }

        // resets for new data types - just by cloning contents w/new type
        public void SetDataTypes(IEnumerable<int> dataTypeIds, Func<int, PublishedContentType> getCache)
        {
            Locker.EnterWriteLock();
            try
            {
                var contentTypeRefs = _contentTypes.Values
                    .Where(x => x.ContentType.PropertyTypes.Any(p => dataTypeIds.Contains(p.DataTypeId)));

                foreach (var contentTypeRef in contentTypeRefs)
                {
                    var contentTypeId = contentTypeRef.ContentType.Id;
                    var newContentType = getCache(contentTypeId);
                    contentTypeRef.ContentType = newContentType;
                    foreach (var content in AllContent.Values.Where(x => x.ContentType.Id == contentTypeId))
                    {
                        if (_topView != null)
                            _topView.Set(content);
                        var node = new ContentNode(content, newContentType);
                        AllContent[node.Id] = node;
                    }
                }

                _contentTypesDirty = true;
            }
            finally
            {
                Locker.ExitWriteLock();                
            }
        }

        #endregion

        #region Views

        public ContentView GetView()
        {
            var now = DateTime.Now;

            if (_trackViews)
                Collect(false);

            // upgradeable read lock block write locks, and other upgradeable
            // read locks, but do not block simple read locks
            Locker.EnterUpgradeableReadLock();
            try
            {
                // if there is no top view, create one and return
                if (_topView == null)
                {
                    // no need to upgrade lock: if there is no top view, there is nothing
                    _topView = CreateView(now);
                    return _topView;
                }

                // if top view has no content yet, or is recent, or views are frozen,
                // return current top view
                if (_topView.HasLocalContent == false || (now - _lastViewTime).TotalMilliseconds < _minViewsInterval || _frozen)
                {
                    return _topView;
                }

                // else lock and create and return a new top view
                // which will be a snapshot of the store as of right now
                Locker.EnterWriteLock();
                try
                {
                    var oldTopView = _topView;
                    _topView = CreateView(now);
                    oldTopView.Push(_topView);
                    return _topView;
                }
                finally
                {
                    Locker.ExitWriteLock();
                }
            }
            finally
            {
                Locker.ExitUpgradeableReadLock();
            }
        }

        private ContentView CreateView(DateTime creationTime)
        {
            var view = new ContentView(this);

            if (_trackViews)
                _views.Add(new WeakReference(view));

            _lastViewTime = creationTime;
            return view;
        }

        // non thread-safe, need proper lock
        internal int[] GetRootContent()
        {
            if (_rootContentIdsDirty)
            {
                _rootContentIdsSnap = _rootContentIds.ToArray();
                _rootContentIdsDirty = false;
            }
            return _rootContentIdsSnap;
        }

        // non thread-safe, need proper lock
        internal Dictionary<int, PublishedContentType> GetContentTypes()
        {
            if (_contentTypesDirty)
            {
                _contentTypesSnap = _contentTypes.ToDictionary(x => x.Key, x => x.Value.ContentType);
                _contentTypesDirty = false;
            }
            return _contentTypesSnap;
        }

        public void KillViews()
        {
            Locker.EnterWriteLock();
            try
            {
                if (_topView == null) return;
                _topView.Kill();
                _topView = null;
                if (_trackViews)
                    _views.Clear();
            }
            finally
            {
                Locker.ExitWriteLock();
            }
        }

        public IDisposable Frozen
        {
            get
            {
                _freezeLock.Lock();
                return _freezeLock;
            }
        }

        private class FrozenLock : IDisposable
        {
            private readonly object _locko = new object();
            private readonly ContentStore _store;

            public FrozenLock(ContentStore store)
            {
                _store = store;
            }

            public void Lock()
            {
                _store.Locker.EnterWriteLock();
                try
                {
                    Monitor.Enter(_locko);
                    _store._frozen = true;
                }
                finally
                {
                    _store.Locker.ExitWriteLock();
                }
            }

            public void Dispose()
            {
                _store.Locker.EnterWriteLock();
                try
                {
                    _store._frozen = false;
                    Monitor.Exit(_locko);
                }
                finally
                {
                    _store.Locker.ExitWriteLock();
                }
            }
        }

        #endregion

        #region Instrumentation

        public int ViewsCount
        {
            get
            {
                if (_trackViews == false)
                    throw new InvalidOperationException("Views tracking is not enabled.");

                // in a multi-threaded environment this will be an indication,
                // not an exact value, but anyway

                Collect(true);
                WaitForPendingCollection();

                Locker.EnterReadLock();
                try
                {
                    return _views.Count();
                }
                finally
                {
                    Locker.ExitReadLock();
                }
            }
        }

        public void Collect(bool forceNow)
        {
            if (_trackViews == false) return;

            // from C# specs
            //
            // "[...] Reads and writes of the following data types are atomic: bool, char,
            // byte, sbyte, short, ushort, uint, int, float, and reference types. [...]"
            //
            // so maybe we don't need the locks everywhere, but I'm not a multithread guru
            // enough to try it so let's be safe

            Locker.EnterUpgradeableReadLock();
            try
            {
                if (_collectTask != null) return;
                if (forceNow == false && (DateTime.Now - _lastCollect).TotalMilliseconds < _viewsCollectInterval) return;

                Locker.EnterWriteLock();
                try
                {
                    _collectTask = Task.Run(() =>
                    {
                        var remove = _views.Where(x => x.IsAlive == false).ToArray();
                        foreach (var wref in remove) _views.Remove(wref);

                        _lastCollect = DateTime.Now;
                        Locker.EnterWriteLock();
                        try
                        {
                            _collectTask = null;
                        }
                        finally
                        {
                            Locker.ExitWriteLock();
                        }
                    });
                }
                finally
                {
                    Locker.ExitWriteLock();
                }
            }
            finally
            {               
                Locker.ExitUpgradeableReadLock();
            }
        }

        public void WaitForPendingCollection()
        {
            if (_trackViews == false) return;

            var collectTask = _collectTask; // atomic read
            if (collectTask != null) collectTask.Wait();
        }

        #endregion
    }
}
