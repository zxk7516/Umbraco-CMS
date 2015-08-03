using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using Umbraco.Core.Deploy;

namespace Umbraco.Tests.Deploy
{
    [TestFixture]
    public class DeployKeyTests
    {

        [TestCase("ThisIsAnInvalidStringId")]
        [TestCase("ThisIsOk_ThisIsNotAGuid")]
        public void ItemIdentifier_Throws_With_Invalid_Id(string id)
        {
            Assert.Throws<InvalidOperationException>(() => DeployKey.Parse(id));
        }

        [TestCase("Hello_56D26A20-56F5-4BEA-8C93-BFAF98240CF7")]
        [TestCase("1234_ECB47ADA-3B61-465F-94F1-CFB8028CAD0F")]
        public void ItemIdentifier_Parses_Valid_Id(string id)
        {
            var parts = id.Split('_');
            var identifier = DeployKey.Parse(id);
            Assert.AreEqual(parts[0], identifier.Id);
            Assert.AreEqual(Guid.Parse(parts[1]), identifier.ProviderId);
        }

    }
}
