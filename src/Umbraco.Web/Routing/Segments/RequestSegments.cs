using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Web;
using System.Web.ApplicationServices;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Umbraco.Core;

namespace Umbraco.Web.Routing.Segments
{
    /// <summary>
    /// Used to retreive (and persist) the segments that have been found based on the current request information
    /// </summary>
    /// <remarks>
    /// When retreiving the segments, this is done lazily and during that execution it will also persist the 
    /// matched advertised values to a cookie.
    /// 
    /// This class is NOT thread safe
    /// 
    /// </remarks>
    public class RequestSegments
    {
        private readonly IEnumerable<ContentSegmentProvider> _segmentProviders;

        private readonly Lazy<SegmentCollection> _assignedSegments;

        public RequestSegments(IEnumerable<ContentSegmentProvider> segmentProviders, 
            Uri originalRequestUrl,
            Uri cleanedRequestUrl,
            HttpRequestBase httpRequest)
        {
            _segmentProviders = segmentProviders;

            _assignedSegments = new Lazy<SegmentCollection>(() =>
                GetAllSegmentsForRequest(
                    _segmentProviders,
                    originalRequestUrl,
                    cleanedRequestUrl,
                    httpRequest), 
                    //This class should ONLY be used by one thread at a time (i.e. current request)
                    LazyThreadSafetyMode.None);
        }

        /// <summary>
        /// This will add a segment to the current instance - this is only used to manually add a segment during a request when
        /// using the MemberSegment and is for performance improvements so we don't have to go to the db on each usage of MemberSegment.
        /// </summary>
        /// <param name="segment"></param>        
        internal void Add(Segment segment)
        {
            _assignedSegments.Value.AddNew(segment);
        }

        /// <summary>
        /// This method must be called in order to persist the segments to cookie
        /// </summary>
        /// <remarks>
        /// This will not overwrite cookie data that isn't contained in the current request segments
        /// </remarks>
        internal void EnsurePersisted(HttpResponseBase response, HttpRequestBase request)
        {
            //NOTE: The cookie data will alraedy be part of the PersistedSegments (see GetAllSegmentsForRequest)

            var toPersist = PersistedSegments.ToList();
            //var cookieData = request.Cookies[Constants.Web.SegmentCookieName] == null
            //    ? new List<Segment>()
            //    //TODO: try/catch
            //    : JsonConvert.DeserializeObject<IEnumerable<Segment>>(request.Cookies[Constants.Web.SegmentCookieName].Value);
            if (toPersist.Any())
            {
                ////find anything in the cookie data that is not in our segments and add it to the segments to be persisted
                //foreach (var item in cookieData.Where(item => toPersist.Any(x => x.Name == item.Name) == false))
                //{
                //    toPersist.Add(item);
                //}

                var json = JsonConvert.SerializeObject(toPersist);
                
                var cookie = new HttpCookie(Constants.Web.SegmentCookieName, json)
                {
                    //sliding 30 day expiry?
                    Expires = DateTime.Now.AddDays(30)
                };
                response.SetCookie(cookie);
            }
            //else if (cookieData.Any())
            //{
            //    var json = JsonConvert.SerializeObject(toPersist);
            //    var cookie = new HttpCookie(Constants.Web.SegmentCookieName, json)
            //    {
            //        //sliding 30 day expiry? 
            //        Expires = DateTime.Now.AddDays(30)
            //    };
            //    response.SetCookie(cookie);
            //}
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
        public IEnumerable<Segment> AssignedSegments
        {
            get { return _assignedSegments.Value; }
        }

        /// <summary>
        /// Returns the segments that are persisted (cookie and should be part of the member profile)
        /// </summary>
        internal IEnumerable<Segment> PersistedSegments
        {
            get { return _assignedSegments.Value.Where(x => x.Persist); }
        }

        //TODO: Add an GetAll method!

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
            return AssignedSegments.Any(x => x.Name == segmentKey && x.Value is bool && (bool)x.Value);
        }

        /// <summary>
        /// Returns true if any assigned segment has a value equal to the one specified
        /// </summary>
        /// <param name="segmentVal"></param>
        /// <returns></returns>
        public bool RequestContainsValue(string segmentVal)
        {
            return AssignedSegments.Any(x => x.Value.ToString() == segmentVal);
        }

        /// <summary>
        /// Returns true if the request contains the specified key
        /// </summary>
        /// <param name="segmentKey"></param>
        /// <returns></returns>
        public bool RequestContainsKey(string segmentKey)
        {
            return AssignedSegments.Any(x => x.Name == segmentKey);
        }

        /// <summary>
        /// Returns true if any assigned segment key + value matches the specified parameters
        /// </summary>
        /// <param name="key"></param>
        /// <param name="val"></param>
        /// <returns></returns>
        public bool RequestEquals(string key, object val)
        {
            return AssignedSegments.Any(x => x.Name == key && x.Value == val);
        }

        /// <summary>
        /// Internal so it can be tested - collects all segments from providers and the ones in the cookie
        /// </summary>
        /// <param name="segmentProviders"></param>
        /// <param name="originalRequestUrl"></param>
        /// <param name="cleanedRequestUrl"></param>
        /// <param name="httpRequest"></param>
        /// <returns>
        /// </returns>
        internal static SegmentCollection GetAllSegmentsForRequest(
            IEnumerable<ContentSegmentProvider> segmentProviders,
            Uri originalRequestUrl,
            Uri cleanedRequestUrl,
            HttpRequestBase httpRequest)
        {
            //get all key/vals, there might be duplicates so we will simply take the last one in

            var d = new List<Segment>();

            foreach (var provider in segmentProviders)
            {
                var segments = provider.GetSegmentsForRequest(originalRequestUrl, cleanedRequestUrl, httpRequest).ToArray();
                var advertised = provider.SegmentsAdvertised.ToArray();
                d.AddRange(segments.Select(s => new Segment(s.Name, s.Value, advertised.Contains(s.Name))));
            }

            var cookieData = httpRequest.Cookies[Constants.Web.SegmentCookieName] == null
                ? new List<Segment>()
                //TODO: try/catch
                : JsonConvert.DeserializeObject<IEnumerable<Segment>>(httpRequest.Cookies[Constants.Web.SegmentCookieName].Value);

            //Add anything in the cookie that is not already in the list
            d.AddRange(cookieData.Where(x => d.Contains(x) == false));

            return new SegmentCollection(d);
        } 
    }
}