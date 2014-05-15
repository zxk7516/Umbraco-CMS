using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using Newtonsoft.Json;
using Umbraco.Core;
using Umbraco.Web.Routing.Segments;

namespace Umbraco.Web.Routing
{
    /// <summary>
    /// Used to retreive (and persist) the segments that have been found based on the current request information
    /// </summary>
    /// <remarks>
    /// When retreiving the segments, this is done lazily and during that execution it will also persist the 
    /// matched advertised values to a cookie.
    /// 
    /// TODO: HOW DO WE GET THAT IN THE MEMBER PROFILE if they are logged in? I think that will need to be part of the 
    /// membership validation/login logic, we can't do that here as the logic to auth might not have even occured. 
    /// 
    /// 
    /// </remarks>
    public class RequestSegments
    {
        private readonly IEnumerable<ContentSegmentProvider> _segmentProviders;
        private readonly HttpRequestBase _httpRequest;
        private readonly Lazy<IDictionary<string, Tuple<object, bool>>> _assignedSegments;

        public RequestSegments(IEnumerable<ContentSegmentProvider> segmentProviders, 
            Uri originalRequestUrl,
            Uri cleanedRequestUrl,
            HttpRequestBase httpRequest)
        {
            _segmentProviders = segmentProviders;
            _httpRequest = httpRequest;

            _assignedSegments = new Lazy<IDictionary<string, Tuple<object, bool>>>(() =>
                GetAllSegmentsForRequest(
                    _segmentProviders,
                    originalRequestUrl,
                    cleanedRequestUrl,
                    httpRequest));
        }

        /// <summary>
        /// This method must be called in order to persist the segments to cookie
        /// </summary>
        internal void EnsurePersisted(HttpResponseBase response)
        {
            var persisted = PersistedSegments.ToArray();
            if (persisted.Any())
            {
                var json = JsonConvert.SerializeObject(PersistedSegments);
                var cookie = new HttpCookie(Constants.Web.SegmentCookieName, json)
                {
                    //sliding 30 day expiry?
                    Expires = DateTime.Now.AddDays(30)
                };
                response.SetCookie(cookie);
            }
            else
            {
                response.SetCookie(new HttpCookie(Constants.Web.SegmentCookieName)
                {
                    //remove it!
                    Expires = DateTime.Now.AddDays(-30)
                });
            }
        }

        /// <summary>
        /// Returns the assigned segments for the current request
        /// </summary>
        public IDictionary<string, object> AssignedSegments
        {
            get { return _assignedSegments.Value.ToDictionary(x => x.Key, x => x.Value.Item1); }
        }

        /// <summary>
        /// Returns the segments that are persisted (cookie and should be part of the member profile)
        /// </summary>
        internal IDictionary<string, object> PersistedSegments
        {
            get { return _assignedSegments.Value.Where(x => x.Value.Item2).ToDictionary(x => x.Key, x => x.Value.Item1); }
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
        /// <returns>
        /// The return value is a dictionary of the 'key' (segment name) and a tuple of the 'value' of the segment and a boolean of 
        /// whether it should be persisted or not (based on a provider's advertised segments)
        /// </returns>
        internal static IDictionary<string, Tuple<object, bool>> GetAllSegmentsForRequest(
            IEnumerable<ContentSegmentProvider> segmentProviders,
            Uri originalRequestUrl,
            Uri cleanedRequestUrl,
            HttpRequestBase httpRequest)
        {
            //get all key/vals, there might be duplicates so we will simply take the last one in

            var d = new Dictionary<string, Tuple<object, bool>>();

            foreach (var provider in segmentProviders)
            {
                var segments = provider.GetSegmentsForRequest(originalRequestUrl, cleanedRequestUrl, httpRequest).ToArray();
                var advertised = provider.SegmentsAdvertised.ToArray();
                foreach (var s in segments)
                {
                    d[s.Key] = new Tuple<object, bool>(s.Value, advertised.Contains(s.Key));
                }
            }

            return d;
        } 
    }
}