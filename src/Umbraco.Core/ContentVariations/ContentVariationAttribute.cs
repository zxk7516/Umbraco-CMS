using System;

namespace Umbraco.Core.ContentVariations
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public sealed class ContentVariationAttribute : Attribute
    {
        public string SegmentName { get; private set; }

        public ContentVariationAttribute(string segmentName)
        {
            SegmentName = segmentName;
        }
    }
}