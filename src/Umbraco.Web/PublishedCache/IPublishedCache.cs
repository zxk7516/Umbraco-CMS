using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.XPath;
using Umbraco.Core.CodeAnnotations;
using Umbraco.Core.Models;
using Umbraco.Core.Xml;

namespace Umbraco.Web.PublishedCache
{
    /// <summary>
    /// Provides access to cached contents.
    /// </summary>
    public interface IPublishedCache : IXPathNavigable
    {
        /// <summary>
        /// Gets a content identified by its unique identifier.
        /// </summary>
        /// <param name="preview">A value indicating whether to consider unpublished content.</param>
        /// <param name="contentId">The content unique identifier.</param>
        /// <returns>The content, or null.</returns>
        /// <remarks>The value of <paramref name="preview"/> overrides the context.</remarks>
        IPublishedContent GetById(bool preview, int contentId);

        /// <summary>
        /// Gets a content identified by its unique identifier.
        /// </summary>
        /// <param name="contentId">The content unique identifier.</param>
        /// <returns>The content, or null.</returns>
        /// <remarks>Considers published or unpublished content depending on defaults.</remarks>
        IPublishedContent GetById(int contentId);

        /// <summary>
        /// Gets a value indicating whether the cache contains a specified content.
        /// </summary>
        /// <param name="preview">A value indicating whether to consider unpublished content.</param>
        /// <param name="contentId">The content unique identifier.</param>
        /// <returns>A value indicating whether to the cache contains the specified content.</returns>
        bool HasById(bool preview, int contentId);

        /// <summary>
        /// Gets a value indicating whether the cache contains a specified content.
        /// </summary>
        /// <param name="contentId">The content unique identifier.</param>
        /// <returns>A value indicating whether to the cache contains the specified content.</returns>
        /// <remarks>Considers published or unpublished content depending on defaults.</remarks>
        bool HasById(int contentId);

        /// <summary>
        /// Gets contents at root.
        /// </summary>
        /// <param name="preview">A value indicating whether to consider unpublished content.</param>
        /// <returns>The contents.</returns>
        /// <remarks>The value of <paramref name="preview"/> overrides the context.</remarks>
        IEnumerable<IPublishedContent> GetAtRoot(bool preview);

        /// <summary>
        /// Gets contents at root.
        /// </summary>
        /// <returns>The contents.</returns>
        /// <remarks>Considers published or unpublished content depending on defaults.</remarks>
        IEnumerable<IPublishedContent> GetAtRoot();

        /// <summary>
        /// Gets a content resulting from an XPath query.
        /// </summary>
        /// <param name="preview">A value indicating whether to consider unpublished content.</param>
        /// <param name="xpath">The XPath query.</param>
        /// <param name="vars">Optional XPath variables.</param>
        /// <returns>The content, or null.</returns>
        /// <remarks>The value of <paramref name="preview"/> overrides the context.</remarks>
        IPublishedContent GetSingleByXPath(bool preview, string xpath, params XPathVariable[] vars);

        /// <summary>
        /// Gets a content resulting from an XPath query.
        /// </summary>
        /// <param name="xpath">The XPath query.</param>
        /// <param name="vars">Optional XPath variables.</param>
        /// <returns>The content, or null.</returns>
        /// <remarks>Considers published or unpublished content depending on defaults.</remarks>
        IPublishedContent GetSingleByXPath(string xpath, params XPathVariable[] vars);

        /// <summary>
        /// Gets a content resulting from an XPath query.
        /// </summary>
        /// <param name="preview">A value indicating whether to consider unpublished content.</param>
        /// <param name="xpath">The XPath query.</param>
        /// <param name="vars">Optional XPath variables.</param>
        /// <returns>The content, or null.</returns>
        /// <remarks>The value of <paramref name="preview"/> overrides the context.</remarks>
        IPublishedContent GetSingleByXPath(bool preview, XPathExpression xpath, params XPathVariable[] vars);

        /// <summary>
        /// Gets a content resulting from an XPath query.
        /// </summary>
        /// <param name="xpath">The XPath query.</param>
        /// <param name="vars">Optional XPath variables.</param>
        /// <returns>The content, or null.</returns>
        /// <remarks>Considers published or unpublished content depending on defaults.</remarks>
        IPublishedContent GetSingleByXPath(XPathExpression xpath, params XPathVariable[] vars);

        /// <summary>
        /// Gets contents resulting from an XPath query.
        /// </summary>
        /// <param name="preview">A value indicating whether to consider unpublished content.</param>
        /// <param name="xpath">The XPath query.</param>
        /// <param name="vars">Optional XPath variables.</param>
        /// <returns>The contents.</returns>
        /// <remarks>The value of <paramref name="preview"/> overrides the context.</remarks>
        IEnumerable<IPublishedContent> GetByXPath(bool preview, string xpath, params XPathVariable[] vars);

        /// <summary>
        /// Gets contents resulting from an XPath query.
        /// </summary>
        /// <param name="xpath">The XPath query.</param>
        /// <param name="vars">Optional XPath variables.</param>
        /// <returns>The contents.</returns>
        /// <remarks>Considers published or unpublished content depending on defaults.</remarks>
        IEnumerable<IPublishedContent> GetByXPath(string xpath, params XPathVariable[] vars);

        /// <summary>
        /// Gets contents resulting from an XPath query.
        /// </summary>
        /// <param name="preview">A value indicating whether to consider unpublished content.</param>
        /// <param name="xpath">The XPath query.</param>
        /// <param name="vars">Optional XPath variables.</param>
        /// <returns>The contents.</returns>
        /// <remarks>The value of <paramref name="preview"/> overrides the context.</remarks>
        IEnumerable<IPublishedContent> GetByXPath(bool preview, XPathExpression xpath, params XPathVariable[] vars);

        /// <summary>
        /// Gets contents resulting from an XPath query.
        /// </summary>
        /// <param name="xpath">The XPath query.</param>
        /// <param name="vars">Optional XPath variables.</param>
        /// <returns>The contents.</returns>
        /// <remarks>Considers published or unpublished content depending on defaults.</remarks>
        IEnumerable<IPublishedContent> GetByXPath(XPathExpression xpath, params XPathVariable[] vars);

        /// <summary>
        /// Creates an XPath navigator that can be used to navigate contents.
        /// </summary>
        /// <param name="preview">A value indicating whether to consider unpublished content.</param>
        /// <returns>The XPath navigator.</returns>
        /// <remarks>
        /// <para>The value of <paramref name="preview"/> overrides the context.</para>
        /// <para>The navigator is already a safe clone (no need to clone it again).</para>
        /// </remarks>
        XPathNavigator CreateNavigator(bool preview);

        /// <summary>
        /// Creates an XPath navigator that can be used to navigate one node.
        /// </summary>
        /// <param name="id">The node identifier.</param>
        /// <param name="preview">A value indicating whether to consider unpublished content.</param>
        /// <returns>The XPath navigator.</returns>
        /// <remarks>
        /// <para>The value of <paramref name="preview"/> overrides the context.</para>
        /// <para>The navigator is already a safe clone (no need to clone it again).</para>
        /// <para>Navigates over the node - and only the node, ie no children. Exists only for backward
        /// compatibility + transition reasons, we should obsolete that one as soon as possible.</para>
        /// </remarks>
        // fixme - what-if the node does not exist?
        XPathNavigator CreateNodeNavigator(int id, bool preview);

        /// <summary>
        /// Creates an XPath navigator that can be used to navigate contents.
        /// </summary>
        /// <returns>The XPath navigator.</returns>
        /// <remarks>
        /// <para>Considers published or unpublished content depending on defaults.</para>
        /// <para>The navigator is already a safe clone (no need to clone it again).</para>
        /// <para>Implements IXPathNavigable.CreateNavigator().</para>
        /// </remarks>
        //XPathNavigator CreateNavigator();

        /// <summary>
        /// Gets a value indicating whether the cache contains published content.
        /// </summary>
        /// <param name="preview">A value indicating whether to consider unpublished content.</param>
        /// <returns>A value indicating whether the cache contains published content.</returns>
        /// <remarks>The value of <paramref name="preview"/> overrides the context.</remarks>
        bool HasContent(bool preview);

        /// <summary>
        /// Gets a value indicating whether the cache contains published content.
        /// </summary>
        /// <returns>A value indicating whether the cache contains published content.</returns>
        /// <remarks>Considers published or unpublished content depending on defaults.</remarks>
        bool HasContent();

        //TODO: SD: We should make this happen! This will allow us to natively do a GetByDocumentType query
	    // on the UmbracoHelper (or an internal DataContext that it uses, etc...)
	    // One issue is that we need to make media work as fast as we can and need to create a ConvertFromMediaObject
	    // method in the DefaultPublishedMediaStore, there's already a TODO noting this but in order to do that we'll 
	    // have to also use Examine as much as we can so we don't have to make db calls for looking up things like the 
	    // node type alias, etc... in order to populate the created IPublishedContent object.
	    //IEnumerable<IPublishedContent> GetDocumentsByType(string docTypeAlias);
    }
}
