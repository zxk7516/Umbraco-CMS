using System;
using System.Collections.Generic;
using Umbraco.Core.Events;

namespace Umbraco.Core.Cache
{
    /// <summary>
    /// Aggregates a set of changes against the distributed cache.
    /// </summary>
    /// <remarks>
    /// <para>Aggregates a set of changes against the distributed cache, that might result from
    /// different events (eg, save several content items), as one distributed event payload,
    /// so that they can be processed as a whole by refreshers (eg, prevent the content
    /// cache from being in a state where some changes have been made, but not all).</para>
    /// <para>The changes will be flushed as one payload when the last reference to the change
    /// set is disposed, or when the change set is explicitely flushed, or when changes for a
    /// different cache refresher are added (a change set can contain changes for only one
    /// refresher at a time).</para>
    /// <para>Not thread safe!</para>
    /// <para>User code can only get references to the ambient change set via
    /// using (ChangeSet.WithAmbient) {...}</para>
    /// </remarks>
    internal class ChangeSet
    {
        private int _refsCount;
        private Guid _refresherGuid;
        private List<object> _items; 
        private const string ContextKey = "Umbraco.Core.Services.AmbientChangeSet";

        /// <summary>
        /// Gets the change set items.
        /// </summary>
        public IList<object> Items { get { return _items; } }

        /// <summary>
        /// Gets the current refresher identifier.
        /// </summary>
        public Guid RefresherGuid { get { return _refresherGuid; } }

        /// <summary>
        /// Gets a value indicating whether the change set is empty.
        /// </summary>
        public bool IsEmpty { get { return _items == null || _items.Count == 0; } }

        private ChangeSet()
        { }

        /// <summary>
        /// Adds a change item for a specified refresher.
        /// </summary>
        /// <param name="refresherGuid">The unique identifier of the refresher.</param>
        /// <param name="items">The change item to add.</param>
        /// <remarks>If the refresher is not the current refresher, then the change set
        /// is flushed before the change item is added to it.</remarks>
        public void Add(Guid refresherGuid, IEnumerable<object> items)
        {
            if (refresherGuid != _refresherGuid)
            {
                Flush();
                _refresherGuid = refresherGuid;
            }
            if (_items == null) _items = new List<object>();
            _items.AddRange(items);
        }

        /// <summary>
        /// Represents a reference to a <see cref="ChangeSet"/>.
        /// </summary>
        /// <remarks>References to change sets must be disposed. When the last reference to a set
        /// is disposed, the set is flushed, ie the payloads it contains are passed to the distributed
        /// cache for further processing.</remarks>
        public class ChangeSetReference : IDisposable
        {
            private readonly ChangeSet _changeSet;

            internal ChangeSetReference(ChangeSet changeSet)
            {
                _changeSet = changeSet;
                _changeSet.RegisterReference();
            }

            /// <summary>
            /// Disposes the reference.
            /// </summary>
            public void Dispose()
            {
                _changeSet.ReleaseReference();
            }
        }

        // usage
        // using (ChangeSet.WithAmbient)
        // {
        //   ...
        // }
        //
        // must dispose to flush and for events to trigger
        // no way to prevent flush & events at the moment (no rollback)
        //
        // bad idea to use a ChangeSet while handling Committed!

        /// <summary>
        /// Triggers when the change set is flushed.
        /// </summary>
        public static TypedEventHandler<ChangeSet, EventArgs> Flushed;

        /// <summary>
        /// Gets a reference to an ambient change set.
        /// </summary>
        /// <remarks>
        /// <para>If no ambient change set current exists, a new ambient
        /// change set is registered. Ambient change sets are registered in
        /// the "current" context via <see cref="CurrentContextItems"/>.</para>
        /// <para>The reference is returned as an <see cref="IDisposable"/> as
        /// all it is used for</para>
        /// </remarks>
        public static IDisposable WithAmbient
        {
            get
            {
                var cs = (ChangeSet) CurrentContextItems.Get(ContextKey);
                if (cs == null)
                    CurrentContextItems.Set(ContextKey, cs = new ChangeSet());
                return new ChangeSetReference(cs);
            }
        }

        /// <summary>
        /// Gets the current change set.
        /// </summary>
        internal static ChangeSet Ambient
        {
            get { return (ChangeSet) CurrentContextItems.Get(ContextKey); }
        }

        /// <summary>
        /// Gets a value indicating whether an ambient change set has
        /// been registered in the current context.
        /// </summary>
        internal static bool HasAmbient
        {
            get { return CurrentContextItems.Get(ContextKey) != null; }
        }

        /// <summary>
        /// Flushes the ambient change set, if any.
        /// </summary>
        public static void FlushAmbient()
        {
            if (HasAmbient)
                Ambient.Flush();
        }

        /// <summary>
        /// Flushes the change set.
        /// </summary>
        private void Flush()
        {
            if (IsEmpty)
                return;

            // make sure we remove the ChangeSet even if the handler throws
            try
            {
                Flushed.RaiseEvent(EventArgs.Empty, this);
            }
            finally
            {
                _items.Clear();
            }
        }

        /// <summary>
        /// Registers a reference to the change set.
        /// </summary>
        private void RegisterReference()
        {
            if (_refsCount < 0)
                throw new Exception("ChangeSet has already been dereferenced.");

            _refsCount++;
        }

        /// <summary>
        /// Release a reference to the change set.
        /// </summary>
        /// <remarks>If the reference is the last one, then the change
        /// set is flushed.</remarks>
        private void ReleaseReference()
        {
            if (_refsCount <= 0)
                throw new Exception("ChangeSet is not referenced.");
            if (--_refsCount > 0) return;

            // _refsCount == 0, set to -1 to lock the ChangeSet
            _refsCount = -1;

            // flush and remove ambient
            try
            {
                Flush();
            }
            finally 
            {
                CurrentContextItems.Clear(ContextKey);
            }
        }
    }
}