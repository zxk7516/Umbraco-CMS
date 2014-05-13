using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using NUnit.Framework;
using Umbraco.Web.Routing.Segments;

namespace Umbraco.Tests.Routing
{
    [TestFixture]
    public class ContentSegmentProviderTests
    {

        [Test]
        public void Get_Advertised_Segments()
        {
            var provider = new MyTestProvider();

            Assert.AreEqual(3, provider.SegmentsAdvertised.Count());
        }

        [ContentSegment("Test1")]
        [ContentSegment("Test2")]
        [ContentSegment("Test3")]
        [ContentSegment("Test2")]
        private class MyTestProvider : ContentSegmentProvider
        {
            public override IDictionary<string, object> GetSegmentsForRequest(Uri originalRequestUrl, Uri cleanedRequestUrl, HttpRequestBase httpRequest)
            {
                return new Dictionary<string, object>();
            }
        }

    }
}
