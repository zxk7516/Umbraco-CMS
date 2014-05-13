using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using Umbraco.Web.Routing.Segments;

namespace Umbraco.Web.Routing
{
    public class RequestSegments
    {
        private readonly IEnumerable<ContentSegmentProvider> _segmentProviders;
        private readonly Lazy<IDictionary<string, object>> _assignedSegments;

        public RequestSegments(IEnumerable<ContentSegmentProvider> segmentProviders, 
            Uri originalRequestUrl,
            Uri cleanedRequestUrl,
            HttpRequestBase httpRequest)
        {
            _segmentProviders = segmentProviders;

            _assignedSegments = new Lazy<IDictionary<string, object>>(() =>
                GetAllSegmentsForRequest(
                    _segmentProviders,
                    originalRequestUrl,
                    cleanedRequestUrl,
                    httpRequest));
        }

        /// <summary>
        /// Returns the assigned segments for the current request
        /// </summary>
        public IDictionary<string, object> AssignedSegments
        {
            get { return _assignedSegments.Value; }
        }

        /// <summary>
        /// Returns true if any assigned segment value that is a boolean is set to true that matches the specified key
        /// </summary>
        /// <param name="segmentKey"></param>
        /// <returns></returns>
        /// <remarks>
        /// Example: RequestIs("Mobile") if a segment key is "Mobile" and it's value is a boolean true.
        /// </remarks>
        public bool RequestIs(string segmentKey)
        {
            return AssignedSegments.Any(x => x.Key == segmentKey && x.Value is bool && (bool)x.Value);
        }

        /// <summary>
        /// Returns true if any assigned segment has a value equal to the one specified
        /// </summary>
        /// <param name="segmentVal"></param>
        /// <returns></returns>
        public bool RequestContains(string segmentVal)
        {
            return AssignedSegments.Any(x => x.Value.ToString() == segmentVal);
        }

        /// <summary>
        /// Returns true if any assigned segment key + value matches the specified parameters
        /// </summary>
        /// <param name="key"></param>
        /// <param name="val"></param>
        /// <returns></returns>
        public bool RequestEquals(string key, object val)
        {
            return AssignedSegments.Any(x => x.Key == key && x.Value == val);
        }

        /// <summary>
        /// Internal so it can be tested
        /// </summary>
        /// <param name="segmentProviders"></param>
        /// <param name="originalRequestUrl"></param>
        /// <param name="cleanedRequestUrl"></param>
        /// <param name="httpRequest"></param>
        /// <returns></returns>
        internal static IDictionary<string, object> GetAllSegmentsForRequest(
            IEnumerable<ContentSegmentProvider> segmentProviders,
            Uri originalRequestUrl,
            Uri cleanedRequestUrl,
            HttpRequestBase httpRequest)
        {
            //get all key/vals, there might be duplicates so we will simply take the last one in
            var allKeyVals = segmentProviders.SelectMany(x =>
                x.GetSegmentsForRequest(
                    originalRequestUrl,
                    cleanedRequestUrl,
                    httpRequest));

            var d = new Dictionary<string, object>();
            foreach (var keyVal in allKeyVals)
            {
                d[keyVal.Key] = keyVal.Value;
            }
            return d;
        } 
    }
}