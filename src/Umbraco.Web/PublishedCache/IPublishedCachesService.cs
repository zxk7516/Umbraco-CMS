using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Umbraco.Core.Models.Membership;

namespace Umbraco.Web.PublishedCache
{
    interface IPublishedCachesService
    {
        /* Various places (such as Node) want to access the XML content, today as an XmlDocument
         * but to migrate to a new cache, they're migrating to an XPathNavigator. Still, they need
         * to find out how to get that navigator.
         * 
         * Because a cache such as the DrippingCache is contextual ie it has a "snapshot" nothing
         * and remains consistent over the snapshot, the navigator should come from the "current"
         * snapshot.
         * 
         * The factory creates those snapshots in IPublishedCaches objects.
         * 
         * Places such as Node need to be able to find the "current" one so the factory has a
         * notion of what is "current". In most cases, the IPublishedCaches object is created
         * and registered against an UmbracoContext, and that context is then used as "current".
         * 
         * But for tests we need to have a way to specify what's the "current" object & preview.
         * Which is defined in PublishedCacheFactoryBase.
         * 
         */

        /// <summary>
        /// Creates a set of published caches.
        /// </summary>
        /// <param name="previewToken">A preview token, or <c>null</c> if not previewing.</param>
        /// <returns>A set of published caches.</returns>
        IPublishedCaches CreatePublishedCaches(string previewToken);

        /// <summary>
        /// Gets the current set of published caches.
        /// </summary>
        /// <returns>The current set of published caches.</returns>
        /// <remarks></remarks>
        IPublishedCaches GetPublishedCaches();

        /* Later on we can imagine that EnterPreview would handle a "level" that would be either
         * the content only, or the content's branch, or the whole tree + it could be possible
         * to register filters against the factory to filter out which nodes should be preview
         * vs non preview.
         * 
         * EnterPreview() returns the previewToken. It is up to callers to store that token
         * wherever they want, most probably in a cookie.
         * 
         */

        /// <summary>
        /// Enters preview for specified user and content.
        /// </summary>
        /// <param name="user">The user.</param>
        /// <param name="contentId">The content identifier.</param>
        /// <returns>A preview token.</returns>
        /// <remarks>
        /// <para>Tells the caches that they should prepare any data that they would be keeping
        /// in order to provide preview to a give user. In the Xml cache this means creating the Xml
        /// file, though other caches may do things differently.</para>
        /// <para>Does not handle the preview token storage (cookie, etc) that must be handled separately.</para>
        /// </remarks>
        string EnterPreview(IUser user, int contentId);

        /// <summary>
        /// Refreshes preview for a specifiedcontent.
        /// </summary>
        /// <param name="previewToken">The preview token.</param>
        /// <param name="contentId">The content identifier.</param>
        /// <remarks>Tells the caches that they should update any data that they would be keeping
        /// in order to provide preview to a given user. In the Xml cache this means updating the Xml
        /// file, though other caches may do things differently.</remarks>
        void RefreshPreview(string previewToken, int contentId);

        /// <summary>
        /// Exits preview for a specified preview token.
        /// </summary>
        /// <param name="previewToken">The preview token.</param>
        /// <remarks>
        /// <para>Tells the caches that they can dispose of any data that they would be keeping
        /// in order to provide preview to a given user. In the Xml cache this means deleting the Xml file,
        /// though other caches may do things differently.</para>
        /// <para>Does not handle the preview token storage (cookie, etc) that must be handled separately.</para>
        /// </remarks>
        void ExitPreview(string previewToken);

        /* Maintain the cache...
         * 
         * The service should subscribe to the proper events in order to maintain the cache content
         * consistent with what is in the database, accross all LB servers. So there is no need to
         * tell the service that a content has changed, etc. However,
         * 
         * - the service may defer some actions, eg the Xml cache does not write to disk each time
         *   the Xml content changes but only at the end of the current http request. So, service
         *   implement the FlushChanges method that can be used to tell them it is a good time to
         *   do whatever they have defered.
         *   
         * - the service may need to be resetted - (not working on that one yet)
         *
         */

        /// <summary>
        /// Signals the service that it is a good time to execute defered actions.
        /// </summary>
        /// <remarks>What this means exactly depends on the cache.</remarks>
        void FlushChanges();
    }
}
