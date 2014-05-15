using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Web;

namespace Umbraco.Web.Routing.Segments
{
    /// <summary>
    /// Assigns segments based on MS's HttpBrowserCapabilities object
    /// </summary>
    internal class BrowserCapabilitiesProvider : ContentSegmentProvider
    {
        public BrowserCapabilitiesProvider()
        {
            _browserCapabilityProps = typeof(HttpBrowserCapabilitiesBase).GetProperties()
                .Where(x => PropNames.Contains(x.Name))
                .ToArray();
        }

        private readonly PropertyInfo[] _browserCapabilityProps;

        private static readonly string[] PropNames =
        {
            "IsMobileDevice",
            "JavaApplets",
            "MajorVersion",
            "MinorVersion",
            "MobileDeviceModel",
            "MobileDeviceManufacturer",
            "Platform"
        };

        public override IDictionary<string, object> GetSegmentsForRequest(Uri originalRequestUrl, Uri cleanedRequestUrl, HttpRequestBase httpRequest)
        {
            return _browserCapabilityProps
                .Select(x => new {key = x.Name, val = x.GetValue(httpRequest.Browser, null)})
                .ToDictionary(key => key.key, val => val.val);
        }
    }
}