using System;
using System.Threading;
using System.Threading.Tasks;

namespace Umbraco.Core
{
    // http://blogs.msdn.com/b/pfxteam/archive/2012/02/12/10266988.aspx
    internal class AsyncLock
    {
        //private readonly AsyncSemaphore _mSemaphore;
        private readonly SemaphoreSlim _semaphore;
        private readonly Releaser _releaser;
        private readonly Task<Releaser> _releaserTask;

        public AsyncLock()
        {
            //_semaphore = new AsyncSemaphore(1);
            _semaphore = new SemaphoreSlim(1);
            _releaser = new Releaser(this);
            _releaserTask = Task.FromResult(_releaser);
        }

        public Task<Releaser> LockAsync()
        {
            var wait = _semaphore.WaitAsync();
            return wait.IsCompleted ?
                _releaserTask :
                wait.ContinueWith((_, state) => new Releaser((AsyncLock) state),
                    this, CancellationToken.None,
                    TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        }

        public Releaser Lock()
        {
            _semaphore.Wait();
            return _releaser;
        }

        public struct Releaser : IDisposable
        {
            private readonly AsyncLock _mToRelease;

            internal Releaser(AsyncLock toRelease) { _mToRelease = toRelease; }

            public void Dispose()
            {
                if (_mToRelease != null)
                    _mToRelease._semaphore.Release();
            }
        }
    }
}
