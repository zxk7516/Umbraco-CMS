using System;
using System.Collections.Generic;
using System.ComponentModel;
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

        public static string GetDisplayName(ContentSegmentProvider provider)
        {
            var att = provider.GetType().GetCustomAttribute<DisplayNameAttribute>(false);
            return att == null ? GetTypeName(provider) : att.DisplayName;
        }

        public static string GetDescription(ContentSegmentProvider provider)
        {
            var att = provider.GetType().GetCustomAttribute<DescriptionAttribute>(false);
            return att == null ? string.Empty : att.Description;
        }

        public static string GetTypeName(ContentSegmentProvider provider)
        {
            var type = provider.GetType();
            return type.Namespace.EnsureEndsWith('.') + type.Name;
        }

        private readonly ReaderWriterLockSlim _lock = new ReaderWriterLockSlim();
        private IEnumerable<string> _advertised;

        /// <summary>
        /// Get the advertised segments for this, these can be used for content variants and are also the segments that
        /// will be persisted to storage (cookie and membership provider) if they match the segment names returned in the 
        /// GetSegmentsForRequest method
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
        public abstract SegmentCollection GetSegmentsForRequest(
            Uri originalRequestUrl,
            Uri cleanedRequestUrl,
            HttpRequestBase httpRequest);

    }
}
