using System;
using System.Collections.Generic;
using Umbraco.Core.Events;

namespace Umbraco.Core.Services
{
    // note: does not support nesting
    // note: rename to UnitOfChange, ChangeUnit...?

    internal class ChangeSet : IDisposable
    {
        private bool _disposed;
        private readonly Dictionary<string, object> _items = new Dictionary<string, object>();
        private const string ContextKey = "Umbraco.Core.Services.AmbientChangeSet";

        private ChangeSet()
        { }

        public IDictionary<string, object> Items { get { return _items; } }

        // must be properly disposed for event to trigger
        // if finalized without being disposed, event does not trigger
        // when disposed, event triggers
        // so it's "autocommit" because service events should trigger ONLY
        // if the supporting transaction is committed - no need to rollback

        internal static TypedEventHandler<ChangeSet, EventArgs> Committed;

        internal static ChangeSet WithAmbient
        {
            get
            {
                var cs = (ChangeSet) CurrentContextItems.Get(ContextKey);
                if (cs == null)
                    CurrentContextItems.Set(ContextKey, cs = new ChangeSet());
                else
                    throw new NotSupportedException("Nested ambient change sets are not supported.");
                return cs;
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

        public void Dispose()
        {
            if (_disposed) return;

            var handler = Committed;
            if (handler != null) handler(this, EventArgs.Empty);
            
            CurrentContextItems.Clear(ContextKey);
            _disposed = true;
        }
    }
}