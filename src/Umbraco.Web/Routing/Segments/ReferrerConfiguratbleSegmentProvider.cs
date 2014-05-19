using System;
using System.Web;

namespace Umbraco.Web.Routing.Segments
{
    /// <summary>
    /// A configurable provider to match against the referer
    /// </summary>
    public class ReferrerConfiguratbleSegmentProvider : ConfigurableSegmentProvider
    {
        public override string GetCurrentValue(Uri cleanedRequestUrl, HttpRequestBase httpRequest)
        {
            return httpRequest.UrlReferrer == null ? "" : httpRequest.UrlReferrer.OriginalString;
        }
    }
}