using System;
using Umbraco.Core.Models;
using System.Collections.Generic;
using System.Linq;

namespace Umbraco.Web.PublishedCache.NuCache
{
    /// <summary>
    /// Represents a content bucket.
    /// </summary>
    // internal for unit tests
    internal class ContentBucket
    {
        // note: should get them from config?
        private static int _minNewDropInterval = 1000; // max one drop per second
        private static int _dropsCollectInterval = 1000; // max collect every second
        private static bool _trackDrops; // do not track drops

        private readonly ContentRoot _root;
        private ContentDrop _topDrop;
        private DateTime _lastNewDropTime;
        private readonly object _locko  = new object();
        private bool _frozen;

        private readonly bool _thisTracksDrops;
        private readonly SynchronizedCollection<WeakReference> _drops;
        private volatile System.Threading.Tasks.Task _collectTask;
        private DateTime _lastCollect = DateTime.MinValue;

        #region Constructor

        /// <summary>
        /// Initializes a new instance of the <see cref="ContentBucket"/> class.
        /// </summary>
        public ContentBucket()
        {
            _root = new ContentRoot();

            _thisTracksDrops = _trackDrops;

            if (_thisTracksDrops == false) return;
            _drops = new SynchronizedCollection<WeakReference>();
        }

        // for tests purposes
        internal ContentBucket(int minNewDropInterval, int dropsCollectInterval)
            : this()
        {
            _minNewDropInterval = minNewDropInterval;

            if (_thisTracksDrops == false) return;
            _dropsCollectInterval = dropsCollectInterval;
        }

        // for tests purposes
        // must call before any instance is created
        internal static void EnableDropsTracking()
        {
            _trackDrops = true;
        }

        #endregion

        #region Freeze

        /// <summary>
        /// Freezes the content bucket.
        /// </summary>
        /// <remarks>Freezing a bucket suspends drops creation.</remarks>
        public void Freeze()
        {
            lock (_locko)
            {
                _frozen = true;                
            }
        }

        /// <summary>
        /// Unfreezes the content bucket.
        /// </summary>
        public void UnFreeze()
        {
            lock (_locko)
            {
                _frozen = false;
            }
        }

        #endregion

        #region Drops

        /// <summary>
        /// Gets a drop.
        /// </summary>
        /// <returns>A drop.</returns>
        public ContentDrop GetDrop()
        {
            var now = DateTime.Now;

            if (_thisTracksDrops)
                Collect(false);

            lock (_locko)
            {
                // no top drop yet = time to create a top drop.
                if (_topDrop == null)
                {
                    _topDrop = CreateTopDrop(now);
                    return _topDrop;
                }

                // while top drop has no local content, can keep returning it (bucket hasn't changed).
                // also ensure we don't create drops too fast, so keep returning the same one for some time.
                // also ensure that the bucket is not frozen, else keep returning the same drop.
                // safe to use _topDrop.HasLocalContent because of lock(_locko)
                if (_topDrop.HasLocalContent == false || (now - _lastNewDropTime).TotalMilliseconds < _minNewDropInterval || _frozen)
                {
                    return _topDrop;
                }

                // finally, we need top create a new top drop
                var drop = _topDrop;
                _topDrop = CreateTopDrop(now);
                drop.DrippingBefore(_topDrop); // will need a write-lock on drop
                return _topDrop;
            }
        }

        /// <summary>
        /// Shakes the bucket.
        /// </summary>
        /// <remarks>Shaking the bucket detaches the top drop, and all other drops. These drops
        /// all become unstable and continuing to use them can have unpredictable results. Therefore
        /// Shake should never be called in a running web application. This is for standalone
        /// applications, and for tests.</remarks>
        public void Shake()
        {
            lock (_locko)
            {
                if (_topDrop == null) return;

                _topDrop.Detach();
                _topDrop = null;

                if (_thisTracksDrops)
                    _drops.Clear();
            }
        }

        /// <summary>
        /// Gets the number of dripping drops.
        /// </summary>
        /// <remarks>This includes the top drop, if any.</remarks>
        public int DropsCount
        {
            get
            {
                if (_thisTracksDrops == false)
                    throw new InvalidOperationException("Drops tracking is not enabled.");

                // in a multi-threaded environment this will be an indication,
                // not an exact value, but anyway

                //WipeDrops();
                Collect(true);
                WaitForPendingCollection();
                lock (_locko)
                {
                    return _drops.Count;
                }
            }
        }

        // not thread-safe, must call from within a lock!
        private ContentDrop CreateTopDrop(DateTime creationTime)
        {
            var drop = new ContentDrop(_root);

            if (_thisTracksDrops)
                _drops.Add(new WeakReference(drop));

            _lastNewDropTime = creationTime;
            return drop;
        }

        internal void Collect(bool forceNow)
        {
            if (_thisTracksDrops == false) return;

            // from C# specs
            //
            // "[...] Reads and writes of the following data types are atomic: bool, char,
            // byte, sbyte, short, ushort, uint, int, float, and reference types. [...]"
            //
            // so maybe we don't need the locks everywhere, but I'm not a multithread guru
            // enough to try it so let's be safe

            //if (_collectTask != null) return; // atomic read
            lock (_locko) // lock to run only once, but will run unlocked
            {
                if (_collectTask != null) return;
                if (forceNow == false && (DateTime.Now - _lastCollect).TotalMilliseconds < _dropsCollectInterval) return;
                Console.WriteLine("*1");
                // _collectTask = System.Threading.Tasks.Task.Factory.StartNew(() => // .NET 4.0
                _collectTask = System.Threading.Tasks.Task.Run(() =>
                {
                    Console.WriteLine("*2");
                    var remove = _drops.Where(wref => wref.IsAlive == false).ToArray();
                    foreach (var wref in remove) _drops.Remove(wref);

                    _lastCollect = DateTime.Now;
                    //_collectTask = null; // atomic write
                    lock (_locko)
                    {
                        _collectTask = null;
                    }
                    Console.WriteLine("*3");
                });
                Console.WriteLine("*6");
            }
            Console.WriteLine("*7");
        }
        
        public void WaitForPendingCollection()
        {
            if (_thisTracksDrops == false) return;

            Console.WriteLine("*4");
            var collectTask = _collectTask; // atomic read
            if (collectTask != null) collectTask.Wait();
            Console.WriteLine("*5");
        }

        #endregion

        #region Getters and Setters

        /// <summary>
        /// Gets a content identified by its identifier.
        /// </summary>
        /// <param name="id">The content identifier.</param>
        /// <returns>The content, or null.</returns>
        /// <remarks>Returns the content as it is in the root drop. Not drop-safe.</remarks>
        public IPublishedContent Get(int id)
        {
            lock (_locko)
            {
                return _root.Get(id);
            }
        }

        /// <summary>
        /// Sets a content.
        /// </summary>
        /// <param name="content">The content to set.</param>
        public void Set(IPublishedContent content)
        {
            if (content == null)
                throw new ArgumentNullException("content");

            lock (_locko)
            {
                if (_topDrop != null)
                {
                    var oldContent = _root.Get(content.Id);
                    if (oldContent != null)
                        _topDrop.Set(oldContent);
                    else
                        _topDrop.Remove(content.Id);
               }

                _root.Set(content);
            }
        }

        /// <summary>
        /// Removes a content identified by its identifier.
        /// </summary>
        /// <param name="id">The content identifier.</param>
        public void Remove(int id)
        {
            lock (_locko)
            {
                // removing a content that does not exist will not write anything
                // to the _topDrop, and therefore will not cause a drip
                var oldContent = _root.Get(id);
                if (oldContent != null && _topDrop != null)
                    _topDrop.Set(oldContent);

                _root.Remove(id);
            }
        }

        #endregion
    }
}
