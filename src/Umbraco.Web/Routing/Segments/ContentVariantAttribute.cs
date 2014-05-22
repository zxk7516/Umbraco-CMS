using System;

namespace Umbraco.Web.Routing.Segments
{
    /// <summary>
    /// Defines a custom Content Variant exposed by a segment provider
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public sealed class ContentVariantAttribute : Attribute
    {
        public string VariantName { get; set; }
        public string SegmentMatchKey { get; set; }
        public object SegmentMatchValue { get; set; }

        /// <summary>
        /// Constructor used to specify the content variant name and the segment key to match, in 
        /// order for the request to match on this variant the value stored against the segmentMatchKey must
        /// be a boolean and must be true.
        /// </summary>
        /// <param name="variantName"></param>
        /// <param name="segmentMatchKey"></param>
        public ContentVariantAttribute(string variantName, string segmentMatchKey)
        {
            VariantName = variantName;
            SegmentMatchKey = segmentMatchKey;
        }

        /// <summary>
        /// Constructor used to specify the content variant name and the segment key to match, in 
        /// order for the request to match on this variant the value stored against the segmentMatchKey must
        /// be equal to the segmentMatchValue specified
        /// </summary>
        /// <param name="variantName"></param>
        /// <param name="segmentMatchKey"></param>
        /// <param name="segmentMatchValue"></param>
        public ContentVariantAttribute(string variantName, string segmentMatchKey, object segmentMatchValue)
        {
            VariantName = variantName;
            SegmentMatchKey = segmentMatchKey;
            SegmentMatchValue = segmentMatchValue;
        }
    }
}