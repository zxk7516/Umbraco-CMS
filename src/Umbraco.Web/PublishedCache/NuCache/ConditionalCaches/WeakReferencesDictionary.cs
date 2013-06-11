using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;

namespace Umbraco.Web.PublishedCache.NuCache.ConditionalCaches
{
    class WeakReferencesDictionary<TKey, TValue>
        where TKey : class
    {
        // concurrent so we're concurrent + can be collected without a lock
        private readonly ConcurrentDictionary<WeakReferences<TKey>, TValue> _d =
            new ConcurrentDictionary<WeakReferences<TKey>, TValue>();

        public bool ContainsKey(WeakReferences<TKey> key)
        {
            return _d.ContainsKey(key);
        }

        public TValue this[WeakReferences<TKey> key]
        {
            get { return _d[key]; }
            set
            {
                Collect();
                _d[key] = value;
            }
        }

        public bool TryGetValue(WeakReferences<TKey> key, out TValue value)
        {
            return _d.TryGetValue(key, out value);
        }

        public int Count
        {
            get { return _d.Count; }
        }

        private const int CollectSpan = 1000; // once every second
        private readonly object _locko = new object();
        private DateTime _lastCollect = DateTime.MinValue;
        private volatile Task _ctask;

        // fixme - todo
        // should we also set a timer to ensure we collect in 1 or 2 minutes
        // if not collected in the meantime? just to release memory pressure?

        public void Collect()
        {
            // from C# specs
            //
            // "[...] Reads and writes of the following data types are atomic: bool, char,
            // byte, sbyte, short, ushort, uint, int, float, and reference types. [...]"
            //
            // so maybe we don't need the locks everywhere, but I'm not a multithread guru
            // enough to try it so let's be safe

            //if (_ctask != null) return; // atomic read
            lock (_locko) // lock to run only once, but will run unlocked
            {
                if (_ctask != null) return;
                if ((DateTime.Now - _lastCollect).TotalMilliseconds < CollectSpan) return;

				// _ctask = Task.Factory.StartNew(() => // .NET 4.0
                _ctask = Task.Run(() =>
                    {
                        var remove = _d.Keys.Where(k => k.IsAlive == false).ToArray();
                        TValue tmp;
                        foreach (var k in remove) _d.TryRemove(k, out tmp);

                        _lastCollect = DateTime.Now;
                        //_ctask = null; // atomic write
                        lock (_locko)
                        {
                            _ctask = null;
                        }
                    });
            }
        }

        /// <summary>
        /// Waits for a pending collection to complete, if any.
        /// </summary>
        /// <remarks>
        /// <para>Does not trigger a collection.</para>
        /// <para>When the method returns, another collection may be running, but the
        /// collection that was running (if any) has completed.</para>
        /// <para>The method should be used following a write, to ensure that the
        /// collection that was triggered has completed - mostly for unit tests.</para>
        /// </remarks>
        public void WaitForPendingCollection()
        {
            var ctask = _ctask;
            if (ctask != null) ctask.Wait();
        }
    }
}
