using System;
using System.Threading;
using System.Threading.Tasks;
using Umbraco.Core.Logging;
using Umbraco.Web.Scheduling;

namespace Umbraco.Web.PublishedCache.XmlPublishedCache
{
    /// <summary>
    /// This is the background task runner that persists the xml file to the file system
    /// </summary>
    /// <remarks>
    /// This is used so that all file saving is done on a web aware worker background thread and all logic is performed async so this
    /// process will not interfere with any web requests threads. This is also done as to not require any global locks and to ensure that
    /// if multiple threads are performing publishing tasks that the file will be persisted in accordance with the final resulting
    /// xml structure since the file writes are queued.
    /// </remarks>
    internal class XmlStoreFilePersister : ILatchedBackgroundTask
    {
        private readonly IBackgroundTaskRunner<XmlStoreFilePersister> _runner;
        private readonly ProfilingLogger _logger;
        private readonly XmlStore _store;
        private readonly ManualResetEventSlim _latch = new ManualResetEventSlim(false);
        private readonly object _locko = new object();
        private bool _released;
        private Timer _timer;
        private DateTime _initialTouch;

        private const int WaitMilliseconds = 4000; // save the cache 4s after the last change (ie every 4s min)
        private const int MaxWaitMilliseconds = 30000; // save the cache after some time (ie no more than 30s of changes)

        // save the cache when the app goes down
        public bool RunsOnShutdown { get { return true; } }

        public XmlStoreFilePersister(IBackgroundTaskRunner<XmlStoreFilePersister> runner, XmlStore store, ProfilingLogger logger, bool touched = false)
        {
            _runner = runner;
            _store = store;
            _logger = logger;

            if (runner.TryAdd(this) == false) return;

            if (touched == false) return;

            LogHelper.Debug<XmlStoreFilePersister>("Create new touched, start.");

            _initialTouch = DateTime.Now;
            _timer = new Timer(_ => Release());

            LogHelper.Debug<XmlStoreFilePersister>("Save in {0}ms.", () => WaitMilliseconds);
            _timer.Change(WaitMilliseconds, 0);
        }

        public XmlStoreFilePersister Touch()
        {
            lock (_locko)
            {
                if (_released)
                {
                    LogHelper.Debug<XmlStoreFilePersister>("Touched, was released, create new.");

                    // released, has run or is running, too late, return a new task (adds itself to runner)
                    return new XmlStoreFilePersister(_runner, _store, _logger, true);
                }

                if (_timer == null)
                {
                    LogHelper.Debug<XmlStoreFilePersister>("Touched, was idle, start.");

                    // not started yet, start
                    _initialTouch = DateTime.Now;
                    _timer = new Timer(_ => Release());
                    LogHelper.Debug<XmlStoreFilePersister>("Save in {0}ms.", () => WaitMilliseconds);
                    _timer.Change(WaitMilliseconds, 0);
                    return this;
                }

                // set the timer to trigger in WaitMilliseconds unless we've been touched first more
                // than MaxWaitMilliseconds ago and then release now

                if (DateTime.Now - _initialTouch < TimeSpan.FromMilliseconds(MaxWaitMilliseconds))
                {
                    LogHelper.Debug<XmlStoreFilePersister>("Touched, was waiting, wait.", () => WaitMilliseconds);
                    LogHelper.Debug<XmlStoreFilePersister>("Save in {0}ms.", () => WaitMilliseconds);
                    _timer.Change(WaitMilliseconds, 0);
                }
                else
                {
                    LogHelper.Debug<XmlStoreFilePersister>("Touched, has waited long enough, will save.");
                    //ReleaseLocked();
                }

                return this; // still available
            }
        }

        private void Release()
        {
            lock (_locko)
            {
                ReleaseLocked();
            }
        }

        private void ReleaseLocked()
        {
            LogHelper.Debug<XmlStoreFilePersister>("Timer: save now, release.");
            if (_timer != null)
                _timer.Dispose();
            _timer = null;
            _released = true;

            // if running (because of shutdown) this will have no effect
            // else it tells the runner it is time to run the task
            _latch.Set();
        }

        public WaitHandle Latch
        {
            get { return _latch.WaitHandle; }
        }

        public bool IsLatched
        {
            get { return true; }
        }

        public async Task RunAsync(CancellationToken token)
        {
            LogHelper.Debug<XmlStoreFilePersister>("Run now.");
            await _store.SaveXmlToFileAsync();
        }

        public bool IsAsync
        {
            get { return true; }
        }

        public void Dispose()
        { }

        public void Run()
        {
            throw new NotImplementedException();
        }
    }
}