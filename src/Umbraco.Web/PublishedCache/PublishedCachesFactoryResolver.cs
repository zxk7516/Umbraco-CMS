using Umbraco.Core.ObjectResolution;

namespace Umbraco.Web.PublishedCache
{
    /// <summary>
    /// Resolves the IPublishedCaches object.
    /// </summary>
    internal sealed class PublishedCachesFactoryResolver : SingleObjectResolverBase<PublishedCachesFactoryResolver, IPublishedCachesFactory>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PublishedCachesFactoryResolver"/> class with a factory.
        /// </summary>
        /// <param name="factory">The factory.</param>
        /// <remarks>The resolver is created by the <c>WebBootManager</c> and thus the constructor remains internal.</remarks>
        internal PublishedCachesFactoryResolver(IPublishedCachesFactory factory)
            : base(factory)
        { }

        /// <summary>
        /// Sets the factory.
        /// </summary>
        /// <param name="factory">The factory.</param>
        /// <remarks>For developers, at application startup.</remarks>
        public void SetFactory(IPublishedCachesFactory factory)
        {
            Value = factory;
        }

        /// <summary>
        /// Gets the factory.
        /// </summary>
        public IPublishedCachesFactory Factory
        {
            get { return Value; }
        }
    }
}