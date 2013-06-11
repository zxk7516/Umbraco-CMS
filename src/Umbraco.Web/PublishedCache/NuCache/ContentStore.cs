using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Umbraco.Core.Models.PublishedContent;

namespace Umbraco.Web.PublishedCache.NuCache
{
    // stores content
    internal class ContentStore
    {
        // internal, to be accessed from ContentView (only!)
        internal readonly ReaderWriterLockSlim Locker = new ReaderWriterLockSlim();
        internal readonly Dictionary<int, ContentNode> AllContent = new Dictionary<int, ContentNode>();

        // fixme - see node in ContentView re. root content & children
        private readonly HashSet<int> _rootContentIds = new HashSet<int>();
        private int[] _rootContentIdsSnap = {};
        private bool _rootContentIdsDirty;

        private readonly Dictionary<int, PublishedContentType> _contentTypes = new Dictionary<int, PublishedContentType>();
        private Dictionary<int, PublishedContentType> _contentTypesSnap = new Dictionary<int, PublishedContentType>();
        private bool _contentTypesDirty;

        private ContentView _topView;

        private readonly int _minViewsInterval;
        private readonly int _viewsCollectInterval;
        private readonly bool _trackViews;
        private readonly SynchronizedCollection<WeakReference> _views;
        private volatile Task _collectTask;
        private DateTime _lastCollect = DateTime.MinValue;
        private DateTime _lastViewTime;
        private bool _frozen;

        #region Constructors

        public ContentStore()
            : this(new Options())
        { }

        public ContentStore(Options options)
        {
            _minViewsInterval = options.MinViewsInterval;
            _viewsCollectInterval = options.ViewsCollectInternal;
            _trackViews = options.TrackViews;
            if (_trackViews == false) return;
            _views = new SynchronizedCollection<WeakReference>();
        }

        public class Options
        {
            // note: what is the appropriate MinViewsInterval?
            // if it's too long then changes will NOT become visible, and that's bad
            // so setting it to 0 here - might require experimenting

            public int MinViewsInterval = 0;

            // beware! tracking views has an impact on performances!

            public bool TrackViews = false;
            public int ViewsCollectInternal = 1000;
        }

        #endregion

        #region Set, Clear

        public void Set(ContentNode content)
        {
            if (content == null)
                throw new ArgumentNullException("content");
            if (content.ChildContentIds.Count > 0)
                throw new ArgumentException("Content cannot have children.");

            Locker.EnterWriteLock();
            try
            {
                // get existing
                ContentNode existing;
                AllContent.TryGetValue(content.Id, out existing); // else null

                // moving?
                var moving = existing != null && existing.ParentContentId != content.ParentContentId;

                // update top view (to mask change) and set
                if (_topView != null)
                {
                    if (existing == null)
                        _topView.Clear(content.Id);
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

                // manage the content type
                PublishedContentType contentType;
                if (_contentTypes.TryGetValue(content.ContentType.Id, out contentType))
                {
                    if (ReferenceEquals(contentType, content.ContentType) == false)
                    {
                        // existed, has changed, update
                        // must be frozen because we are creating an inconsistent state where some
                        // content point to the old content type and some point to the new one
                        if (_frozen == false)
                            throw new InvalidOperationException("Cannot replace a content type while not frozen.");
                        _contentTypes[content.ContentType.Id] = content.ContentType;
                        _contentTypesDirty = true;
                    }
                }
                else
                {
                    // new, add
                    _contentTypes[content.ContentType.Id] = content.ContentType;
                    _contentTypesDirty = true;
                }
            }
            finally
            {
                Locker.ExitWriteLock();
            }
        }

        public void Clear(int id)
        {
            Locker.EnterWriteLock();
            try
            {
                // try to find the content
                // if it is not there, nothing to do
                ContentNode content;
                AllContent.TryGetValue(id, out content); // else null
                if (content == null) return;

                // update top view (to mask change) then remove
                CopyBranch(content, true);

                // manage the tree
                RemoveFromParent(content);
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

        private void CopyBranch(ContentNode content, bool clear)
        {
            if (_topView != null)
                _topView.Set(content);
            if (clear)
                AllContent.Remove(content.Id);

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
            if (_topView != null)
            {
                _topView.Set(parent);
                parent = parent.CloneParent();
                AllContent[parent.Id] = parent;
            }
            return parent;
        }

        public void ClearContentType(int id)
        {
            Locker.EnterWriteLock();
            try
            {
                _contentTypes.Remove(id);
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
                _contentTypesSnap = new Dictionary<int, PublishedContentType>(_contentTypes);
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

        public void Freeze(bool frozen)
        {
            Locker.EnterWriteLock();
            try
            {
                _frozen = frozen;
            }
            finally 
            {
                Locker.ExitWriteLock();
            }
        }

        #endregion
    }
}
