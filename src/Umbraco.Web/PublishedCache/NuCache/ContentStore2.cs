using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Umbraco.Core.Logging;
using Umbraco.Core.Models.PublishedContent;

namespace Umbraco.Web.PublishedCache.NuCache
{
    // stores content
    internal class ContentStore2
    {
        // this class is an extended version of SnapDictionary
        // most of the snapshots management code, etc is an exact copy
        // SnapDictionary has unit tests to ensure it all works correctly

        private readonly ConcurrentDictionary<int, LinkedNode<ContentNode>> _contentNodes;
        private readonly ConcurrentDictionary<int, LinkedNode<object>> _contentRootNodes;
        private readonly ConcurrentDictionary<int, LinkedNode<PublishedContentType>> _contentTypesById;
        private readonly ConcurrentDictionary<string, LinkedNode<PublishedContentType>> _contentTypesByAlias;
        private readonly Dictionary<int, HashSet<int>> _contentTypeNodes;

        private readonly ILogger _logger;
        private readonly ConcurrentQueue<WeakReference> _snapshots;
        private WeakReference _snapshot;
        private readonly object _wlocko = new object();
        private readonly object _rlocko = new object();
        private long _liveGen, _floorGen;
        private bool _nextGen, _collectAuto;
        private Task _collectTask;
        private int _wlocked;

        // fixme
        // minGenDelta to be adjusted
        // we may want to throttle collects even if delta is reached
        // we may want to force collect if delta is not reached but very old
        private const long CollectMinGenDelta = 4;

        #region Ctor

        public ContentStore2(ILogger logger)
        {
            _logger = logger;

            _contentNodes = new ConcurrentDictionary<int, LinkedNode<ContentNode>>();
            _contentRootNodes = new ConcurrentDictionary<int, LinkedNode<object>>();
            _contentTypesById = new ConcurrentDictionary<int, LinkedNode<PublishedContentType>>();
            _contentTypesByAlias = new ConcurrentDictionary<string, LinkedNode<PublishedContentType>>(StringComparer.InvariantCultureIgnoreCase);
            _contentTypeNodes = new Dictionary<int, HashSet<int>>();

            _snapshots = new ConcurrentQueue<WeakReference>();
            _snapshot = null; // no initial snapshot exists
            _liveGen = _floorGen = 0;
            _nextGen = false; // first time, must create a snapshot
            _collectAuto = true; // collect automatically by default
        }

        #endregion

        #region Locking

        public void WriteLocked(Action action)
        {
            var wtaken = false;
            var wcount = false;
            try
            {
                Monitor.Enter(_wlocko, ref wtaken);

                var rtaken = false;
                try
                {
                    Monitor.Enter(_rlocko, ref rtaken);

                    // see SnapDictionary
                    try
                    { }
                    finally
                    {
                        _wlocked++;
                        wcount = true;
                        if (_nextGen == false)
                        {
                            // because we are changing things, a new generation
                            // is created, which will trigger a new snapshot
                            _nextGen = true;
                            _liveGen += 1;
                        }
                    }
                }
                finally
                {
                    if (rtaken) Monitor.Exit(_rlocko);
                }

                action();
            }
            finally
            {
                if (wcount) _wlocked--;
                if (wtaken) Monitor.Exit(_wlocko);
            }
        }

        public T WriteLocked<T>(Func<T> func)
        {
            var wtaken = false;
            var wcount = false;
            try
            {
                Monitor.Enter(_wlocko, ref wtaken);

                var rtaken = false;
                try
                {
                    Monitor.Enter(_rlocko, ref rtaken);

                    // see SnapDictionary
                    try
                    { }
                    finally
                    {
                        _wlocked++;
                        wcount = true;
                        if (_nextGen == false)
                        {
                            // because we are changing things, a new generation
                            // is created, which will trigger a new snapshot
                            _nextGen = true;
                            _liveGen += 1;
                        }
                    }
                }
                finally
                {
                    if (rtaken) Monitor.Exit(_rlocko);
                }

                return func();
            }
            finally
            {
                if (wcount) _wlocked--;
                if (wtaken) Monitor.Exit(_wlocko);
            }
        }

        private T ReadLocked<T>(Func<bool, T> func)
        {
            var rtaken = false;
            try
            {
                Monitor.Enter(_rlocko, ref rtaken);

                // we have rlock, so it cannot ++
                // it could -- but that's not important
                var wlocked = Volatile.Read(ref _wlocked) > 0;
                return func(wlocked);
            }
            finally
            {
                if (rtaken) Monitor.Exit(_rlocko);
            }
        }

        #endregion

        #region Content types

        public void UpdateContentTypes(IEnumerable<int> removedIds, IEnumerable<PublishedContentType> refreshedTypes, IEnumerable<ContentNodeKit> kits)
        {
            removedIds = removedIds ?? Enumerable.Empty<int>();
            refreshedTypes = refreshedTypes ?? Enumerable.Empty<PublishedContentType>();
            kits = kits ?? new ContentNodeKit[0];

            WriteLocked(() =>
            {
                foreach (var id in removedIds)
                {
                    // all content should have been deleted - but
                    if (_contentTypeNodes.ContainsKey(id))
                    {
                        foreach (var node in _contentTypeNodes[id])
                            ClearBranchLocked(node);
                        _contentTypeNodes.Remove(id);
                    }

                    LinkedNode<PublishedContentType> link;
                    if (_contentTypesById.TryGetValue(id, out link) == false || link.Value == null)
                        continue;
                    SetValueLocked(_contentTypesById, id, null);
                    SetValueLocked(_contentTypesByAlias, link.Value.Alias, null);
                }

                var temp = new Dictionary<int, HashSet<int>>();

                foreach (var type in refreshedTypes)
                {
                    if (_contentTypeNodes.ContainsKey(type.Id) == false)
                        _contentTypeNodes[type.Id] = new HashSet<int>();

                    SetValueLocked(_contentTypesById, type.Id, type);
                    SetValueLocked(_contentTypesByAlias, type.Alias, type);

                    temp.Add(type.Id, new HashSet<int>(_contentTypeNodes[type.Id]));
                }

                // skip missing type
                // skip missing parents & unbuildable kits - what else could we do?
                foreach (var kit in kits.Where(x =>
                    temp.ContainsKey(x.ContentTypeId) &&
                    ParentExistsLocked(x) &&
                    BuildKit(x)))
                {
                    SetValueLocked(_contentNodes, kit.Node.Id, kit.Node);
                    temp[kit.ContentTypeId].Remove(kit.Node.Id);
                }

                // all content should have been refreshed - but...
                foreach (var id in temp.Values.SelectMany(x => x))
                    ClearBranchLocked(id);
            });
        }

        public void UpdateDataTypes(IEnumerable<int> dataTypeIds, Func<int, PublishedContentType> getContentType)
        {
            WriteLocked(() =>
            {
                var contentTypes = _contentTypesById
                    .Where(kvp =>
                        kvp.Value.Value != null &&
                        kvp.Value.Value.PropertyTypes.Any(p => dataTypeIds.Contains(p.DataTypeId)))
                    .Select(kvp => kvp.Value.Value)
                    .Select(x => getContentType(x.Id));

                foreach (var contentType in contentTypes)
                {
                    if (contentType == null)
                    {
                        // fixme - poof, it's gone, now what?
                        continue;
                    }

                    if (_contentTypeNodes.ContainsKey(contentType.Id) == false)
                        continue; // though, ?!

                    foreach (var id in _contentTypeNodes[contentType.Id])
                    {
                        LinkedNode<ContentNode> link;
                        _contentNodes.TryGetValue(id, out link);
                        if (link == null || link.Value == null)
                            continue;
                        var node = new ContentNode(link.Value, contentType);
                        SetValueLocked(_contentNodes, id, node);
                    }
                }
            });
        }

        private bool BuildKit(ContentNodeKit kit)
        {
            LinkedNode<PublishedContentType> link;

            // unknown = bad
            if (_contentTypesById.TryGetValue(kit.ContentTypeId, out link) == false || link.Value == null)
                return false;
            
            // not checking ByAlias, assuming we don't have internal errors

            // register
            if (_contentTypeNodes.ContainsKey(kit.ContentTypeId) == false)
                _contentTypeNodes[kit.ContentTypeId] = new HashSet<int>();
            _contentTypeNodes[kit.ContentTypeId].Add(kit.Node.Id);

            // and use
            kit.Build(link.Value);

            return true;
        }

        private void ReleaseContentTypeLocked(ContentNode content)
        {
            if (_contentTypeNodes.ContainsKey(content.ContentType.Id) == false)
                return; // though, ?!
            _contentTypeNodes[content.ContentType.Id].Remove(content.Id);
        }

        #endregion

        #region Set, Clear, Get

        public int Count
        {
            get { return _contentNodes.Count; }
        }

        private LinkedNode<TValue> GetHead<TKey, TValue>(ConcurrentDictionary<TKey, LinkedNode<TValue>> dict, TKey key)
            where TValue : class
        {
            LinkedNode<TValue> link;
            dict.TryGetValue(key, out link); // else null
            return link;
        }

        public void Set(ContentNodeKit kit)
        {
            if (kit.IsEmpty)
                throw new ArgumentException("Kit is empty.", "kit");
            if (kit.Node.ChildContentIds.Count > 0)
                throw new ArgumentException("Kit content cannot have children.", "kit");

            _logger.Debug<ContentStore2>("Set content ID:" + kit.Node.Id);

            WriteLocked(() =>
            {
                // get existing
                LinkedNode<ContentNode> link;
                _contentNodes.TryGetValue(kit.Node.Id, out link);
                var existing = link == null ? null : link.Value;

                // else ignore, what else could we do?
                if (ParentExistsLocked(kit) == false || BuildKit(kit) == false)
                    return;

                // moving?
                var moving = existing != null && existing.ParentContentId != kit.Node.ParentContentId;

                // manage children
                if (existing != null)
                    kit.Node.ChildContentIds = existing.ChildContentIds;

                // set
                SetValueLocked(_contentNodes, kit.Node.Id, kit.Node);

                // manage the tree
                if (existing == null)
                {
                    // new, add to parent
                    AddToParentLocked(kit.Node);
                }
                else if (moving)
                {
                    // moved, remove existing from its parent, add content to its parent
                    RemoveFromParentLocked(existing);
                    AddToParentLocked(kit.Node);
                }
            });
        }

        public void SetAll(IEnumerable<ContentNodeKit> kits)
        {
            WriteLocked(() =>
            {
                ClearLocked(_contentNodes);
                ClearLocked(_contentRootNodes);

                // do NOT clear types else they are gone!
                //ClearLocked(_contentTypesById);
                //ClearLocked(_contentTypesByAlias);

                // skip missing parents & unbuildable kits - what else could we do?
                foreach (var kit in kits.Where(x => ParentExistsLocked(x) && BuildKit(x)))
                {
                    SetValueLocked(_contentNodes, kit.Node.Id, kit.Node);
                    AddToParentLocked(kit.Node);
                }
            });
        }

        public void SetBranch(int rootContentId, IEnumerable<ContentNodeKit> kits)
        {
            WriteLocked(() =>
            {
                // get existing
                LinkedNode<ContentNode> link;
                _contentNodes.TryGetValue(rootContentId, out link);
                var existing = link == null ? null : link.Value;

                // clear
                if (existing != null)
                {
                    ClearBranchLocked(existing);
                    RemoveFromParentLocked(existing);
                }

                // now add them all back
                // skip missing parents & unbuildable kits - what else could we do?
                foreach (var s in kits.Where(x => ParentExistsLocked(x) && BuildKit(x)))
                {
                    SetValueLocked(_contentNodes, s.Node.Id, s.Node);
                    AddToParentLocked(s.Node);
                }
            });
        }

        public bool Clear(int id)
        {
            return WriteLocked(() =>
            {
                // try to find the content
                // if it is not there, nothing to do
                LinkedNode<ContentNode> link;
                _contentNodes.TryGetValue(id, out link); // else null
                if (link == null || link.Value == null) return false;

                var content = link.Value;
                _logger.Debug<ContentStore2>("Clear content ID:" + content.Id);

                // clear the entire branch
                ClearBranchLocked(content);

                // manage the tree
                RemoveFromParentLocked(content);

                return true;
            });
        }

        private void ClearBranchLocked(int id)
        {
            LinkedNode<ContentNode> link;
            _contentNodes.TryGetValue(id, out link);
            if (link == null || link.Value == null)
                return;
            ClearBranchLocked(link.Value);
        }

        private void ClearBranchLocked(ContentNode content)
        {
            SetValueLocked(_contentNodes, content.Id, null);
            ReleaseContentTypeLocked(content);
            foreach (var childId in content.ChildContentIds)
            {
                LinkedNode<ContentNode> link;
                if (_contentNodes.TryGetValue(childId, out link) == false || link.Value == null) continue;
                ClearBranchLocked(link.Value);
            }
        }

        private LinkedNode<ContentNode> GetParentLink(ContentNode content)
        {
            LinkedNode<ContentNode> link;
            _contentNodes.TryGetValue(content.ParentContentId, out link); // else null
            //if (link == null || link.Value == null)
            //    throw new Exception("Panic: parent not found.");
            return link;
        }

        private void RemoveFromParentLocked(ContentNode content)
        {
            // remove from root content index,
            // or parent's children index
            if (content.ParentContentId < 0)
            {
                SetValueLocked(_contentRootNodes, content.Id, null);
            }
            else
            {
                // obviously parent has to exist
                var link = GetParentLink(content);
                var parent = link.Value;
                if (link.Gen < _liveGen)
                    parent = parent.CloneParent();
                parent.ChildContentIds.Remove(content.Id);
                if (link.Gen < _liveGen)
                    SetValueLocked(_contentNodes, parent.Id, parent);
            }
        }

        private bool ParentExistsLocked(ContentNodeKit kit)
        {
            if (kit.Node.ParentContentId < 0)
                return true;
            var link = GetParentLink(kit.Node);
            return link != null && link.Value != null;
        }

        private void AddToParentLocked(ContentNode content)
        {
            // add to root content index,
            // or parent's children index
            if (content.ParentContentId < 0)
            {
                // need an object reference... just use this...
                SetValueLocked(_contentRootNodes, content.Id, this);
            }
            else
            {
                // assume parent has been validated and exists
                var link = GetParentLink(content);
                var parent = link.Value;
                if (link.Gen < _liveGen)
                    parent = parent.CloneParent();
                parent.ChildContentIds.Add(content.Id);
                if (link.Gen < _liveGen)
                    SetValueLocked(_contentNodes, parent.Id, parent);
            }
        }

        private void SetValueLocked<TKey, TValue>(ConcurrentDictionary<TKey, LinkedNode<TValue>> dict, TKey key, TValue value)
            where TValue : class
        {
                // this is safe only because we're write-locked
                var link = GetHead(dict, key);
                if (link != null)
                {
                    // already in the dict
                    if (link.Gen != _liveGen)
                    {
                        // for an older gen - if value is different then insert a new 
                        // link for the new gen, with the new value
                        if (link.Value != value)
                            dict.TryUpdate(key, new LinkedNode<TValue>(value, _liveGen, link), link);
                    }
                    else
                    {
                        // for the live gen - we can fix the live gen - and remove it
                        // if value is null and there's no next gen
                        if (value == null && link.Next == null)
                            dict.TryRemove(key, out link);
                        else
                            link.Value = value;
                    }
                }
                else
                {
                    dict.TryAdd(key, new LinkedNode<TValue>(value, _liveGen));
                }
        }

        private void ClearLocked<TKey, TValue>(ConcurrentDictionary<TKey, LinkedNode<TValue>> dict)
            where TValue : class
        {
            foreach (var kvp in dict.Where(x => x.Value != null))
            {
                if (kvp.Value.Gen < _liveGen)
                {
                    var link = new LinkedNode<TValue>(null, _liveGen, kvp.Value);
                    dict.TryUpdate(kvp.Key, link, kvp.Value);
                }
                else
                {
                    kvp.Value.Value = null;
                }
            }
        }

        public ContentNode Get(int id, long gen)
        {
            return GetValue(_contentNodes, id, gen);
        }

        public IEnumerable<ContentNode> GetAtRoot(long gen)
        {
            // look ma, no lock!
            foreach (var kvp in _contentRootNodes)
            {
                var link = kvp.Value;
                while (link != null)
                {
                    if (link.Gen <= gen)
                        break;
                    link = link.Next;
                }
                if (link != null && link.Value != null)
                    yield return Get(kvp.Key, gen);
            }
        }

        private TValue GetValue<TKey, TValue>(ConcurrentDictionary<TKey, LinkedNode<TValue>> dict, TKey key, long gen)
            where TValue : class
        {
            // look ma, no lock!
            var link = GetHead(dict, key);
            while (link != null)
            {
                if (link.Gen <= gen)
                    return link.Value; // may be null
                link = link.Next;
            }
            return null;
        }

        public bool IsEmpty(long gen)
        {
            var has = _contentNodes.Any(x =>
            {
                var link = x.Value;
                while (link != null)
                {
                    if (link.Gen <= gen && link.Value != null)
                        return true;
                    link = link.Next;
                }
                return false;
            });
            return has == false;
        }

        public PublishedContentType GetContentType(int id, long gen)
        {
            return GetValue(_contentTypesById, id, gen);
        }

        public PublishedContentType GetContentType(string alias, long gen)
        {
            return GetValue(_contentTypesByAlias, alias, gen);
        }

        #endregion

        #region Snapshots

        // at the moment we create snapshots and give them to facades. these snapshot will not
        // be collected until a CLR GC has completely removed them. so if the CLR decides to
        // GC, it will just remove the snapshots, and then we will deference the contents the
        // next time we collect, and another GC will be needed to remove the contents.
        //
        // could we handle explicit snapshot terminations? we'd need to make the snapshots
        // disposable, somehow manage our own reference counting to know how many snapshots
        // are currently using a given store generation - is it worth it?
        //
        // we would create a new snapshot object each time CreateSnapshot() is invoked, and
        // the queue would contain objects: { gen, refCount }... BUT then what-if a snapshot
        // is not properly disposed and the whole queue is blocked? better do with eg
        // objects: { get, refCount, weak(refPtr) } and a refPtr is just an object that each
        // snapshot references - so that if a snapshot is not disposed but eventually GCed,
        // after a while all refs to refPtr will drop, and we know we're ok

        public Snapshot CreateSnapshot()
        {
            return ReadLocked(wlocked =>
            {
                // if no next generation is required, and the last snapshot is
                // still there, return that last snapshot
                var snapshot = _snapshot == null ? null : (Snapshot) _snapshot.Target;
                if (_nextGen == false && snapshot != null)
                    return snapshot;

                snapshot = new Snapshot(this, _liveGen);
                var wref = new WeakReference(snapshot);
                _snapshots.Enqueue(wref);
                _snapshot = wref;
                _nextGen = false;

                // reading _floorGen is safe if _collect is null
                if (_collectTask == null && _collectAuto && _liveGen - _floorGen > CollectMinGenDelta)
                    CollectAsyncLocked();

                return snapshot;
            });
        }

        public Task CollectAsync()
        {
            lock (_rlocko)
            {
                return CollectAsyncLocked();
            }
        }

        private Task CollectAsyncLocked()
        {
            if (_collectTask != null)
                return _collectTask;
            
            // ReSharper disable InconsistentlySynchronizedField
            var task = _collectTask = Task.Run(() => Collect());
            _collectTask.ContinueWith(_ =>
            {
                lock (_rlocko)
                {
                    _collectTask = null;
                }
            }, TaskContinuationOptions.ExecuteSynchronously);
            // ReSharper restore InconsistentlySynchronizedField

            return task;
        }

        private void Collect()
        {
            // if we keep queuing views & disposing them fast enough then
            // that 'while' loop may never end - but does it really make
            // any sense?

            // process the queue and dequeue dead wrefs, ie figure out which
            // generations we can get rid of, by incrementing floorGen - this
            // does not need any lock
            WeakReference wref;
            while (_snapshots.TryPeek(out wref) && wref.IsAlive == false)
            {
                _snapshots.TryDequeue(out wref); // cannot fail since TryPeek has succeeded
                _floorGen += 1;
            }

            Collect(_contentNodes);
            Collect(_contentRootNodes);
            Collect(_contentTypesById);
            Collect(_contentTypesByAlias);
        }

        private void Collect<TKey, TValue>(ConcurrentDictionary<TKey, LinkedNode<TValue>> dict)
            where TValue : class
        {
            // it is OK to enumerate a concurrent dictionary and it does not lock
            // it - and here it's not an issue if we skip some items, they will be
            // processed next time we collect

            long liveGen;
            lock (_wlocko)
            {
                liveGen = _liveGen;
                if (_nextGen == false)
                    liveGen += 1;
            }

            foreach (var kvp in dict)
            {
                var link = kvp.Value;

                // take care of standalone null entries
                // not live means .Next and .Value are safe
                if (link.Gen < liveGen && link.Next == null && link.Value == null)
                {
                    // remove, but only if the dict has not been updated, have to do it
                    // via ICollection<> (thanks Mr Toub) and if the dict has been updated
                    // there is nothing to collect
                    var idict = dict as ICollection<KeyValuePair<TKey, LinkedNode<TValue>>>;
                    idict.Remove(new KeyValuePair<TKey, LinkedNode<TValue>>(kvp.Key, link));
                    continue;
                }

                // .Next is not null, or .Value is not null
                // standalone non-null entry, don't remove it
                if (link.Next == null)
                    continue;

                while (link.Next != null && link.Next.Gen > _floorGen)
                    link = link.Next;
                link.Next = null;
            }
        }

        public async Task WaitForPendingCollect()
        {
            Task task;
            lock (_rlocko)
            {
                task = _collectTask;
            }
            if (task != null)
                await task;
        }

        public long SnapCount
        {
            get { return _snapshots.Count; }
        }

        #endregion

        #region Unit testing

        private TestHelper _unitTesting;

        // note: nothing here is thread-safe
        internal class TestHelper
        {
            private readonly ContentStore2 _store;

            public TestHelper(ContentStore2 store)
            {
                _store = store;
            }

            public long LiveGen { get { return _store._liveGen; } }
            public long FloorGen { get { return _store._floorGen; } }
            public bool NextGen { get { return _store._nextGen; } }
            public bool CollectAuto { get { return _store._collectAuto; } set { _store._collectAuto = value; } }

            public Tuple<long, ContentNode>[] GetValues(int id)
            {
                LinkedNode<ContentNode> link;
                _store._contentNodes.TryGetValue(id, out link); // else null

                if (link == null)
                    return new Tuple<long, ContentNode>[0];

                var tuples = new List<Tuple<long, ContentNode>>();
                do
                {
                    tuples.Add(Tuple.Create(link.Gen, link.Value));
                    link = link.Next;
                } while (link != null);
                return tuples.ToArray();
            }
        }

        internal TestHelper Test { get { return _unitTesting ?? (_unitTesting = new TestHelper(this)); } }
        
        #endregion

        #region Classes

        private class LinkedNode<TValue>
            where TValue: class
        {
            public LinkedNode(TValue value, long gen, LinkedNode<TValue> next = null)
            {
                Value = value;
                Gen = gen;
                Next = next;
            }

            internal readonly long Gen;

            // reading & writing references is thread-safe on all .NET platforms
            // mark as volatile to ensure we always read the correct value
            internal volatile TValue Value;
            internal volatile LinkedNode<TValue> Next;
        }

        public class Snapshot
        {
            private readonly ContentStore2 _store;
            private readonly long _gen;

            internal Snapshot(ContentStore2 store, long gen)
            {
                _store = store;
                _gen = gen;
            }

            public ContentNode Get(int id)
            {
                return _store.Get(id, _gen);
            }

            public IEnumerable<ContentNode> GetAtRoot()
            {
                return _store.GetAtRoot(_gen);
            }

            public PublishedContentType GetContentType(int id)
            {
                return _store.GetContentType(id, _gen);
            }

            public PublishedContentType GetContentType(string alias)
            {
                return _store.GetContentType(alias, _gen);
            }

            public bool IsEmpty { get { return _store.IsEmpty(_gen); } }

            public long Gen { get { return _gen; } }
        }

        #endregion
    }
}
