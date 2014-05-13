using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Web;
using Umbraco.Core;

namespace Umbraco.Web.Routing.Segments
{

    /// <summary>
    /// Returns the segment names to assign to the current request
    /// </summary>
    /// <remarks>
    /// The provider also exposes via attributes which static segments can be applied to content variations
    /// </remarks>
    public abstract class ContentSegmentProvider
    {
        private readonly ReaderWriterLockSlim _lock = new ReaderWriterLockSlim();
        private IEnumerable<string> _advertised;

        /// <summary>
        /// Get the advertised segments for this
        /// </summary>
        public IEnumerable<string> SegmentsAdvertised
        {
            get
            {
                using (var lck = new UpgradeableReadLock(_lock))
                {
                    if (_advertised == null)
                    {
                        lck.UpgradeToWriteLock();
                        _advertised = this.GetType().GetCustomAttributes<ContentSegmentAttribute>(false)
                            .Select(x => x.SegmentName)
                            .Distinct()
                            .ToArray();
                    }   
                }
                return _advertised;
            }
        } 

        /// <summary>
        /// Returns the segment names and values to assign to the current request
        /// </summary>
        /// <param name="originalRequestUrl"></param>
        /// <param name="cleanedRequestUrl"></param>
        /// <param name="httpRequest"></param>
        /// <returns></returns>
        public abstract IDictionary<string, object> GetSegmentsForRequest(
            Uri originalRequestUrl,
            Uri cleanedRequestUrl,
            HttpRequestBase httpRequest);

    }
}
