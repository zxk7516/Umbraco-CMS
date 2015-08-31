using System;
using System.Collections.Generic;
using System.Linq;

namespace Umbraco.Core.Services
{
    [Flags]
    public enum TreeChangeTypes : byte
    {
        None = 0,

        // all items have been refreshed
        RefreshAll = 1,

        // an item node has been refreshed
        // with only local impact
        RefreshNode = 2,

        // an item node has been refreshed
        // with branch impact
        RefreshBranch = 4,

        // an item node has been removed
        // never to return
        Remove = 8,
    }

    internal static class TreeChangeExtensions
    {
        public static TreeChange<TItem>.EventArgs ToEventArgs<TItem>(this IEnumerable<TreeChange<TItem>> changes)
        {
            return new TreeChange<TItem>.EventArgs(changes);
        }

        public static bool HasType(this TreeChangeTypes change, TreeChangeTypes type)
        {
            return (change & type) != TreeChangeTypes.None;
        }

        public static bool HasTypesAll(this TreeChangeTypes change, TreeChangeTypes types)
        {
            return (change & types) == types;
        }

        public static bool HasTypesAny(this TreeChangeTypes change, TreeChangeTypes types)
        {
            return (change & types) != TreeChangeTypes.None;
        }

        public static bool HasTypesNone(this TreeChangeTypes change, TreeChangeTypes types)
        {
            return (change & types) == TreeChangeTypes.None;
        }
    }

    internal class TreeChange<TItem>
    {
        public TreeChange(TItem changedItem, TreeChangeTypes changeTypes)
        {
            Item = changedItem;
            ChangeTypes = changeTypes;
        }

        public TItem Item;
        public TreeChangeTypes ChangeTypes;

        public EventArgs ToEventArgs()
        {
            return new EventArgs(this);
        }

        public class EventArgs : System.EventArgs
        {
            public EventArgs(IEnumerable<TreeChange<TItem>> changes)
            {
                Changes = changes.ToArray();
            }

            public EventArgs(TreeChange<TItem> change)
                : this(new[] { change })
            { }

            public IEnumerable<TreeChange<TItem>> Changes { get; private set; }
        }
    }
}
