using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using NUnit.Framework;
using Umbraco.Core.ContentVariations;

namespace Umbraco.Tests.ContentVariations
{
    [TestFixture]
    public class ContentVariationProviderTests
    {

        [Test]
        public void Get_Advertised_Segments()
        {
            var provider = new MyTestProvider();

            Assert.AreEqual(3, provider.SegmentsAdvertised.Count());
        }

        [ContentVariation("Test1")]
        [ContentVariation("Test2")]
        [ContentVariation("Test3")]
        [ContentVariation("Test2")]
        private class MyTestProvider : ContentVariationProvider
        {
            public override IEnumerable<string> GetSegmentsForRequest(Uri originalRequestUrl, Uri cleanedRequestUrl, HttpRequestBase httpRequest)
            {
                return Enumerable.Empty<string>();
            }
        }

    }
}
