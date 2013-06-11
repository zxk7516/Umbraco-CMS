using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using Umbraco.Core.Models;

namespace Umbraco.Web.PublishedCache.NuCache
{
    /// <summary>
    /// Represents a content drop.
    /// </summary>
    // internal for unit tests
    internal class ContentDrop
    {
        protected ConcurrentDictionary<int, IPublishedContent> ContentStore;
        private ContentDrop _nextDrop;
        private readonly bool _hasContent;

        /// <summary>
        /// Initializes a new instance of the <see cref="ContentDrop"/> class.
        /// </summary>
        /// <remarks>For use by <see cref="ContentRoot"/>.</remarks>
        protected ContentDrop()
        { }

        /// <summary>
        /// Initializes a new instance of the <see cref="ContentDrop"/> class, dripping before another drop.
        /// </summary>
        /// <param name="rootDrop">The other drop.</param>
        /// <remarks>Only the <see cref="ContentBucket"/> can create drops. So, here,
        /// <c>nextDrop</c> is always the bucket's root drop. It will change when <c>DippingBefore</c>
        /// is called.</remarks>
        public ContentDrop(ContentRoot rootDrop)
        {
            if (rootDrop == null)
                throw new ArgumentNullException("rootDrop");
            _nextDrop = rootDrop;

            // because that will not change for this drop
            // safe because the ctor is called by the bucket from within a lock
            _hasContent = rootDrop.HasContent;
        }

        /// <summary>
        /// Ensures that the drop has a content store ready to store local content.
        /// </summary>
        /// <remarks>Not thread-safe, but invoked by ContentDrop.Set ie from within a lock.</remarks>
        protected void EnsureInitialized()
        {
            if (ContentStore == null)
                ContentStore = new ConcurrentDictionary<int, IPublishedContent>();
        }

        /// <summary>
        /// Registers that the drop has detached from the root drop and is now dripping before another drop.
        /// </summary>
        /// <param name="nextDrop">The other drop.</param>
        /// <remarks>Only the <see cref="ContentBucket"/> can drip drops. So, here, the
        /// drop is the former top drop, and <c>nextDrop</c> is the new top drop. Once the drop is dripping
        /// it is frozen, ie it is illegal to modify its content.</remarks>
        public void DrippingBefore(ContentDrop nextDrop)
        {
            if (nextDrop == null)
                throw new ArgumentNullException("nextDrop");

            try
            {
                // EnterWriteLock throws if this drop is not the top drop
                EnterWriteLock();

                // so here we know that this drop is the top drop
                // and we can get it to point to the new top drop
                _nextDrop = nextDrop;
            }
            finally
            {
                ExitAndNullWriteLock();
            }
        }

        // gets the next drop, and ensures it is not null (detached)
        // not thread-safe, must call from within a lock!
        private ContentDrop GetNextDrop()
        {
            if (_nextDrop == null)
                throw new InvalidOperationException("ContentDrop has been detached, should not be used anymore.");
            return _nextDrop;
        }

        /// <summary>
        /// Gets a value indicating whether the content drop has content.
        /// </summary>
        public virtual bool HasContent
        {
            get { return _hasContent; }
        }

        /// <summary>
        /// Gets a value indicating whether the drop has local content.
        /// </summary>
        /// <remarks>not thread-safe, must call from within a lock!</remarks>
        public virtual bool HasLocalContent { get { return ContentStore != null && ContentStore.IsEmpty == false; } }

        #region Drops

        private bool IsTopDrop { get { return _dropLock != null; } }

        /// <summary>
        /// Detaches the drop.
        /// </summary>
        public virtual void Detach()
        {
            try
            {
                EnterWriteLock();
                if (IsTopDrop == false)
                    throw new InvalidOperationException("Only the top drop can be detached.");
                _nextDrop = null; // detach
            }
            finally
            {
                ExitWriteLock();
            }
        }

        #endregion

        #region Locking

        private ReaderWriterLockSlim _dropLock = new ReaderWriterLockSlim();

        private void EnterReadLock()
        {
            // if it's null then we're not the top drop anymore, no need to lock
            var dropLock = _dropLock; // capture
            if (dropLock == null) return;

            // else lock and check for race condition
            //  fixme - but what if we enter the lock and then got killed before we can set _dropLocked to true?
            dropLock.EnterReadLock();
            if (_dropLock == null)
                dropLock.ExitReadLock(); // no need to lock, finally
        }

        private void ExitReadLock()
        {
            var dropLock = _dropLock;
            if (dropLock == null) return;

            if (dropLock.IsReadLockHeld)
                dropLock.ExitReadLock();
        }

        private void EnterWriteLock()
        {
            // if it's null then we're not the top drop anymore, throw
            var dropLock = _dropLock; // capture
            if (dropLock == null)
                throw new InvalidOperationException("ContentDrop is not the top drop, cannot write to it anymore.");

            // else lock and check for race condition
            dropLock.EnterWriteLock();
            if (_dropLock == null)
            {
                dropLock.ExitWriteLock();
                throw new InvalidOperationException("ContentDrop is not the top drop, cannot write to it anymore.");
            }
        }

        private void ExitWriteLock()
        {
            var dropLock = _dropLock;
            if (dropLock == null) return;

            if (dropLock.IsWriteLockHeld)
                dropLock.ExitWriteLock();
        }

        private void ExitAndNullWriteLock()
        {
            var dropLock = _dropLock;
            if (dropLock == null) return;

            _dropLock = null;
            if (dropLock.IsWriteLockHeld)
                dropLock.ExitWriteLock();
        }

        #endregion

        #region Getters

        /// <summary>
        /// Gets a content identified by its identifier.
        /// </summary>
        /// <param name="id">The content identifier.</param>
        /// <returns>The content identified by the identifier, or null.</returns>
        /// <remarks>Recursively looks for content up to the root content drop. So the returned
        /// value is the bucket's content identified by the identifier, as seen through this drop.</remarks>
        public virtual IPublishedContent Get(int id)
        {
            IPublishedContent content;
            try
            {
                EnterReadLock();
                content = ContentStore != null && ContentStore.TryGetValue(id, out content) ? content : GetNextDrop().Get(id);
            }
            finally
            {
                ExitReadLock();
            }
            return content;
        }

 
        /// <summary>
        /// Gets all content satisfying a condition.
        /// </summary>
        /// <param name="predicate">A predicate.</param>
        /// <returns>All content satisfying the specified condition.</returns>
        /// <remarks>
        /// <para>Recursively looks for content up to the root content drop. So the returned
        /// value is the bucket's content identified by the identifier, as seen through this drop.</para>
        /// <para><paramref name="predicate"/> can be <c>null</c> and then all content is returned.</para>
        /// </remarks>
        public virtual IEnumerable<IPublishedContent> GetAll(Func<IPublishedContent, bool> predicate = null)
        {
            ContentDrop nextDrop;
            bool isTopDrop;
            try
            {
                EnterReadLock();
                isTopDrop = IsTopDrop;
                nextDrop = GetNextDrop(); // get it while locked 'cos it may change
            }
            finally
            {
                ExitReadLock();
            }

            if (isTopDrop)
                return GetAllRoot(nextDrop, predicate).UnionDistinct1(GetNonRoot(predicate));

            // not the top drop, safe to use HasLocalContent and ContentStore outside a lock
            if (HasLocalContent == false)
                return nextDrop.GetAll(predicate);
            var contentStore = predicate == null ? ContentStore : ContentStore.Where(kvp => predicate(kvp.Value));
            return contentStore.UnionDistinct(nextDrop.GetAll(predicate));
        }

        #endregion

        #region Setters

        /// <summary>
        /// Sets a content.
        /// </summary>
        /// <param name="content">The content to set.</param>
        /// <remarks>
        /// <para>It is illegal to set content on a dripping drop, ie a drop that
        /// is not the root drop neither the top drop.</para>
        /// <para>Only the <see cref="ContentBucket"/> can set a drop's content.</para>
        /// </remarks>
        public virtual void Set(IPublishedContent content)
        {
            if (content == null)
                throw new ArgumentNullException("content");

            // entering the lock will throw if content drop is old ie if it
            // is not the top drop anymore and its content cannot change
            try
            {
                EnterWriteLock();
                EnsureInitialized();
                if (ContentStore.ContainsKey(content.Id) == false)
                    ContentStore[content.Id] = content;
            }
            finally
            {
                ExitWriteLock();
            }
        }

        /// <summary>
        /// Removes a content identified by its identifier.
        /// </summary>
        /// <param name="id">The content identifier.</param>
        /// <remarks>
        /// <para>It is illegal to remove content from a dripping drop, ie a drop that
        /// is not the root drop neither the top drop.</para>
        /// <para>The content is not actually removed from the top drop, but a null value
        /// is registered, so we know that the content is not visible from the drop anymore.</para>
        /// <para>Only the <see cref="ContentBucket"/> can remove content from a drop.</para>
        /// </remarks>
        public virtual void Remove(int id)
        {
            // entering the lock will throw if content drop is old ie if it
            // is not the top drop anymore and its content cannot change
            try
            {
                EnterWriteLock();
                EnsureInitialized();
                if (ContentStore.ContainsKey(id) == false)
                    ContentStore[id] = null;
            }
            finally
            {
                ExitWriteLock();
            }
        }

        #endregion

        #region Infrastructure

        // for every content in the root drop, return how that content is viewed from
        // this drop, ie the content itself, or a shielded version, or nothing.
        private IEnumerable<IPublishedContent> GetAllRoot(ContentDrop rootDrop, Func<IPublishedContent, bool> predicate)
        {
            // the top drop content store is always modified _before_ the root drop content store,
            // so as long as we take care of shielded content we should be safe
            //
            // we are out of locks here and nothing prevents the bucket from inserting a new
            // drop between us and the root drop, and while we iterate the root drop, some
            // changes may be shielded by the intermediate drop, hence we have to Get() everything.
            //
            // and when we Get() we can get null if the content has been shielded as non-existing.

            return (predicate == null ? rootDrop.GetAll() : rootDrop.GetAll(predicate))
                .Select(c =>
                    {
                        IPublishedContent content;
                        return TryGetNonRoot(c.Id, out content) ? content : c;
                    })
                .Where(c => c != null);
        }

        // tries to find a content in the local content ie every drop but not the root drop
        public virtual bool TryGetNonRoot(int id, out IPublishedContent content)
        {
            try
            {
                EnterReadLock();
                if (ContentStore != null && ContentStore.TryGetValue(id, out content))
                {
                    return true;                    
                }
                if (IsTopDrop)
                {
                    content = null;
                    return false;                    
                }
                return GetNextDrop().TryGetNonRoot(id, out content);
            }
            finally
            {
                ExitReadLock();
            }
        }

        // gets every content that's defined in drops, but not in the root drop
        // so we have to stop at the top drop... but since we do it outside a lock,
        // the top drop may change. take care of this.
        public virtual IEnumerable<IPublishedContent> GetNonRoot(Func<IPublishedContent, bool> predicate)
        {
            // at the time that method was called the drop was the top drop, first
            // yield return its own local content
            if (ContentStore != null)
                foreach (var content in ContentStore.Values.Where(x => x != null && (predicate == null || predicate(x))))
                    yield return content;


            // then check whether it still is the top drop
            ContentDrop nextDrop;
            try
            {
                EnterReadLock();
                nextDrop = IsTopDrop ? null : GetNextDrop();
            }
            finally
            {
                ExitReadLock();
            }

            // if the drop is not the top drop anymore, then we want to enumerate the new top drop
            if (nextDrop != null)
                foreach (var content in nextDrop.GetNonRoot(predicate))
                    yield return content;
        }

        #endregion
    }
}
