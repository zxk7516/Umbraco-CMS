using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Umbraco.Web.PublishedCache
{
    /// <summary>
    /// Provides caches (content and media).
    /// </summary>
    /// <remarks>Groups caches that _may_ be related.</remarks>
    public interface IPublishedCaches
    {
        // fixme - remove
        ///// <summary>
        ///// Creates a contextual content cache for a specified context.
        ///// </summary>
        ///// <param name="context">The context.</param>
        ///// <returns>A new contextual content cache for the specified context.</returns>
        //ContextualPublishedContentCache CreateContextualContentCache(UmbracoContext context);

        ///// <summary>
        ///// Creates a contextual media cache for a specified context.
        ///// </summary>
        ///// <param name="context">The context.</param>
        ///// <returns>A new contextual media cache for the specified context.</returns>
        //ContextualPublishedMediaCache CreateContextualMediaCache(UmbracoContext context);

        // fixme - think
        // the cache is contextual because there's a "session" nothing where nothing changes
        // this is the key point of the dripping cache, that it is stable
        // BUT we don't want to link it to the UmbracoContext, that is too heavy
        // so... do we need an "ambient cache session" mechanism?

        // fixme - document
        // used in tests to configure the XML source = acceptable
        // used in Node to get content = NOT acceptable, MUST be contextual to some extend!!!
        IPublishedContentCache ContentCache { get; }

        // fixme - document
        // not used at the moment
        IPublishedMediaCache MediaCache { get; }
    }
}
