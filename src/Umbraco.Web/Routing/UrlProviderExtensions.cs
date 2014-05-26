using System.Collections.Generic;
using Umbraco.Core.Models;
using umbraco;
using Umbraco.Core.Models.Membership;
using Umbraco.Web.Security;

namespace Umbraco.Web.Routing
{
    internal static class UrlProviderExtensions
    {        
        public static IEnumerable<string> GetContentUrls(this IContent content, IUser user, UrlProvider urlProvider)
        {
            var urls = new List<string>();

            if (content.HasPublishedVersion() == false)
            {
                urls.Add(ui.Text("content", "itemNotPublished", user));
                return urls;
            }

            var url = urlProvider.GetUrl(content.Id);
            if (url == "#")
            {
                // document as a published version yet it's url is "#" => a parent must be
                // unpublished, walk up the tree until we find it, and report.
                var parent = content;
                do
                {
                    parent = parent.ParentId > 0 ? parent.Parent() : null;
                }
                while (parent != null && parent.Published);

                if (parent == null) // oops - internal error
                    urls.Add(ui.Text("content", "parentNotPublishedAnomaly", user));
                else
                    urls.Add(ui.Text("content", "parentNotPublished", parent.Name, user));
            }
            else
            {
                urls.Add(url);
                urls.AddRange(urlProvider.GetOtherUrls(content.Id));
            }
            return urls;
        }

        /// <summary>
        /// Gets the URLs for the content item
        /// </summary>
        /// <param name="content"></param>
        /// <returns></returns>
        /// <remarks>
        /// Use this when displaying URLs, if there are errors genertaing the urls the urls themselves will
        /// contain the errors.
        /// </remarks>
        public static IEnumerable<string> GetContentUrls(this IContent content)
        {
            return content.GetContentUrls(
                UmbracoContext.Current.Security.CurrentUser,
                UmbracoContext.Current.RoutingContext.UrlProvider);
        }
    }
}