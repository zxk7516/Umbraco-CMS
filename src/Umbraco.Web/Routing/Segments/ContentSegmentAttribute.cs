using System;

namespace Umbraco.Web.Routing.Segments
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public sealed class ContentSegmentAttribute : Attribute
    {
        public string SegmentName { get; private set; }

        public ContentSegmentAttribute(string segmentName)
        {
            SegmentName = segmentName;
        }
    }
}