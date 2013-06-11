using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using NUnit.Framework;
using Umbraco.Core;
using Umbraco.Core.Models;
using Umbraco.Core.Models.PublishedContent;
using Umbraco.Core.PropertyEditors;
using Umbraco.Tests.PublishedContent;
using Umbraco.Tests.TestHelpers;
using Umbraco.Web.PublishedCache;
using Umbraco.Web.PublishedCache.NuCache;

namespace Umbraco.Tests.Cache.PublishedCache
{
    [TestFixture]
    public class NuCacheTests : BaseUmbracoApplicationTest
    {
        //[SetUp]
        //public void Setup()
        //{
        //    ContentBucket.EnableDropsTracking();
        //    PropertyValueConvertersResolver.Current = new PropertyValueConvertersResolver(new ActivatorServiceProvider(), Logger);
        //    Resolution.Freeze();
        //}

        public override void Initialize()
        {
            ContentBucket.EnableDropsTracking();
            PropertyValueConvertersResolver.Current = new PropertyValueConvertersResolver(new ActivatorServiceProvider(), Logger);
            base.Initialize();
        }

        [TearDown]
        public override void TearDown()
        {
            PropertyValueConvertersResolver.Reset();
        }

        [Test]
        public void NewContentNotVisible()
        {
            var options = new ContentStore.Options
            {
                TrackViews = true
            };
            var store = new ContentStore(options);

            var props = new[]
                    {
                        new PublishedPropertyType("prop1", 1, "?"), 
                    };

            var contentType1 = new PublishedContentType(1, "ContentType1", props);

            var content1 = CreateContentNode(contentType1, 1, null, 1234);
            store.Set(content1);
            var view1 = store.GetView();
            Assert.AreSame(content1, view1.Get(content1.Id));

            var content2 = CreateContentNode(contentType1, 2, null, 3456);
            store.Set(content2);
            var view2 = store.GetView();
            Assert.AreSame(content1, view1.Get(content1.Id));
            Assert.AreSame(content1, view2.Get(content1.Id));
            Assert.AreSame(content2, view2.Get(content2.Id));
            Assert.IsNull(view1.Get(content2.Id));
        }

        [Test]
        public void EditedContentNotVisible()
        {
            var options = new ContentStore.Options
            {
                TrackViews = true
            };
            var store = new ContentStore(options);

            var props = new[]
                    {
                        new PublishedPropertyType("prop1", 1, "?"), 
                    };

            var contentType1 = new PublishedContentType(1, "ContentType1", props);

            var node1 = CreateContentNode(contentType1, 1, null, 1234);
            store.Set(node1);
            var view1 = store.GetView();
            Assert.AreSame(node1, view1.Get(node1.Id));

            var node2 = CreateContentNode(contentType1, 1, null, 5678);
            store.Set(node2);
            var view2 = store.GetView();
            Assert.AreNotSame(view1, view2);
            Assert.AreSame(node1, view1.Get(node1.Id));
            Assert.AreNotSame(node1, view2.Get(node1.Id));
            Assert.AreSame(node2, view2.Get(node1.Id));
            Assert.AreEqual(1234, view1.Get(node1.Id).Published.GetProperty("prop1").Value);
            Assert.AreEqual(5678, view2.Get(node1.Id).Published.GetProperty("prop1").Value);
        }

        [Test]
        public void RootContent()
        {
            var options = new ContentStore.Options
            {
                TrackViews = true
            };
            var store = new ContentStore(options);

            var props = new[]
                    {
                        new PublishedPropertyType("prop1", 1, "?"), 
                    };

            var contentType1 = new PublishedContentType(1, "ContentType1", props);
            var contentType2 = new PublishedContentType(2, "ContentType2", props);

            var node1 = CreateContentNode(contentType1, 1, null, 1234);
            store.Set(node1);

            var view1 = store.GetView();
            Assert.AreEqual(1, view1.GetAtRoot().Count());

            var node2 = CreateContentNode(contentType2, 2, null, 3456);
            store.Set(node2);

            var view2 = store.GetView();
            Assert.AreEqual(1, view1.GetAtRoot().Count());
            Assert.AreEqual(2, view2.GetAtRoot().Count());

            store.Clear(node1.Id);

            var view3 = store.GetView();
            Assert.AreEqual(1, view1.GetAtRoot().Count());
            Assert.AreEqual(2, view2.GetAtRoot().Count());
            Assert.AreEqual(1, view3.GetAtRoot().Count());
        }

        private void SetGetContentByIdOverride(ContentView view)
        {
            Web.PublishedCache.NuCache.PublishedContent.GetContentByIdOverride = ((preview, id) =>
            {
                var n = view.Get(id);
                if (preview == false) return n.Published;
                throw new NotSupportedException();
                //if (n.Draft != null) return n.Draft;
                //return n.Published.AsPublishedWhaterver
            });
        }

        [Test]
        public void Children()
        {
            var options = new ContentStore.Options
            {
                TrackViews = true
            };
            var store = new ContentStore(options);

            var props = new[]
                    {
                        new PublishedPropertyType("prop1", 1, "?"), 
                    };

            var contentType1 = new PublishedContentType(1, "ContentType1", props);
            var contentType2 = new PublishedContentType(2, "ContentType2", props);

            var node1 = CreateContentNode(contentType1, 1, null, 1234);
            store.Set(node1);

            // content1 goes at root and has no children
            var view1 = store.GetView();
            Assert.AreEqual(1, view1.GetAtRoot().Count());
            SetGetContentByIdOverride(view1);
            Assert.AreEqual(0, view1.Get(node1.Id).Published.Children.Count());

            var node2 = CreateContentNode(contentType2, 2, node1, 3456);
            store.Set(node2);
            store.Set(node2);
            store.Set(node2);
            store.Set(node2); // no duplicate

            // still only 1 content at root
            var view2 = store.GetView();
            Assert.AreEqual(1, view1.GetAtRoot().Count());
            Assert.AreEqual(1, view2.GetAtRoot().Count());
            // content1 from view1 still has no children
            SetGetContentByIdOverride(view1);
            Assert.AreEqual(0, view1.Get(node1.Id).Published.Children.Count());
            // content1 from view2 now has one child
            SetGetContentByIdOverride(view2);
            Assert.AreEqual(1, view2.Get(node1.Id).Published.Children.Count());

            store.Clear(node2.Id);
            var view3 = store.GetView();
            // content1 from view3 now has no child
            SetGetContentByIdOverride(view3);
            Assert.AreEqual(0, view3.Get(node1.Id).Published.Children.Count());
        }

        [Test]
        public void ClearBranch()
        {
            var options = new ContentStore.Options
            {
                TrackViews = true
            };
            var store = new ContentStore(options);

            var props = new[]
            {
                new PublishedPropertyType("prop1", 1, "?"), 
            };

            var contentType1 = new PublishedContentType(1, "ContentType1", props);

            var node1 = CreateContentNode(contentType1, 1, null, 1234);
            store.Set(node1);

            var node2 = CreateContentNode(contentType1, 2, node1, 1234);
            store.Set(node2);

            var node3 = CreateContentNode(contentType1, 3, node2, 1234);
            store.Set(node3);

            // only 1 content at root
            var view1 = store.GetView();
            Assert.AreEqual(1, view1.GetAtRoot().Count());
            // children
            SetGetContentByIdOverride(view1);
            Assert.AreEqual(1, view1.Get(node1.Id).Published.Children.Count());
            Assert.AreEqual(1, view1.Get(node2.Id).Published.Children.Count());
            Assert.AreEqual(0, view1.Get(node3.Id).Published.Children.Count());

            // removing a content removes the whole branch
            store.Clear(node2.Id);
            var view2 = store.GetView();
            SetGetContentByIdOverride(view2);
            Assert.AreEqual(0, view2.Get(node1.Id).Published.Children.Count());
            Assert.IsNull(view2.Get(node2.Id));
            Assert.IsNull(view2.Get(node3.Id));

            // but not on view 1
            SetGetContentByIdOverride(view1);
            Assert.AreEqual(1, view1.Get(node1.Id).Published.Children.Count());
            Assert.AreEqual(1, view1.Get(node2.Id).Published.Children.Count());
            Assert.AreEqual(0, view1.Get(node3.Id).Published.Children.Count());
            Assert.IsNotNull(view1.Get(node2.Id));
            Assert.IsNotNull(view1.Get(node3.Id));
        }

        [Test]
        public void SetBranch()
        {
            var options = new ContentStore.Options
            {
                TrackViews = true
            };
            var store = new ContentStore(options);

            var props = new[]
            {
                new PublishedPropertyType("prop1", 1, "?"), 
            };

            var contentType1 = new PublishedContentType(1, "ContentType1", props);

            var node1 = CreateContentNode(contentType1, 1, null, 1234);
            store.Set(node1);

            var node2 = CreateContentNode(contentType1, 2, node1, 1234);
            store.Set(node2);

            var node3 = CreateContentNode(contentType1, 3, node2, 1234);
            store.Set(node3);

            // only 1 content at root
            var view1 = store.GetView();
            Assert.AreEqual(1, view1.GetAtRoot().Count());
            // children
            SetGetContentByIdOverride(view1);
            Assert.AreEqual(1, view1.Get(node1.Id).Published.Children.Count());
            Assert.AreEqual(1, view1.Get(node2.Id).Published.Children.Count());
            Assert.AreEqual(0, view1.Get(node3.Id).Published.Children.Count());

            // editing a content preserves the branch (NOT moving)
            node2 = CreateContentNode(contentType1, 2, node1, 9999);
            store.Set(node2); 
            var view2 = store.GetView();
            SetGetContentByIdOverride(view2);
            Assert.AreEqual(1, view2.Get(node1.Id).Published.Children.Count());
            Assert.AreEqual(1, view2.Get(node2.Id).Published.Children.Count());
            Assert.AreEqual(0, view2.Get(node3.Id).Published.Children.Count());
            Assert.IsNotNull(view2.Get(node2.Id));
            Assert.IsNotNull(view2.Get(node3.Id));
            Assert.AreEqual(9999, view2.Get(node2.Id).Published.GetProperty("prop1").Value);

            // but not on view 1
            SetGetContentByIdOverride(view1);
            Assert.AreEqual(1, view1.Get(node1.Id).Published.Children.Count());
            Assert.AreEqual(1, view1.Get(node2.Id).Published.Children.Count());
            Assert.AreEqual(0, view1.Get(node3.Id).Published.Children.Count());
            Assert.IsNotNull(view1.Get(node2.Id));
            Assert.IsNotNull(view1.Get(node3.Id));
            Assert.AreEqual(1234, view1.Get(node2.Id).Published.GetProperty("prop1").Value);
        }

        [Test]
        public void ContentStore()
        {
            var options = new ContentStore.Options
            {
                TrackViews = true
            };
            var store = new ContentStore(options);

            var props = new[]
                    {
                        new PublishedPropertyType("prop1", 1, "?"), 
                    };

            var contentType1 = new PublishedContentType(1, "ContentType1", props);
            var contentType2 = new PublishedContentType(2, "ContentType2", props);

            var node1 = CreateContentNode(contentType1, 1, null, 1234);
            var node2 = CreateContentNode(contentType2, 2, null, 3456);
            store.Set(node1);
            store.Set(node2);

            // we haven't requested a view yet
            Assert.AreEqual(0, store.ViewsCount);

            // get a view
            // now we should have one view, which has no local content
            // and if we get a view again, we should get the same view
            var view1 = store.GetView();
            Assert.AreEqual(1, store.ViewsCount);
            Assert.IsFalse(view1.HasLocalContent);
            Assert.AreSame(view1, store.GetView());

            // try to get content
            Assert.AreSame(node1, view1.Get(node1.Id));
            Assert.IsNull(view1.Get(666));

            node1 = CreateContentNode(contentType1, 1, null, 5678);
            store.Set(node1);

            // now the view has local content
            Assert.IsTrue(view1.HasLocalContent);

            // get a view
            // now we should have two views, one with local content and one without
            // and if we get a view again, we should get the same view
            var view2 = store.GetView();
            Assert.AreEqual(2, store.ViewsCount);
            Assert.AreNotSame(view1, view2);
            Assert.IsFalse(view2.HasLocalContent);
            Assert.AreEqual(view2, store.GetView());

            // try to get content
            Assert.AreSame(node1, view2.Get(node1.Id));
            Assert.AreNotSame(node1, view1.Get(node1.Id));
            Assert.IsNull(view2.Get(666));

            // each view has its own copy of modified content
            Assert.AreEqual(1234, view1.Get(node1.Id).Published.GetProperty("prop1").Value);
            Assert.AreEqual(5678, view2.Get(node1.Id).Published.GetProperty("prop1").Value);

            // but same content is shared if not modified
            Assert.AreEqual(3456, view1.Get(node2.Id).Published.GetProperty("prop1").Value);
            Assert.AreSame(view1.Get(node2.Id), view2.Get(node2.Id));

            // dereference view1 and it's gone
            view1 = null;
            GC.Collect();
            Assert.AreEqual(1, store.ViewsCount);

            // dereference view2 and it stays because it's the top view
            view2 = null;
            GC.Collect();
            Assert.AreEqual(1, store.ViewsCount);

            // force-kill all views does the job
            store.KillViews();
            GC.Collect();
            Assert.AreEqual(0, store.ViewsCount);
        }

        // note - for the above test to work in "debug" mode we have to use
        // some tricks... namely external methods AssertWhatever - otherwise
        // hidden local vars are created that reference view1 and prevent
        // GC to collect it.
        //
        // .load c:\windows\Microsoft.NET\Framework\v4.0.30319\SOS.dll
        // !dumpheap -type ContentView
        // !gcroot 02282f34
        // !do 02282f34
        // ===> will show referenced 'view1'

        /*
        private void AssertMeth(ContentView view, Action<ContentView> action)
        {
            action(view);
        }

        private void AssertViewHasLocalContent(ContentView view, bool expected)
        {
            if (expected)
                Assert.IsTrue(view.HasLocalContent);
            else
                Assert.IsFalse(view.HasLocalContent);
        }

        private void AssertAreSames(ContentView view, ContentStore store, IPublishedContent content)
        {
            Assert.AreSame(view, store.GetView());
            Assert.AreSame(content, view.Get(content.Id));
        }
        */

        [Test]
        public void WeakRef()
        {
            var o = new object();
            var wr = new WeakReference(o);
            Assert.IsTrue(wr.IsAlive);
            GC.Collect();
            Assert.IsTrue(wr.IsAlive);
            o = null;
            GC.Collect();
            Assert.IsFalse(wr.IsAlive);
        }

        [Test]
        public void ContentStoreWithTimespan()
        {
            var options = new ContentStore.Options
            {
                TrackViews = true,
                MinViewsInterval = 1000
            };
            var store = new ContentStore(options);

            var props = new[]
                    {
                        new PublishedPropertyType("prop1", 1, "?"), 
                    };

            var contentType1 = new PublishedContentType(1, "ContentType1", props);
            var contentType2 = new PublishedContentType(2, "ContentType2", props);

            var node1 = CreateContentNode(contentType1, 1, null, 1234);
            var node2 = CreateContentNode(contentType2, 2, null, 3456);
            store.Set(node1);
            store.Set(node2);
            Assert.AreEqual(0, store.ViewsCount);

            var view1 = store.GetView();
            Assert.AreEqual(1, store.ViewsCount);
            Assert.IsFalse(view1.HasLocalContent);
            Assert.AreSame(view1, store.GetView());
            Assert.AreSame(node1, view1.Get(node1.Id));

            node1 = CreateContentNode(contentType1, 1, null, 5678);
            store.Set(node1);

            Assert.IsTrue(view1.HasLocalContent);

            // get the same because of timeout
            var view2 = store.GetView();
            Assert.AreEqual(1, store.ViewsCount);
            Assert.AreSame(view1, view2);

            // get another one after timeout
            Thread.Sleep(1100);
            view2 = store.GetView();
            Assert.AreEqual(2, store.ViewsCount);
            Assert.AreNotSame(view1, view2);

            view1 = view2 = null; // dereference both
            GC.Collect();
            Assert.AreEqual(1, store.ViewsCount);
            store.KillViews();
            GC.Collect();
            Assert.AreEqual(0, store.ViewsCount);
        }

        [Test]
        public void RemoveFromStore()
        {
            var options = new ContentStore.Options
            {
                TrackViews = true
            };
            var store = new ContentStore(options);

            var props = new[]
                    {
                        new PublishedPropertyType("prop1", 1, "?"), 
                    };

            var contentType1 = new PublishedContentType(1, "ContentType1", props);
            var contentType2 = new PublishedContentType(2, "ContentType2", props);
            var contentType3 = new PublishedContentType(3, "ContentType3", props);

            var node1 = CreateContentNode(contentType1, 1, null, 1234);
            var node2 = CreateContentNode(contentType2, 2, null, 3456);
            store.Set(node1);
            store.Set(node2);
            Assert.AreEqual(0, store.ViewsCount);

            var view1 = store.GetView();
            Assert.AreEqual(1, store.ViewsCount);
            Assert.IsFalse(view1.HasLocalContent);
            Assert.IsTrue(view1.HasContent);
            Assert.AreSame(view1, store.GetView());

            Assert.AreSame(node1, view1.Get(node1.Id));
            Assert.AreSame(node2, view1.Get(node2.Id));

            var node3 = CreateContentNode(contentType3, 3, null, 5678);
            store.Set(node3);

            Assert.IsTrue(view1.HasLocalContent);
            Assert.IsNull(view1.Get(node3.Id)); // protected!

            store.Set(node3);
            Assert.IsTrue(view1.HasLocalContent);
            Assert.IsNull(view1.Get(node3.Id)); // protected!

            var view2 = store.GetView();
            Assert.AreSame(node3, view2.Get(node3.Id)); // it's there!

            store.Clear(node2.Id);
            Assert.AreSame(node2, view1.Get(node2.Id)); // still there
            var view3 = store.GetView();
            Assert.IsNull(view3.Get(node2.Id)); // gone!

            Assert.AreEqual(2, view1.GetAtRoot().Count());
            Assert.AreEqual(3, view2.GetAtRoot().Count());
            Assert.AreEqual(2, view3.GetAtRoot().Count());
        }

        private static ContentNode CreateContentNode(PublishedContentType contentType, int id, ContentNode parent, int value)
        {
            var d = new Web.PublishedCache.NuCache.DataSource.ContentData
            {
                Published = true,
                Name = "Content " + id,
                Version = Guid.Empty,
                TemplateId = -1,
                VersionDate = DateTime.Now,
                WriterId = -1,
                Properties = new Dictionary<string, object>
                {
                    {"prop1", value}
                }
            };
            var n = new ContentNode(id, contentType,
                (parent == null ? 0 : parent.Level) + 1, (parent == null ? "" : parent.Path) + "/" + id, 0,
                (parent == null ? -1 : parent.Id),
                DateTime.Now, -1,
                null, d);
            return n;
        }
    }
}
