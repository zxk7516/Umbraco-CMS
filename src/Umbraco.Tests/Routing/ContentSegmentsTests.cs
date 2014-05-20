using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using Moq;
using NUnit.Framework;
using Umbraco.Web.Models.Segments;
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
                new MyTestProvider1(), 
                new MyTestProvider2()
            }, new Uri("http://localhost/test?blah=1"), new Uri("http://localhost/test/"), 
            Mock.Of<HttpRequestBase>(req => req.Cookies == new HttpCookieCollection()),
            new Dictionary<string, bool>
            {
                {typeof(MyTestProvider1).FullName, true},
                {typeof(MyTestProvider2).FullName, true}
            });

            Assert.AreEqual(4, result.Count());
            Assert.AreEqual(true, result["key1"].Value);
            Assert.AreEqual("blah", result["key2"].Value);
            Assert.AreEqual(9876, result["key3"].Value);
            Assert.AreEqual(123, result["key4"].Value);
        }

        [Test]
        public void Get_All_Segments_For_Request_With_Approval()
        {
            var result = RequestSegments.GetAllSegmentsForRequest(new ContentSegmentProvider[]
            {
                new MyTestProvider1(), 
                new MyTestProvider2()
            }, new Uri("http://localhost/test?blah=1"), new Uri("http://localhost/test/"),
            Mock.Of<HttpRequestBase>(req => req.Cookies == new HttpCookieCollection()),
            new Dictionary<string, bool>
            {
                {typeof(MyTestProvider1).FullName, true},
                {typeof(MyTestProvider2).FullName, false}
            });

            Assert.AreEqual(3, result.Count());
        }

        private class MyTestProvider1 : ContentSegmentProvider
        {
            public override SegmentCollection GetSegmentsForRequest(Uri originalRequestUrl, Uri cleanedRequestUrl, HttpRequestBase httpRequest)
            {
                return new SegmentCollection(new List<Segment>
                {
                    new Segment("key1", true),
                    new Segment("key2", "blahdd"),
                    new Segment("key3", 9876)
                });
            }
        }

        private class MyTestProvider2 : ContentSegmentProvider
        {
            public override SegmentCollection GetSegmentsForRequest(Uri originalRequestUrl, Uri cleanedRequestUrl, HttpRequestBase httpRequest)
            {
                return new SegmentCollection(new List<Segment>
                {
                    new Segment("key1", true),
                    new Segment("key2", "blah"),
                    new Segment("key4", 123)
                });
            }
        }
    }
}