using System;
using System.Collections.Generic;
using Umbraco.Core.Events;

namespace Umbraco.Core.Services
{
    // can be nested - commits when outermost is disposed
    // is NOT thread-safe
    // rename to UnitOfChange, ChangeUnit...?

    internal class ChangeSet
    {
        private int _refsCount;
        private readonly Dictionary<string, object> _items = new Dictionary<string, object>();
        private const string ContextKey = "Umbraco.Core.Services.AmbientChangeSet";

        public IDictionary<string, object> Items { get { return _items; } }

        private ChangeSet()
        { }

        public class ChangeSetPtr : IDisposable
        {
            private readonly ChangeSet _changeSet;

            internal ChangeSetPtr(ChangeSet changeSet)
            {
                _changeSet = changeSet;
                _changeSet.RegisterReference();
            }

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
        // must dispose to commit and for events to trigger
        // no way to prevent commit & events at the moment (no rollback)
        //
        // if (ChangeSet.HasAmbient)
        //   ChangeSet.Ambient.Items[...] = ...;
        //
        // bad idea to use a ChangeSet while handling Committed!

        internal static TypedEventHandler<ChangeSet, EventArgs> Committed;

        internal static ChangeSetPtr WithAmbient
        {
            get
            {
                var cs = (ChangeSet) CurrentContextItems.Get(ContextKey);
                if (cs == null)
                    CurrentContextItems.Set(ContextKey, cs = new ChangeSet());
                return new ChangeSetPtr(cs);
            }
        }

        internal static ChangeSet Ambient
        {
            get { return (ChangeSet) CurrentContextItems.Get(ContextKey); }
        }

        internal static bool HasAmbient
        {
            get { return CurrentContextItems.Get(ContextKey) != null; }
        }

        private void RegisterReference()
        {
            if (_refsCount < 0)
                throw new Exception("ChangeSet has already been committed.");

            _refsCount++;
        }

        private void ReleaseReference()
        {
            if (_refsCount < 0)
                throw new Exception("ChangeSet has already been committed.");
            if (--_refsCount > 0) return;

            // _refsCount == 0, set to -1 to lock the ChangeSet
            _refsCount = -1;

            // make sure we remove the ChangeSet even if the handler throws
            try
            {
                var handler = Committed;
                if (handler != null) handler(this, EventArgs.Empty);
            }
            finally
            {
                CurrentContextItems.Clear(ContextKey);
            }
        }
    }
}