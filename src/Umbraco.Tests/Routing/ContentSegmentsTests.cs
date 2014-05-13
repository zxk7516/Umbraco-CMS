using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using Moq;
using NUnit.Framework;
using Umbraco.Web.Routing;
using Umbraco.Web.Routing.Segments;

namespace Umbraco.Tests.Routing
{
    [TestFixture]
    public class ContentSegmentsTests
    {

        [Test]
        public void Get_All_Segments_For_Request()
        {
            var result = RequestSegments.GetAllSegmentsForRequest(new ContentSegmentProvider[]
            {
                new MyTestProvider1(), new MyTestProvider2()
            }, new Uri("http://localhost/test?blah=1"), new Uri("http://localhost/test/"), Mock.Of<HttpRequestBase>());

            Assert.AreEqual(4, result.Count());
            Assert.AreEqual(true, result["key1"]);
            Assert.AreEqual("blah", result["key2"]);
            Assert.AreEqual(9876, result["key3"]);
            Assert.AreEqual(123, result["key4"]);
        }

        private class MyTestProvider1 : ContentSegmentProvider
        {
            public override IDictionary<string, object> GetSegmentsForRequest(Uri originalRequestUrl, Uri cleanedRequestUrl, HttpRequestBase httpRequest)
            {
                return new Dictionary<string, object> { { "key1", false }, { "key2", "blahdd" }, { "key3", 9876 } };
            }
        }

        private class MyTestProvider2 : ContentSegmentProvider
        {
            public override IDictionary<string, object> GetSegmentsForRequest(Uri originalRequestUrl, Uri cleanedRequestUrl, HttpRequestBase httpRequest)
            {
                return new Dictionary<string, object> {{"key1", true}, {"key2", "blah"}, {"key4", 123}};
            }
        }
    }
}