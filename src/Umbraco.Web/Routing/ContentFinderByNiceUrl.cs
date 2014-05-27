using System.Linq;
using Umbraco.Core.Logging;
using Umbraco.Core.Models;
using Umbraco.Core;

namespace Umbraco.Web.Routing
{
	/// <summary>
	/// Provides an implementation of <see cref="IContentFinder"/> that handles page nice urls.
	/// </summary>
	/// <remarks>
	/// <para>Handles <c>/foo/bar</c> where <c>/foo/bar</c> is the nice url of a document.</para>
	/// </remarks>
    public class ContentFinderByNiceUrl : IContentFinder
    {
	    /// <summary>
		/// Tries to find and assign an Umbraco document to a <c>PublishedContentRequest</c>.
		/// </summary>
		/// <param name="docRequest">The <c>PublishedContentRequest</c>.</param>		
		/// <returns>A value indicating whether an Umbraco document was found and assigned.</returns>
		public virtual bool TryFindContent(PublishedContentRequest docRequest)
        {
	        if (docRequest.HasDomain)
	        {               
	            var route = docRequest.Domain.RootNodeId + DomainHelper.PathRelativeToDomain(docRequest.DomainUri, docRequest.Uri.GetAbsolutePathDecoded());
                var node = FindContent(docRequest, route);               
                return node != null;
	        }
	        else
	        {
	            var route = docRequest.Uri.GetAbsolutePathDecoded();
                var node = FindContent(docRequest, route);
                return node != null;
	        }
        }

		/// <summary>
		/// Tries to find an Umbraco document for a <c>PublishedContentRequest</c> and a route.
		/// </summary>
		/// <param name="docreq">The document request.</param>
		/// <param name="route">The route.</param>
		/// <returns>The document node, or null.</returns>
        protected IPublishedContent FindContent(PublishedContentRequest docreq, string route)
        {
			LogHelper.Debug<ContentFinderByNiceUrl>("Test route \"{0}\"", () => route);


		    var node = docreq.RoutingContext.UmbracoContext.ContentCache.GetByRoute(route);
            if (node != null)
            {
                //so we have a node but we need to check if a domain is assigned, if one is then we might have a variant for this
                // particular language, so let's check
                if (docreq.HasDomain)
                {
                    var variant = docreq.RoutingContext.UmbracoContext.ContentCache.GetSingleByXPath(
                        string.Format("/root//* [@masterDocId='{0}' and @variantKey='{1}']", node.Id, docreq.Domain.Language.CultureAlias));

                    if (variant != null)
                    {
                        //there is a variant for this culture for this domain so we will use that
                        node = variant;
                    }
                }

                //TODO: If content is found, but it has variants of the language assigned to the domain found here do we
                // match against the variant? Or do we match against the master? Or do we 404 ?

                docreq.PublishedContent = node;
                LogHelper.Debug<ContentFinderByNiceUrl>("Got content, id={0}", () => node.Id);
            }
            else
            {
                LogHelper.Debug<ContentFinderByNiceUrl>("No match.");
            }

		    return node;
        }
    }
}