using System;
using System.Linq;
using Umbraco.Core.Models.PublishedContent;
using Umbraco.Core.Xml.XPath;

namespace Umbraco.Web.PublishedCache.PublishedNoCache.Navigable
{
    class Source : INavigableSource
    {
        private readonly IPublishedCache _cache;
        private readonly UmbracoContext _umbracoContext;
        private readonly bool _preview;
        private readonly RootContent _root;

        public Source(IPublishedCache cache, UmbracoContext umbracoContext, bool preview)
        {
            _cache = cache;
            _umbracoContext = umbracoContext;
            _preview = preview;

            var contentAtRoot = cache.GetAtRoot(umbracoContext, preview);
            _root = new RootContent(contentAtRoot.Select(x => x.Id));
        }

        public INavigableContent Get(int contentId)
        {
            // wrap in a navigable content

            var content = _cache.GetById(_umbracoContext, _preview, contentId);
            if (content == null) return null;

            // content may be a strongly typed model, have to unwrap first

            PublishedContentWrapped wrapped;
            while ((wrapped = content as PublishedContentWrapped) != null)
                content = wrapped.Unwrap();
            var published = content as IPublishedContentOrMedia;
            if (published == null)
                throw new InvalidOperationException("Innermost content is not IPublishedContentOrMedia.");
            return new NavigableContent(published);
        }

        public int LastAttributeIndex
        {
            get { return NavigableContentType.BuiltinProperties.Length - 1; }
        }

        public INavigableContent Root
        {
            get { return _root; }
        }
    }
}
