using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml.XPath;
using Umbraco.Core;
using Umbraco.Core.Models.PublishedContent;
using Umbraco.Core.Services;
using Umbraco.Core.Xml;
using Umbraco.Core.Xml.XPath;
using Umbraco.Core.Models;
using Umbraco.Web.Routing;

namespace Umbraco.Web.PublishedCache.PublishedNoCache
{
    // temp - an experimental published cache that does not cache
    // temp - are we handling the published thing correctly? probably NOT?

    class PublishedContentCache : PublishedCacheBase, IPublishedContentCache
    {
        private readonly IContentService _contentService;

        public PublishedContentCache(string previewToken, IContentService contentService)
            : base(previewToken.IsNullOrWhiteSpace() == false)
        {
            _contentService = contentService;
        }

        #region Routes

        public IPublishedContent GetByRoute(string route, bool? hideTopLevelNode = null)
        {
            return GetByRoute(CurrentPreview, route, hideTopLevelNode);
        }

        public IPublishedContent GetByRoute(bool preview, string route, bool? hideTopLevelNode = null)
        {
            if (route == null) throw new ArgumentNullException("route");

            // determine the id
            hideTopLevelNode = hideTopLevelNode ?? Core.Configuration.GlobalSettings.HideTopLevelNodeFromPath; // default = settings

            //the route always needs to be lower case because we only store the urlName attribute in lower case
            route = route.ToLowerInvariant();

            var pos = route.IndexOf('/');
            var path = pos == 0 ? route : route.Substring(pos);
            var startNodeId = pos == 0 ? 0 : int.Parse(route.Substring(0, pos));

            var segments = path.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            var rootContent = startNodeId > 0 ? GetById(preview, startNodeId) : null;

            var content = GetContentByRoute(rootContent, segments, hideTopLevelNode.Value, preview);
            return content;
        }

        private IPublishedContent GetContentByRoute(IPublishedContent rootContent, IList<string> segments,
            bool hideTopLevelNode, bool preview)
        {
            if (rootContent != null) // we have a domain
            {
                var content = rootContent;
                if (segments.Count == 0) return content;
                var i = 0;
                while (content != null && i < segments.Count)
                {
                    // case-sensitive routes...
                    content = content.Children.FirstOrDefault(x => x.UrlName == segments[i]);
                    i++;
                }
                if (content != null) return content;
            }
            else // we don't have a domain
            {
                var contentAtRoot = GetAtRoot(preview);
                if (segments.Count == 0) return contentAtRoot.FirstOrDefault();
                IPublishedContent content;
                if (hideTopLevelNode)
                {
                    // we won't try to implement the legacy thing about multiple content at root
                    // so we assume that there's only one content at root else we would not be
                    // ignoring top level - anyway we probably need to think the whole urls thing
                    // over...
                    content = contentAtRoot.FirstOrDefault();
                    if (content == null) return null;
                    content = content.Children.FirstOrDefault(x => x.UrlName == segments[0]);
                }
                else
                {
                    content = contentAtRoot.FirstOrDefault(x => x.UrlName == segments[0]);
                }
                var i = 1;
                while (content != null && i < segments.Count)
                {
                    // case-sensitive routes...
                    content = content.Children.FirstOrDefault(x => x.UrlName == segments[i]);
                    i++;
                }
                if (content != null) return content;
            }

            return null;
        }

        public string GetRouteById(int contentId)
        {
            return GetRouteById(CurrentPreview, contentId);
        }

        public string GetRouteById(bool preview, int contentId)
        {
            var node = GetById(preview, contentId);
            if (node == null)
                return null;

            // walk up from that node until we hit a node with a domain,
            // or we reach the content root, collecting urls in the way
            var pathParts = new List<string>();
            var n = node;
            var hasDomains = DomainHelper.NodeHasDomains(n.Id);
            while (hasDomains == false && n != null) // n is null at root
            {
                // get the url
                var urlName = n.UrlName;
                pathParts.Add(urlName);

                // move to parent node
                n = n.Parent;
                hasDomains = n != null && DomainHelper.NodeHasDomains(n.Id);
            }

            // no domain, respect HideTopLevelNodeFromPath for legacy purposes
            if (hasDomains == false && Core.Configuration.GlobalSettings.HideTopLevelNodeFromPath)
                ApplyHideTopLevelNodeFromPath(node, pathParts, preview);

            // assemble the route
            pathParts.Reverse();
            var path = "/" + string.Join("/", pathParts); // will be "/" or "/foo" or "/foo/bar" etc
            var route = (n == null ? "" : n.Id.ToString(CultureInfo.InvariantCulture)) + path;

            return route;
        }

        private void ApplyHideTopLevelNodeFromPath(IPublishedContent content, IList<string> segments, bool preview)
        {
            // in theory if hideTopLevelNodeFromPath is true, then there should be only once
            // top-level node, or else domains should be assigned. but for backward compatibility
            // we add this check - we look for the document matching "/" and if it's not us, then
            // we do not hide the top level path
            // it has to be taken care of in GetByRoute too so if
            // "/foo" fails (looking for "/*/foo") we try also "/foo". 
            // this does not make much sense anyway esp. if both "/foo/" and "/bar/foo" exist, but
            // that's the way it works pre-4.10 and we try to be backward compat for the time being
            if (content.Parent == null)
            {
                var rootNode = GetByRoute(preview, "/", true);
                if (rootNode == null)
                    throw new Exception("Failed to get node at /.");
                if (rootNode.Id == content.Id) // remove only if we're the default node
                    segments.RemoveAt(segments.Count - 1);
            }
            else
            {
                segments.RemoveAt(segments.Count - 1);
            }
        }

        #endregion

        #region Getters

        public override IPublishedContent GetById(bool preview, int contentId)
        {
            var content = preview
                ? _contentService.GetById(contentId) // gets the latest version, including draft
                : _contentService.GetPublishedVersion(contentId); // gets the published version, or null

            return content == null ? null : (new PublishedContent(content, this, preview)).CreateModel();
        }

        public override bool HasById(bool preview, int contentId)
        {
            var content = preview
                ? _contentService.GetById(contentId) // gets the latest version, including draft
                : _contentService.GetPublishedVersion(contentId); // gets the published version, or null
            return content != null;
        }

        public override IEnumerable<IPublishedContent> GetAtRoot(bool preview)
        {
            var content = _contentService.GetRootContent(); // gets the latest versions, including drafts

            if (preview == false)
                content = content
                    .Select(c => c.Published ? c : _contentService.GetPublishedVersion(c.Id))
                    .Where(c => c != null);

            return content
                .OrderBy(c => c.SortOrder)
                .Select(c => (new PublishedContent(c, this, preview)).CreateModel());
        }

        public override IPublishedContent GetSingleByXPath(bool preview, string xpath, params XPathVariable[] vars)
        {
            var navigator = CreateNavigator(preview);
            var iterator = navigator.Select(xpath, vars);
            return GetSingleByXPath(iterator);
        }

        public override IPublishedContent GetSingleByXPath(bool preview, XPathExpression xpath, params XPathVariable[] vars)
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

            var xcontent = xnav.UnderlyingObject as Navigable.NavigableContent;
            return xcontent == null ? null : xcontent.InnerContent;
        }

        public override IEnumerable<IPublishedContent> GetByXPath(bool preview, string xpath, params XPathVariable[] vars)
        {
            var navigator = CreateNavigator(preview);
            var iterator = navigator.Select(xpath, vars);
            return GetByXPath(iterator);
        }

        public override IEnumerable<IPublishedContent> GetByXPath(bool preview, XPathExpression xpath, params XPathVariable[] vars)
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

                var xcontent = xnav.UnderlyingObject as Navigable.NavigableContent;
                if (xcontent == null) continue;

                yield return xcontent.InnerContent;
            }
        }

        public override XPathNavigator CreateNavigator(bool preview)
        {
            var source = new Navigable.Source(this, preview);
            var navigator = new NavigableNavigator(source);
            return navigator;
        }

        public override XPathNavigator CreateNodeNavigator(int id, bool preview)
        {
            var source = new Navigable.Source(this, preview);
            var navigator = new NavigableNavigator(source);
            return navigator.CloneWithNewRoot(id, 0);
        }

        public override bool HasContent(bool preview)
        {
            return GetAtRoot(preview).Any();
        }

        #endregion

        #region Detached

        public IPublishedProperty CreateDetachedProperty(PublishedPropertyType propertyType, object value, bool isPreviewing)
        {
            if (propertyType.IsDetachedOrNested == false)
                throw new ArgumentException("Property type is neither detached nor nested.", "propertyType");
            return new PublishedProperty(propertyType, value, isPreviewing);
        }

        #endregion
    }
}
