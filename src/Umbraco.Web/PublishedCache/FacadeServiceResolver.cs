using Umbraco.Core.ObjectResolution;

namespace Umbraco.Web.PublishedCache
{
    /// <summary>
    /// Resolves the IPublishedCachesService object.
    /// </summary>
    internal sealed class FacadeServiceResolver : SingleObjectResolverBase<FacadeServiceResolver, IFacadeService>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="FacadeServiceResolver"/> class with a service implementation.
        /// </summary>
        /// <param name="service">The service.</param>
        /// <remarks>The resolver is created by the <c>WebBootManager</c> and thus the constructor remains internal.</remarks>
        internal FacadeServiceResolver(IFacadeService service)
            : base(service)
        { }

        /// <summary>
        /// Sets the service implementation.
        /// </summary>
        /// <param name="service">The service implementation.</param>
        /// <remarks>For developers, at application startup.</remarks>
        public void SetService(IFacadeService service)
        {
            Value = service;
        }

        /// <summary>
        /// Gets the service implementation.
        /// </summary>
        public IFacadeService Service
        {
            get { return Value; }
        }
    }
}