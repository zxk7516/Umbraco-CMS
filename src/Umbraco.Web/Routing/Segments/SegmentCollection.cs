using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Umbraco.Web.Routing.Segments
{
    public class SegmentCollection : KeyedCollection<string, Segment>
    {
        public SegmentCollection(IEnumerable<Segment> segments)
        {
            foreach (var segment in segments)
            {
                //last one in wins
                if (Contains(segment.Name))
                {
                    Remove(segment.Name);                    
                }

                Add(segment);
            }
        }

        internal void AddNew(Segment segment)
        {
            //last one in wins
            if (Contains(segment.Name))
            {
                Remove(segment.Name);
            }

            Add(segment);
        }

        protected override string GetKeyForItem(Segment item)
        {
            return item.Name;
        }
    }
}