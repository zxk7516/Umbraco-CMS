using System.Collections.Generic;
using System.Xml.XPath;
using Umbraco.Core.Xml;
using Umbraco.Core.Models;

namespace Umbraco.Web.PublishedCache
{
    abstract class PublishedCacheBase :IPublishedCache
    {
        // fixme - need a better name
        public bool CurrentPreview { get; private set; }

        protected PublishedCacheBase(bool preview)
        {
            CurrentPreview = preview;
        }

        public abstract IPublishedContent GetById(bool preview, int contentId);

        public IPublishedContent GetById(int contentId)
        {
            return GetById(CurrentPreview, contentId);
        }

        public abstract bool HasById(bool preview, int contentId);

        public bool HasById(int contentId)
        {
            return HasById(CurrentPreview, contentId);
        }

        public abstract IEnumerable<IPublishedContent> GetAtRoot(bool preview);

        public IEnumerable<IPublishedContent> GetAtRoot()
        {
            return GetAtRoot(CurrentPreview);
        }

        public abstract IPublishedContent GetSingleByXPath(bool preview, string xpath, XPathVariable[] vars);

        public IPublishedContent GetSingleByXPath(string xpath, XPathVariable[] vars)
        {
            return GetSingleByXPath(CurrentPreview, xpath, vars);
        }

        public abstract IPublishedContent GetSingleByXPath(bool preview, XPathExpression xpath, XPathVariable[] vars);

        public IPublishedContent GetSingleByXPath(XPathExpression xpath, XPathVariable[] vars)
        {
            return GetSingleByXPath(CurrentPreview, xpath, vars);
        }

        public abstract IEnumerable<IPublishedContent> GetByXPath(bool preview, string xpath, XPathVariable[] vars);

        public IEnumerable<IPublishedContent> GetByXPath(string xpath, XPathVariable[] vars)
        {
            return GetByXPath(CurrentPreview, xpath, vars);
        }

        public abstract IEnumerable<IPublishedContent> GetByXPath(bool preview, XPathExpression xpath, XPathVariable[] vars);

        public IEnumerable<IPublishedContent> GetByXPath(XPathExpression xpath, XPathVariable[] vars)
        {
            return GetByXPath(CurrentPreview, xpath, vars);
        }

        public abstract XPathNavigator CreateNavigator(bool preview);

        public XPathNavigator CreateNavigator()
        {
            return CreateNavigator(CurrentPreview);
        }

        public abstract bool HasContent(bool preview);

        public bool HasContent()
        {
            return HasContent(CurrentPreview);
        }
    }
}
