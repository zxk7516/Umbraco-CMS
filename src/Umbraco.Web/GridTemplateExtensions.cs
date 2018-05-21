using System;
using System.Web.Mvc;
using System.Web.Mvc.Html;
using Umbraco.Core.Exceptions;
using Umbraco.Core.Models;
using Umbraco.Core.Models.PublishedContent;

namespace Umbraco.Web
{
    public static class GridTemplateExtensions
    {
        private const string _defaultPropertyAlias = "bodyText";
        private const string _defaultFramework = "bootstrap4";

        public static MvcHtmlString GetGridHtml(this HtmlHelper html, IPublishedContent contentItem)
        {
            return html.GetGridHtml(contentItem, _defaultPropertyAlias, _defaultFramework);
        }

        public static MvcHtmlString GetGridHtml(this HtmlHelper html, IPublishedContent contentItem, string propertyAlias)
        {
            return html.GetGridHtml(contentItem, propertyAlias, _defaultFramework);
        }

        public static MvcHtmlString GetGridHtml(this HtmlHelper html, IPublishedContent contentItem, string propertyAlias, string framework)
        {
            if (string.IsNullOrWhiteSpace(propertyAlias))
                throw new ArgumentNullOrEmptyException(nameof(propertyAlias));

            var property = contentItem.GetProperty(propertyAlias);
            if (property == null)
                throw new NullReferenceException($"No property type found with alias '{propertyAlias}'");

            return html.GetGridHtml(property, framework);
        }

        public static MvcHtmlString GetGridHtml(this HtmlHelper html, IPublishedProperty property, string framework = _defaultFramework)
        {
            if (string.IsNullOrWhiteSpace(framework))
                framework = _defaultFramework;

            var model = property.GetValue();

            // NOTE: The Grid v2 uses a strongly-typed model, v1 is dynamic
            if (model is Grid2Value grid)
                return html.Partial($"Grid2/{framework}", model);

            if (model is string s && string.IsNullOrEmpty(s))
                return new MvcHtmlString(string.Empty);

            return html.Partial($"Grid/{framework}", model);
        }

        public static MvcHtmlString GetGridHtml(this IPublishedContent contentItem, HtmlHelper html)
        {
            return GetGridHtml(contentItem, html, _defaultPropertyAlias, _defaultFramework);
        }

        public static MvcHtmlString GetGridHtml(this IPublishedContent contentItem, HtmlHelper html, string propertyAlias)
        {
            return GetGridHtml(contentItem, html, propertyAlias, _defaultFramework);
        }

        public static MvcHtmlString GetGridHtml(this IPublishedContent contentItem, HtmlHelper html, string propertyAlias, string framework)
        {
            return html.GetGridHtml(contentItem, propertyAlias, framework);
        }

        public static MvcHtmlString GetGridHtml(this IPublishedProperty property, HtmlHelper html, string framework = _defaultFramework)
        {
            return html.GetGridHtml(property, framework);
        }
    }
}
