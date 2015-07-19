using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using NUnit.Framework;
using Umbraco.Core.Serialization;

namespace Umbraco.Tests
{
    [TestFixture]
    public class JsonSerializationTests
    {
        [Test]
        public void Int64Test()
        {
            const string json = "{\"value1\":1, \"value2\":2, \"value3\":\"hello\"}";
            var data = JsonConvert.DeserializeObject<Dictionary<string, object>>(json);
            Assert.IsInstanceOf<long>(data["value1"]);
            Assert.IsInstanceOf<long>(data["value2"]);
            Assert.IsInstanceOf<string>(data["value3"]);
        }

        [Test]
        public void Int32Test()
        {
            const string json = "{\"value1\":1, \"value2\":2, \"value3\":\"hello\"}";
            var settings = new JsonSerializerSettings
            {
                Converters = new List<JsonConverter> { new ForceInt32Converter() }
            };
            var data = JsonConvert.DeserializeObject<Dictionary<string, object>>(json, settings);
            Assert.IsInstanceOf<int>(data["value1"]);
            Assert.IsInstanceOf<int>(data["value2"]);
            Assert.IsInstanceOf<string>(data["value3"]);
        }
    }
}
