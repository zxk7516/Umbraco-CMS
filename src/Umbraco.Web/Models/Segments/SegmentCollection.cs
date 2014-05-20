using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Umbraco.Web.Models.Segments
{
    /// <summary>
    /// A keyed collection of segments
    /// </summary>
    public class SegmentCollection : KeyedCollection<string, Segment>
    {
        public SegmentCollection(IEnumerable<Segment> segments)
        {
            foreach (var segment in segments)
            {
                //last one in wins
                if (Contains(segment.Key))
                {
                    Remove(segment.Key);                    
                }

                Add(segment);
            }
        }

        internal void AddNew(Segment segment)
        {
            //last one in wins
            if (Contains(segment.Key))
            {
                Remove(segment.Key);
            }

            Add(segment);
        }

        protected override string GetKeyForItem(Segment item)
        {
            return item.Key;
        }
    }
}