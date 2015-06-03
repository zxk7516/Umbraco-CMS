using System;
using NUnit.Framework;
using Umbraco.Web.PublishedCache.NuCache;

namespace Umbraco.Tests.Cache
{
    [TestFixture]
    public class SnapDictionaryTests
    {
        [Test]
        public void LiveGenUpdate()
        {
            var d = new SnapDictionary<int, string>();
            d.Test.CollectAuto = false;

            Assert.AreEqual(0, d.Test.GetValues(1).Length);

            // gen 1
            d.Set(1, "one");
            Assert.AreEqual(1, d.Test.GetValues(1).Length);
            d.Clear(1);
            Assert.AreEqual(0, d.Test.GetValues(1).Length); // gone

            Assert.AreEqual(1, d.Test.LiveGen);
            Assert.IsTrue(d.Test.NextGen);
            Assert.AreEqual(0, d.Test.FloorGen);
        }

        [Test]
        public void OtherGenUpdate()
        {
            var d = new SnapDictionary<int, string>();
            d.Test.CollectAuto = false;

            Assert.AreEqual(0, d.Test.GetValues(1).Length);
            Assert.AreEqual(0, d.Test.LiveGen);
            Assert.IsFalse(d.Test.NextGen);

            // gen 1
            d.Set(1, "one");
            Assert.AreEqual(1, d.Test.GetValues(1).Length);
            Assert.AreEqual(1, d.Test.LiveGen);
            Assert.IsTrue(d.Test.NextGen);

            var s = d.CreateSnapshot();
            Assert.AreEqual(1, s.Gen);
            Assert.AreEqual(1, d.Test.LiveGen);
            Assert.IsFalse(d.Test.NextGen);

            // gen 2
            d.Clear(1);
            Assert.AreEqual(2, d.Test.GetValues(1).Length); // there
            Assert.AreEqual(2, d.Test.LiveGen);
            Assert.IsTrue(d.Test.NextGen);

            Assert.AreEqual(0, d.Test.FloorGen);

            GC.KeepAlive(s);
        }

        [Test]
        public void MissingReturnsNull()
        {
            var d = new SnapDictionary<int, string>();
            var s = d.CreateSnapshot();

            Assert.IsNull(s.Get(1));
        }

        [Test]
        public void DeletedReturnsNull()
        {
            var d = new SnapDictionary<int, string>();

            // gen 1
            d.Set(1, "one");

            var s1 = d.CreateSnapshot();
            Assert.AreEqual("one", s1.Get(1));

            // gen 2
            d.Clear(1);

            var s2 = d.CreateSnapshot();
            Assert.IsNull(s2.Get(1));

            Assert.AreEqual("one", s1.Get(1));
        }

        [Test]
        public async void CollectValues()
        {
            var d = new SnapDictionary<int, string>();
            d.Test.CollectAuto = false;

            // gen 1
            d.Set(1, "one");
            Assert.AreEqual(1, d.Test.GetValues(1).Length);
            d.Set(1, "one");
            Assert.AreEqual(1, d.Test.GetValues(1).Length);
            d.Set(1, "uno");
            Assert.AreEqual(1, d.Test.GetValues(1).Length);

            Assert.AreEqual(1, d.Test.LiveGen);
            Assert.IsTrue(d.Test.NextGen);

            var s1 = d.CreateSnapshot();

            Assert.AreEqual(1, d.Test.LiveGen);
            Assert.IsFalse(d.Test.NextGen);

            // gen 2
            Assert.AreEqual(1, d.Test.GetValues(1).Length);
            d.Set(1, "one");
            Assert.AreEqual(2, d.Test.GetValues(1).Length);
            d.Set(1, "uno");
            Assert.AreEqual(2, d.Test.GetValues(1).Length);

            Assert.AreEqual(2, d.Test.LiveGen);
            Assert.IsTrue(d.Test.NextGen);

            var s2 = d.CreateSnapshot();

            Assert.AreEqual(2, d.Test.LiveGen);
            Assert.IsFalse(d.Test.NextGen);

            // gen 3
            Assert.AreEqual(2, d.Test.GetValues(1).Length);
            d.Set(1, "one");
            Assert.AreEqual(3, d.Test.GetValues(1).Length);
            d.Set(1, "uno");
            Assert.AreEqual(3, d.Test.GetValues(1).Length);

            Assert.AreEqual(3, d.Test.LiveGen);
            Assert.IsTrue(d.Test.NextGen);

            var tv = d.Test.GetValues(1);
            Assert.AreEqual(3, tv[0].Item1);
            Assert.AreEqual(2, tv[1].Item1);
            Assert.AreEqual(1, tv[2].Item1);

            Assert.AreEqual(0, d.Test.FloorGen);

            // nothing to collect
            await d.CollectAsync();
            GC.KeepAlive(s1);
            GC.KeepAlive(s2);
            Assert.AreEqual(0, d.Test.FloorGen);
            Assert.AreEqual(3, d.Test.LiveGen);
            Assert.IsTrue(d.Test.NextGen);
            Assert.AreEqual(2, d.SnapCount);
            Assert.AreEqual(3, d.Test.GetValues(1).Length);

            // one snapshot to collect
            s1 = null;
            GC.Collect();
            GC.KeepAlive(s2);
            await d.CollectAsync();
            Assert.AreEqual(1, d.Test.FloorGen);
            Assert.AreEqual(3, d.Test.LiveGen);
            Assert.IsTrue(d.Test.NextGen);
            Assert.AreEqual(1, d.SnapCount);
            Assert.AreEqual(2, d.Test.GetValues(1).Length);

            // another snapshot to collect
            s2 = null;
            GC.Collect();
            await d.CollectAsync();
            Assert.AreEqual(2, d.Test.FloorGen);
            Assert.AreEqual(3, d.Test.LiveGen);
            Assert.IsTrue(d.Test.NextGen);
            Assert.AreEqual(0, d.SnapCount);
            Assert.AreEqual(1, d.Test.GetValues(1).Length);
        }

        [Test]
        public async void ProperlyCollects()
        {
            var d = new SnapDictionary<int, string>();
            d.Test.CollectAuto = false;

            for (var i = 0; i < 32; i++)
            {
                d.Set(i, i.ToString());
                d.CreateSnapshot().Dispose();
            }

            await d.CollectAsync();
            Assert.AreEqual(32, d.Test.LiveGen);
            Assert.IsFalse(d.Test.NextGen);
            Assert.AreEqual(0, d.SnapCount);
            Assert.AreEqual(32, d.Count);

            for (var i = 0; i < 32; i++)
                d.Set(i, null);

            d.CreateSnapshot().Dispose();

            // because we haven't collected yet
            Assert.AreEqual(1, d.SnapCount);
            Assert.AreEqual(32, d.Count);

            // once we collect, they are all gone
            // since noone is interested anymore
            await d.CollectAsync();
            Assert.AreEqual(0, d.SnapCount);
            Assert.AreEqual(0, d.Count);
        }

        [Test]
        public async void CollectNulls()
        {
            var d = new SnapDictionary<int, string>();
            d.Test.CollectAuto = false;

            // gen 1
            d.Set(1, "one");
            Assert.AreEqual(1, d.Test.GetValues(1).Length);
            d.Set(1, "one");
            Assert.AreEqual(1, d.Test.GetValues(1).Length);
            d.Set(1, "uno");
            Assert.AreEqual(1, d.Test.GetValues(1).Length);

            Assert.AreEqual(1, d.Test.LiveGen);
            Assert.IsTrue(d.Test.NextGen);

            var s1 = d.CreateSnapshot();

            Assert.AreEqual(1, d.Test.LiveGen);
            Assert.IsFalse(d.Test.NextGen);

            // gen 2
            Assert.AreEqual(1, d.Test.GetValues(1).Length);
            d.Set(1, "one");
            Assert.AreEqual(2, d.Test.GetValues(1).Length);
            d.Set(1, "uno");
            Assert.AreEqual(2, d.Test.GetValues(1).Length);

            Assert.AreEqual(2, d.Test.LiveGen);
            Assert.IsTrue(d.Test.NextGen);

            var s2 = d.CreateSnapshot();

            Assert.AreEqual(2, d.Test.LiveGen);
            Assert.IsFalse(d.Test.NextGen);

            // gen 3
            Assert.AreEqual(2, d.Test.GetValues(1).Length);
            d.Set(1, "one");
            Assert.AreEqual(3, d.Test.GetValues(1).Length);
            d.Set(1, "uno");
            Assert.AreEqual(3, d.Test.GetValues(1).Length);
            d.Clear(1);
            Assert.AreEqual(3, d.Test.GetValues(1).Length);

            Assert.AreEqual(3, d.Test.LiveGen);
            Assert.IsTrue(d.Test.NextGen);

            var tv = d.Test.GetValues(1);
            Assert.AreEqual(3, tv[0].Item1);
            Assert.AreEqual(2, tv[1].Item1);
            Assert.AreEqual(1, tv[2].Item1);

            Assert.AreEqual(0, d.Test.FloorGen);

            // nothing to collect
            await d.CollectAsync();
            GC.KeepAlive(s1);
            GC.KeepAlive(s2);
            Assert.AreEqual(0, d.Test.FloorGen);
            Assert.AreEqual(3, d.Test.LiveGen);
            Assert.IsTrue(d.Test.NextGen);
            Assert.AreEqual(2, d.SnapCount);
            Assert.AreEqual(3, d.Test.GetValues(1).Length);

            // one snapshot to collect
            s1 = null;
            GC.Collect();
            GC.KeepAlive(s2);
            await d.CollectAsync();
            Assert.AreEqual(1, d.Test.FloorGen);
            Assert.AreEqual(3, d.Test.LiveGen);
            Assert.IsTrue(d.Test.NextGen);
            Assert.AreEqual(1, d.SnapCount);
            Assert.AreEqual(2, d.Test.GetValues(1).Length);

            // another snapshot to collect
            s2 = null;
            GC.Collect();
            await d.CollectAsync();
            Assert.AreEqual(2, d.Test.FloorGen);
            Assert.AreEqual(3, d.Test.LiveGen);
            Assert.IsTrue(d.Test.NextGen);
            Assert.AreEqual(0, d.SnapCount);

            // and everything is gone?
            // no, cannot collect the live gen because we'd need to lock
            Assert.AreEqual(1, d.Test.GetValues(1).Length);

            d.CreateSnapshot();
            GC.Collect();
            await d.CollectAsync();

            // poof, gone
            Assert.AreEqual(0, d.Test.GetValues(1).Length);
        }

        [Test]
        public async void EventuallyCollectNulls()
        {
            var d = new SnapDictionary<int, string>();
            d.Test.CollectAuto = false;

            Assert.AreEqual(0, d.Test.GetValues(1).Length);

            // gen 1
            d.Set(1, "one");
            Assert.AreEqual(1, d.Test.GetValues(1).Length);

            Assert.AreEqual(1, d.Test.LiveGen);
            Assert.IsTrue(d.Test.NextGen);

            await d.CollectAsync();
            var tv = d.Test.GetValues(1);
            Assert.AreEqual(1, tv.Length);
            Assert.AreEqual(1, tv[0].Item1);

            var s = d.CreateSnapshot();
            Assert.AreEqual("one", s.Get(1));

            Assert.AreEqual(1, d.Test.LiveGen);
            Assert.IsFalse(d.Test.NextGen);

            // gen 2
            d.Clear(1);
            tv = d.Test.GetValues(1);
            Assert.AreEqual(2, tv.Length);
            Assert.AreEqual(2, tv[0].Item1);

            Assert.AreEqual(2, d.Test.LiveGen);
            Assert.IsTrue(d.Test.NextGen);

            // nothing to collect
            await d.CollectAsync();
            GC.KeepAlive(s);
            Assert.AreEqual(2, d.Test.GetValues(1).Length);
            Assert.AreEqual(1, d.SnapCount);

            Assert.AreEqual(2, d.Test.LiveGen);
            Assert.IsTrue(d.Test.NextGen);

            // collect snapshot
            // don't collect liveGen+
            s = null;
            GC.Collect();
            await d.CollectAsync();
            Assert.AreEqual(1, d.Test.GetValues(1).Length);
            Assert.AreEqual(0, d.SnapCount);

            // liveGen/nextGen
            d.CreateSnapshot();

            // collect liveGen
            GC.Collect();
            await d.CollectAsync();
            Assert.AreEqual(0, d.Test.GetValues(1).Length);
            Assert.AreEqual(0, d.SnapCount);
        }

        [Test]
        public async void CollectDisposedSnapshots()
        {
            var d = new SnapDictionary<int, string>();
            d.Test.CollectAuto = false;

            // gen 1
            d.Set(1, "one");
            Assert.AreEqual(1, d.Test.GetValues(1).Length);

            Assert.AreEqual(1, d.Test.LiveGen);
            Assert.IsTrue(d.Test.NextGen);

            var s1 = d.CreateSnapshot();

            Assert.AreEqual(1, d.Test.LiveGen);
            Assert.IsFalse(d.Test.NextGen);

            // gen 2
            d.Set(1, "two");
            Assert.AreEqual(2, d.Test.GetValues(1).Length);

            Assert.AreEqual(2, d.Test.LiveGen);
            Assert.IsTrue(d.Test.NextGen);

            var s2 = d.CreateSnapshot();

            Assert.AreEqual(2, d.Test.LiveGen);
            Assert.IsFalse(d.Test.NextGen);

            // gen 3
            d.Set(1, "three");
            Assert.AreEqual(3, d.Test.GetValues(1).Length);

            Assert.AreEqual(3, d.Test.LiveGen);
            Assert.IsTrue(d.Test.NextGen);

            var s3 = d.CreateSnapshot();

            Assert.AreEqual(3, d.Test.LiveGen);
            Assert.IsFalse(d.Test.NextGen);
        
            Assert.AreEqual(3, d.SnapCount);

            s1.Dispose();
            await d.CollectAsync();
            Assert.AreEqual(2, d.SnapCount);
            Assert.AreEqual(2, d.Test.GetValues(1).Length);

            s2.Dispose();
            await d.CollectAsync();
            Assert.AreEqual(1, d.SnapCount);
            Assert.AreEqual(1, d.Test.GetValues(1).Length);

            s3.Dispose();
            await d.CollectAsync();
            Assert.AreEqual(0, d.SnapCount);
            Assert.AreEqual(1, d.Test.GetValues(1).Length);
        }

        [Test]
        public async void CollectGcSnapshots()
        {
            var d = new SnapDictionary<int, string>();
            d.Test.CollectAuto = false;

            // gen 1
            d.Set(1, "one");
            Assert.AreEqual(1, d.Test.GetValues(1).Length);

            Assert.AreEqual(1, d.Test.LiveGen);
            Assert.IsTrue(d.Test.NextGen);

            var s1 = d.CreateSnapshot();

            Assert.AreEqual(1, d.Test.LiveGen);
            Assert.IsFalse(d.Test.NextGen);

            // gen 2
            d.Set(1, "two");
            Assert.AreEqual(2, d.Test.GetValues(1).Length);

            Assert.AreEqual(2, d.Test.LiveGen);
            Assert.IsTrue(d.Test.NextGen);

            var s2 = d.CreateSnapshot();

            Assert.AreEqual(2, d.Test.LiveGen);
            Assert.IsFalse(d.Test.NextGen);

            // gen 3
            d.Set(1, "three");
            Assert.AreEqual(3, d.Test.GetValues(1).Length);

            Assert.AreEqual(3, d.Test.LiveGen);
            Assert.IsTrue(d.Test.NextGen);

            var s3 = d.CreateSnapshot();

            Assert.AreEqual(3, d.Test.LiveGen);
            Assert.IsFalse(d.Test.NextGen);

            Assert.AreEqual(3, d.SnapCount);

            s1 = s2 = s3 = null;

            await d.CollectAsync();
            Assert.AreEqual(3, d.SnapCount);
            Assert.AreEqual(3, d.Test.GetValues(1).Length);


            GC.Collect();
            await d.CollectAsync();
            Assert.AreEqual(0, d.SnapCount);
            Assert.AreEqual(1, d.Test.GetValues(1).Length);
        }

        [Test]
        public async void CollectsSnapshots()
        {
            var d = new SnapDictionary<int, string>();
            d.Set(1, "one");
            d.CreateSnapshot();
            d.Set(1, "two");
            d.CreateSnapshot();
            d.Set(1, "three");
            d.CreateSnapshot();
            d.Set(1, "four");
            d.CreateSnapshot();
            d.Set(1, "five");
            d.CreateSnapshot();
            d.Set(1, "six");
            d.CreateSnapshot();
            Assert.AreEqual(6, d.SnapCount);
            GC.Collect();
            d.Set(1, "seven");
            d.CreateSnapshot();
            await d.PendingCollect();
            Assert.AreEqual(1, d.SnapCount);
        }

        [Test]
        public async void RandomTest1()
        {
            var d = new SnapDictionary<int, string>();
            d.Test.CollectAuto = false;

            d.Set(1, "one");
            d.Set(2, "two");

            var s1 = d.CreateSnapshot();
            var v1 = s1.Get(1);
            Assert.AreEqual("one", v1);

            d.Set(1, "uno");

            var s2 = d.CreateSnapshot();
            var v2 = s2.Get(1);
            Assert.AreEqual("uno", v2);

            v1 = s1.Get(1);
            Assert.AreEqual("one", v1);

            Assert.AreEqual(2, d.SnapCount);

            s1 = null;
            GC.Collect();
            await d.CollectAsync();

            Assert.AreEqual(1, d.SnapCount);
            v2 = s2.Get(1);
            Assert.AreEqual("uno", v2);

            s2 = null;
            GC.Collect();
            await d.CollectAsync();

            Assert.AreEqual(0, d.SnapCount);
        }

        [Test]
        public async void RandomTest2()
        {
            var d = new SnapDictionary<int, string>();
            d.Test.CollectAuto = false;

            d.Set(1, "one");
            d.Set(2, "two");

            var s1 = d.CreateSnapshot();
            var v1 = s1.Get(1);
            Assert.AreEqual("one", v1);

            d.Clear(1);

            var s2 = d.CreateSnapshot();
            var v2 = s2.Get(1);
            Assert.AreEqual(null, v2);

            v1 = s1.Get(1);
            Assert.AreEqual("one", v1);

            Assert.AreEqual(2, d.SnapCount);

            s1 = null;
            GC.Collect();
            await d.CollectAsync();

            Assert.AreEqual(1, d.SnapCount);
            v2 = s2.Get(1);
            Assert.AreEqual(null, v2);

            s2 = null;
            GC.Collect();
            await d.CollectAsync();

            Assert.AreEqual(0, d.SnapCount);
        }

        [Test]
        public void WriteLockingFirstSnapshot()
        {
            var d = new SnapDictionary<int, string>();
            d.Test.CollectAuto = false;

            // gen 1
            d.Set(1, "one");
            Assert.AreEqual(1, d.Test.GetValues(1).Length);

            Assert.AreEqual(1, d.Test.LiveGen);
            Assert.IsTrue(d.Test.NextGen);

            d.WriteLocked(() =>
            {
                var s1 = d.CreateSnapshot();

                Assert.AreEqual(0, s1.Gen);
                Assert.AreEqual(1, d.Test.LiveGen);
                Assert.IsTrue(d.Test.NextGen);
                Assert.IsNull(s1.Get(1));
            });

            var s2 = d.CreateSnapshot();

            Assert.AreEqual(1, s2.Gen);
            Assert.AreEqual(1, d.Test.LiveGen);
            Assert.IsFalse(d.Test.NextGen);
            Assert.AreEqual("one", s2.Get(1));
        }

        [Test]
        public void WriteLocking()
        {
            var d = new SnapDictionary<int, string>();
            d.Test.CollectAuto = false;

            // gen 1
            d.Set(1, "one");
            Assert.AreEqual(1, d.Test.GetValues(1).Length);

            Assert.AreEqual(1, d.Test.LiveGen);
            Assert.IsTrue(d.Test.NextGen);

            var s1 = d.CreateSnapshot();

            Assert.AreEqual(1, s1.Gen);
            Assert.AreEqual(1, d.Test.LiveGen);
            Assert.IsFalse(d.Test.NextGen);
            Assert.AreEqual("one", s1.Get(1));

            // gen 2
            Assert.AreEqual(1, d.Test.GetValues(1).Length);
            d.Set(1, "uno");
            Assert.AreEqual(2, d.Test.GetValues(1).Length);

            Assert.AreEqual(2, d.Test.LiveGen);
            Assert.IsTrue(d.Test.NextGen);

            var s2 = d.CreateSnapshot();

            Assert.AreEqual(2, s2.Gen);
            Assert.AreEqual(2, d.Test.LiveGen);
            Assert.IsFalse(d.Test.NextGen);
            Assert.AreEqual("uno", s2.Get(1));

            d.WriteLocked(() =>
            {
                // gen 3
                Assert.AreEqual(2, d.Test.GetValues(1).Length);
                d.Set(1, "ein");
                Assert.AreEqual(3, d.Test.GetValues(1).Length);

                Assert.AreEqual(3, d.Test.LiveGen);
                Assert.IsTrue(d.Test.NextGen);

                var s3 = d.CreateSnapshot();

                Assert.AreEqual(2, s3.Gen);
                Assert.AreEqual(3, d.Test.LiveGen);
                Assert.IsTrue(d.Test.NextGen); // has NOT changed when (non) creating snapshot
                Assert.AreEqual("uno", s3.Get(1));
            });

            var s4 = d.CreateSnapshot();

            Assert.AreEqual(3, s4.Gen);
            Assert.AreEqual(3, d.Test.LiveGen);
            Assert.IsFalse(d.Test.NextGen);
            Assert.AreEqual("ein", s4.Get(1));
        }
    }
}
