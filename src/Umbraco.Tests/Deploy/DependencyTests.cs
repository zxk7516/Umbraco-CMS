using System;
using NUnit.Framework;
using Umbraco.Core.Deploy;

namespace Umbraco.Tests.Deploy
{
    [TestFixture]
    public class DependencyTests
    {
        [Test]
        public void Equals()
        {
            var guid = Guid.NewGuid();
            var d1 = new Dependency("test", guid);
            var d2 = new Dependency("test", guid);
            var d3 = new Dependency("myname", "test", guid);
            var d4 = new Dependency(new DeployKey("test", guid), true);
            Assert.AreEqual(d1, d2);
            Assert.AreEqual(d2, d3);
            Assert.AreEqual(d3, d4);
        }

        [Test]
        public void Not_Equals()
        {
            var d1 = new Dependency("test", Guid.NewGuid());
            var d2 = new Dependency("test", Guid.NewGuid());
            Assert.AreNotEqual(d1, d2);
        }

    }
}