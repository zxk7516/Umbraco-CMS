using System;
using System.Runtime.ConstrainedExecution;
using System.Threading;
using System.Threading.Tasks;

namespace Umbraco.Core
{
    // http://blogs.msdn.com/b/pfxteam/archive/2012/02/12/10266988.aspx
    internal class AsyncLock
    {
        private readonly SemaphoreSlim _semaphore;
        private readonly Semaphore _semaphore2;
        private readonly IDisposable _releaser;
        private readonly Task<IDisposable> _releaserTask;

        public AsyncLock()
            : this (null)
        { }

        public AsyncLock(string name)
        {
            // initial count: how many can be granted
            // maximum count: how far can 'count' go when release is called

            if (string.IsNullOrWhiteSpace(name))
                _semaphore = new SemaphoreSlim(1, 1);
            else
                _semaphore2 = new Semaphore(1, 1, name);

            _releaser = CreateReleaser(this);
            _releaserTask = Task.FromResult(_releaser);
        }

        private static IDisposable CreateReleaser(AsyncLock asyncLock)
        {
            return asyncLock._semaphore != null
                ? (IDisposable) new SemaphoreSlimReleaser(asyncLock._semaphore)
                : (IDisposable) new NamedSemaphoreReleaser(asyncLock._semaphore2);
        }

        public Task<IDisposable> LockAsync()
        {
            var wait = _semaphore != null 
                ? _semaphore.WaitAsync() 
                : WaitOneAsync(_semaphore2);

            return wait.IsCompleted ?
                _releaserTask :
                wait.ContinueWith((_, state) => CreateReleaser((AsyncLock) state),
                    this, CancellationToken.None,
                    TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        }

        public IDisposable Lock()
        {
            if (_semaphore != null)
                _semaphore.Wait();
            else
                _semaphore2.WaitOne();
            return _releaser;
        }

        // note - before making that class a struct, read 
        // about "impure methods" and mutating readonly structs...

        private class NamedSemaphoreReleaser : CriticalFinalizerObject, IDisposable
        {
            private readonly Semaphore _semaphore;

            internal NamedSemaphoreReleaser(Semaphore semaphore)
            {
                _semaphore = semaphore;
            }

            public void Dispose()
            {
                Dispose(true);
                GC.SuppressFinalize(this);
            }

            private void Dispose(bool disposing)
            {
                // critical
                _semaphore.Release();
            }

            ~NamedSemaphoreReleaser()
            {
                Dispose(false);
            }
        }

        private class SemaphoreSlimReleaser : IDisposable
        {
            private readonly SemaphoreSlim _semaphore;

            internal SemaphoreSlimReleaser(SemaphoreSlim semaphore)
            {
                _semaphore = semaphore;
            }

            public void Dispose()
            {
                Dispose(true);
                GC.SuppressFinalize(this);
            }

            private void Dispose(bool disposing)
            {
                if (disposing)
                {
                    // normal
                    _semaphore.Release();
                }
            }

            ~SemaphoreSlimReleaser()
            {
                Dispose(false);
            }
        }

        // http://stackoverflow.com/questions/25382583/waiting-on-a-named-semaphore-with-waitone100-vs-waitone0-task-delay100
        // http://blog.nerdbank.net/2011/07/c-await-for-waithandle.html
        // F# has a AwaitWaitHandle method that accepts a time out... and seems pretty complex...
        // version below should be OK

        private static Task WaitOneAsync(WaitHandle handle)
        {
            var tcs = new TaskCompletionSource<object>();
            var callbackHandleInitLock = new object();
            lock (callbackHandleInitLock)
            {
                RegisteredWaitHandle callbackHandle = null;
                // ReSharper disable once RedundantAssignment
                callbackHandle = ThreadPool.RegisterWaitForSingleObject(
                    handle,
                    (state, timedOut) =>
                    {
                        tcs.SetResult(null);

                        // we take a lock here to make sure the outer method has completed setting the local variable callbackHandle.
                        lock (callbackHandleInitLock)
                        {
                            // ReSharper disable once PossibleNullReferenceException
                            // ReSharper disable once AccessToModifiedClosure
                            callbackHandle.Unregister(null);
                        }
                    },
                    /*state:*/ null,
                    /*millisecondsTimeOutInterval:*/ Timeout.Infinite,
                    /*executeOnlyOnce:*/ true);
            }

            return tcs.Task;
        }
    }
}
