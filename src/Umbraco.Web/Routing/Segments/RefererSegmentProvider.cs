using System;
using System.ComponentModel;
using System.Web;

namespace Umbraco.Web.Routing.Segments
{
    /// <summary>
    /// A configurable provider to match against the referer
    /// </summary>
    [DisplayName("Referer provider")]
    [Description("A configurable provider that analyzes the current request's referer")]
    public class RefererSegmentProvider : ConfigurableSegmentProvider
    {
        public override string GetCurrentValue(Uri cleanedRequestUrl, HttpRequestBase httpRequest)
        {
            return httpRequest.UrlReferrer == null ? "" : httpRequest.UrlReferrer.OriginalString;
        }
    }
}