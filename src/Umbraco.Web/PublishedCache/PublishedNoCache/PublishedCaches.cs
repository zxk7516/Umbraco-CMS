using System;
using Umbraco.Core;
using Umbraco.Core.ObjectResolution;
using Umbraco.Core.Services;

namespace Umbraco.Web.PublishedCache.PublishedNoCache
{
    /// <summary>
    /// Provides caches (content and media).
    /// </summary>
    class PublishedCaches : IPublishedCaches
    {
        private IPublishedContentCache _contentCache;
        private IPublishedMediaCache _mediaCache;

        /// <summary>
        /// Initializes a new instance of the <see cref="PublishedCaches"/> class with a content service factory,
        /// and a media service factory.
        /// </summary>
        /// <param name="contentServiceFactory">A function returning an <c>IContentService</c>.</param>
        /// <param name="mediaServiceFactory">A function returning an <c>IMediaService</c>.</param>
        /// <remarks>The actual services will be obtained once from the factories when resolution freezes.</remarks>
        public PublishedCaches(Func<IContentService> contentServiceFactory, Func<IMediaService> mediaServiceFactory)
        {
            Resolution.Frozen += (sender, args) =>
                {
                    _contentCache = new PublishedContentCache(contentServiceFactory());
                    _mediaCache = new PublishedMediaCache(mediaServiceFactory());
                };
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="PublishedCaches"/> class with a content service,
        /// and a media service.
        /// </summary>
        /// <param name="contentService">An <c>IContentService</c>.</param>
        /// <param name="mediaService">An <c>IMediaService</c>.</param>
        /// <remarks>For tests. Cannot use it in <c>WebBootManager</c> because it is too soon to resolve
        /// the services, hence the other constructor (using factories) that waits for resolution freeze
        /// to actually obtain the services.</remarks>
        internal PublishedCaches(IContentService contentService, IMediaService mediaService)
        {
            _contentCache = new PublishedContentCache(contentService);
            _mediaCache = new PublishedMediaCache(mediaService);
        }

        /// <summary>
        /// Creates a contextual content cache for a specified context.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <returns>A new contextual content cache for the specified context.</returns>
        public ContextualPublishedContentCache CreateContextualContentCache(UmbracoContext context)
        {
            return new ContextualPublishedContentCache(_contentCache, context);
        }

        /// <summary>
        /// Creates a contextual media cache for a specified context.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <returns>A new contextual media cache for the specified context.</returns>
        public ContextualPublishedMediaCache CreateContextualMediaCache(UmbracoContext context)
        {
            return new ContextualPublishedMediaCache(_mediaCache, context);
        }
    }
}
