using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.XPath;
using Umbraco.Core.Dynamics;
using Umbraco.Core.Models;
using Umbraco.Core.Models.PublishedContent;
using Umbraco.Core.Xml;
using Umbraco.Core.Xml.XPath;
using umbraco;
using Umbraco.Core;
using Umbraco.Web.PublishedCache.NuCache.Navigable;

namespace Umbraco.Web.PublishedCache.NuCache
{
    class ContentCache : PublishedCacheBase, IPublishedContentCache, INavigableData
    {
        private readonly ContentView _view;

        public ContentCache(bool previewDefault, ContentView view)
            : base(previewDefault)
        {
            _view = view;
        }

        // fixme - the whole GetByRoute / GetRouteById must be refactored

        public IPublishedContent GetByRoute(bool preview, string route, bool? hideTopLevelNode = null)
        {
            if (route == null) throw new ArgumentNullException("route");

            // fixme - this is not optimized at all

            hideTopLevelNode = hideTopLevelNode ?? GlobalSettings.HideTopLevelNodeFromPath; // default = settings

            //the route always needs to be lower case because we only store the urlName attribute in lower case
            route = route.ToLowerInvariant();

            var pos = route.IndexOf('/');
            var path = pos == 0 ? route : route.Substring(pos);
            var startNodeId = pos == 0 ? 0 : int.Parse(route.Substring(0, pos));

            var parts = path.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            if (startNodeId > 0)
            {
                var c = GetById(preview, startNodeId);
                if (parts.Length == 0) return c;
                var i = 0;
                while (c != null && i < parts.Length)
                {
                    c = c.Children.FirstOrDefault(x => x.UrlName == parts[i]);
                    i++;
                }
                if (c != null) return c;
            }
            else
            {
                if (parts.Length == 0) return GetAtRoot(preview).FirstOrDefault();
                var c = GetAtRoot(preview).FirstOrDefault(x => x.UrlName == parts[0]);
                var i = 1;
                while (c != null && i < parts.Length)
                {
                    c = c.Children.FirstOrDefault(x => x.UrlName == parts[i]);
                    i++;
                }
                if (c != null) return c;
            }

            // fixme - and not even complete...

            return null;
        }

        public IPublishedContent GetByRoute(string route, bool? hideTopLevelNode = null)
        {
            return GetByRoute(PreviewDefault, route, hideTopLevelNode);
        }

        public string GetRouteById(bool preview, int contentId)
        {
            // fixme - this is not optimized at all
            // and does not implement domains, nothing

            var segments = new List<string>();
            var c = GetById(preview, contentId);
            while (c != null)
            {
                segments.Add(c.UrlName);
                c = c.Parent;
            }
            segments.Reverse();
            var s = "/" + string.Join("/", segments);

            return s; 
        }

        public string GetRouteById(int contentId)
        {
            return GetRouteById(PreviewDefault, contentId);
        }

        public override IPublishedContent GetById(bool preview, int contentId)
        {
            var n = _view.Get(contentId);
            if (n == null) return null;

            // both .Draft and .Published cannot be null at the same time
            return preview
                ? n.Draft ?? GetPublishedContentAsPreviewing(n.Published)
                : n.Published;
        }

        public override bool HasById(bool preview, int contentId)
        {
            var n = _view.Get(contentId);
            if (n == null) return false;

            return preview || n.Published != null;
        }

        public override IEnumerable<IPublishedContent> GetAtRoot(bool preview)
        {
            var c = _view.GetAtRoot();

            // both .Draft and .Published cannot be null at the same time
            return c.Select(n => preview
                ? n.Draft ?? GetPublishedContentAsPreviewing(n.Published)
                : n.Published).WhereNotNull();
        }

        // gets a published content as a previewing draft, if preview is true
        // this is for published content when previewing
        internal IPublishedContent GetPublishedContentAsPreviewing(IPublishedContent content /*, bool preview*/)
        {
            if (content == null /*|| preview == false*/) return null; //content;
            
            // an object in the cache is either an IPublishedContentOrMedia,
            // or a model inheriting from PublishedContentExtended - in which
            // case we need to unwrap to get to the original IPublishedContentOrMedia.

            var inner = PublishedContent.UnwrapIPublishedContent(content);
            return inner.AsPreviewingModel();
        }

        public override IPublishedContent GetSingleByXPath(bool preview, string xpath, XPathVariable[] vars)
        {
            var navigator = CreateNavigator(preview);
            var iterator = navigator.Select(xpath, vars);
            return GetSingleByXPath(iterator);
        }

        public override IPublishedContent GetSingleByXPath(bool preview, XPathExpression xpath, XPathVariable[] vars)
        {
            var navigator = CreateNavigator(preview);
            var iterator = navigator.Select(xpath, vars);
            return GetSingleByXPath(iterator);
        }

        private static IPublishedContent GetSingleByXPath(XPathNodeIterator iterator)
        {
            if (iterator.MoveNext() == false) return null;

            var xnav = iterator.Current as NavigableNavigator;
            if (xnav == null) return null;

            var xcontent = xnav.UnderlyingObject as NavigableContent;
            return xcontent == null ? null : xcontent.InnerContent;
        }

        public override IEnumerable<IPublishedContent> GetByXPath(bool preview, string xpath, XPathVariable[] vars)
        {
            var navigator = CreateNavigator(preview);
            var iterator = navigator.Select(xpath, vars);
            return GetByXPath(iterator);
        }

        public override IEnumerable<IPublishedContent> GetByXPath(bool preview, XPathExpression xpath, XPathVariable[] vars)
        {
            var navigator = CreateNavigator(preview);
            var iterator = navigator.Select(xpath, vars);
            return GetByXPath(iterator);
        }

        private static IEnumerable<IPublishedContent> GetByXPath(XPathNodeIterator iterator)
        {
            while (iterator.MoveNext())
            {
                var xnav = iterator.Current as NavigableNavigator;
                if (xnav == null) continue;

                var xcontent = xnav.UnderlyingObject as NavigableContent;
                if (xcontent == null) continue;

                yield return xcontent.InnerContent;
            }
        }

        public override XPathNavigator CreateNavigator(bool preview)
        {
            var source = new Source(this, preview);
            var navigator = new NavigableNavigator(source);
            return navigator;
        }

        public override XPathNavigator CreateNodeNavigator(int id, bool preview)
        {
            var source = new Source(this, preview);
            var navigator = new NavigableNavigator(source);
            return navigator.CloneWithNewRoot(id, 0);
        }

        public override bool HasContent(bool preview)
        {
            return preview 
                ? _view.HasContent 
                : _view.GetAtRoot().Any(x => x.Published != null);
        }

        public IPublishedProperty CreateDetachedProperty(PublishedPropertyType propertyType, object value, bool isPreviewing)
        {
            return new Property(propertyType, value, isPreviewing);
        }

        #region Content types

        public override PublishedContentType GetContentType(int id)
        {
            return _view.GetContentType(id);
        }

        public override PublishedContentType GetContentType(string alias)
        {
            return _view.GetContentType(alias);
        }

        #endregion
    }
}
