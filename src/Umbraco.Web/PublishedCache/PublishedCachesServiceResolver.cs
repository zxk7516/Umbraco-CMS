using Umbraco.Core.ObjectResolution;

namespace Umbraco.Web.PublishedCache
{
    /// <summary>
    /// Resolves the IPublishedCachesService object.
    /// </summary>
    internal sealed class PublishedCachesServiceResolver : SingleObjectResolverBase<PublishedCachesServiceResolver, IPublishedCachesService>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PublishedCachesServiceResolver"/> class with a service implementation.
        /// </summary>
        /// <param name="service">The service.</param>
        /// <remarks>The resolver is created by the <c>WebBootManager</c> and thus the constructor remains internal.</remarks>
        internal PublishedCachesServiceResolver(IPublishedCachesService service)
            : base(service)
        { }

        /// <summary>
        /// Sets the service implementation.
        /// </summary>
        /// <param name="service">The service implementation.</param>
        /// <remarks>For developers, at application startup.</remarks>
        public void SetService(IPublishedCachesService service)
        {
            Value = service;
        }

        /// <summary>
        /// Gets the service implementation.
        /// </summary>
        public IPublishedCachesService Service
        {
            get { return Value; }
        }
    }
}