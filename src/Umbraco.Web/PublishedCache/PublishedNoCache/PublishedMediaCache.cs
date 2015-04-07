using System.Collections.Generic;
using System.Linq;
using System.Xml.XPath;
using Umbraco.Core;
using Umbraco.Core.Models.PublishedContent;
using Umbraco.Core.Services;
using Umbraco.Core.Xml;
using Umbraco.Core.Xml.XPath;
using Umbraco.Core.Models;
using Umbraco.Web.PublishedCache.PublishedNoCache.Navigable;

namespace Umbraco.Web.PublishedCache.PublishedNoCache
{
    class PublishedMediaCache : PublishedCacheBase, IPublishedMediaCache, INavigableData
    {
        private readonly IMediaService _mediaService;
        private readonly IMediaTypeService _mediaTypeService;

        public PublishedMediaCache(bool preview, IMediaService mediaService, IMediaTypeService mediaTypeService)
            : base(preview)
        {
            _mediaService = mediaService;
            _mediaTypeService = mediaTypeService;
        }

        public override IPublishedContent GetById(bool preview, int contentId)
        {
            var content = _mediaService.GetById(contentId);
            return content == null ? null : (new PublishedMedia(content, this, preview)).CreateModel();
        }

        public override bool HasById(bool preview, int contentId)
        {
            return _mediaService.GetById(contentId) != null;
        }

        public override IEnumerable<IPublishedContent> GetAtRoot(bool preview)
        {
            var content = _mediaService.GetRootMedia();
            return content.Select(c => (new PublishedMedia(c, this, preview)).CreateModel());
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

                yield return (xcontent.InnerContent);
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

        #region Content types

        public override PublishedContentType GetContentType(int id)
        {
            var contentType = _mediaTypeService.Get(id);
            return contentType == null ? null : new PublishedContentType(contentType);
        }

        public override PublishedContentType GetContentType(string alias)
        {
            var contentType = _mediaTypeService.Get(alias);
            return contentType == null ? null : new PublishedContentType(contentType);
        }

        #endregion
    }
}
