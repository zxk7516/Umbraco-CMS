using System.Collections.Generic;
using System.Linq;
using System.Xml.XPath;
using Umbraco.Core.Models.PublishedContent;
using Umbraco.Core.Services;
using Umbraco.Core.Xml;
using Umbraco.Core.Xml.XPath;
using Umbraco.Core.Models;

namespace Umbraco.Web.PublishedCache.PublishedNoCache
{
    // temp - an experimental published cache that does not cache
    // temp - are we handling the published thing correctly? probably NOT?

    class PublishedContentCache : XmlPublishedCache.PublishedContentCache //, IPublishedContentCache
    {
        private readonly IContentService _contentService;

        public PublishedContentCache(IContentService contentService)
        {
            _contentService = contentService;
        }

        public override IPublishedContent GetByRoute(UmbracoContext umbracoContext, bool preview, string route, bool? hideTopLevelNode = null)
        {
            var content = base.GetByRoute(umbracoContext, preview, route, hideTopLevelNode);
            return content == null ? null : PublishedContentModelFactory.CreateModel(content);
        }

        // no need to override that one
        //public override string GetRouteById(UmbracoContext umbracoContext, bool preview, int contentId)
        //{
        //    return base.GetRouteById(umbracoContext, preview, contentId);
        //}

        public override IPublishedContent GetById(UmbracoContext umbracoContext, bool preview, int contentId)
        {
            return GetById(preview, contentId);
        }

        internal IPublishedContent GetById(bool preview, int contentId)
        {
            var content = preview
                ? _contentService.GetById(contentId) // gets the latest version, including draft
                : _contentService.GetPublishedVersion(contentId); // gets the published version, or null

            return content == null ? null : PublishedContentModelFactory.CreateModel(new PublishedContent(content, this, preview));
        }

        public override IEnumerable<IPublishedContent> GetAtRoot(UmbracoContext umbracoContext, bool preview)
        {
            var content = _contentService.GetRootContent(); // gets the latest versions, including drafts

            if (preview == false)
                content = content
                    .Select(c => c.Published ? c : _contentService.GetPublishedVersion(c.Id))
                    .Where(c => c != null);

            return content
                .OrderBy(c => c.SortOrder)
                .Select(c => PublishedContentModelFactory.CreateModel(new PublishedContent(c, this, preview)));
        }

        public override IPublishedContent GetSingleByXPath(UmbracoContext umbracoContext, bool preview, string xpath, params XPathVariable[] vars)
        {
            var navigator = GetXPathNavigator(umbracoContext, preview);
            var iterator = navigator.Select(xpath, vars);
            return GetSingleByXPath(iterator);
        }

        public override IPublishedContent GetSingleByXPath(UmbracoContext umbracoContext, bool preview, XPathExpression xpath, params XPathVariable[] vars)
        {
            var navigator = GetXPathNavigator(umbracoContext, preview);
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

        public override IEnumerable<IPublishedContent> GetByXPath(UmbracoContext umbracoContext, bool preview, string xpath, params XPathVariable[] vars)
        {
            var navigator = GetXPathNavigator(umbracoContext, preview);
            var iterator = navigator.Select(xpath, vars);
            return GetByXPath(iterator);
        }

        public override IEnumerable<IPublishedContent> GetByXPath(UmbracoContext umbracoContext, bool preview, XPathExpression xpath, params XPathVariable[] vars)
        {
            var navigator = GetXPathNavigator(umbracoContext, preview);
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

        public override XPathNavigator GetXPathNavigator(UmbracoContext umbracoContext, bool preview)
        {
            var source = new Navigable.Source(this, umbracoContext, preview);
            var navigator = new NavigableNavigator(source);
            return navigator;
        }

        public override bool XPathNavigatorIsNavigable
        {
            get { return true; }
        }

        public override bool HasContent(UmbracoContext umbracoContext, bool preview)
        {
            return GetAtRoot(umbracoContext, preview).Any();
        }
    }
}
