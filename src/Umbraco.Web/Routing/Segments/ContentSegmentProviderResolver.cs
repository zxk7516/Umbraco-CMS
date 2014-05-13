using System;
using System.Collections.Generic;
using Umbraco.Core.ObjectResolution;

namespace Umbraco.Web.Routing.Segments
{
    /// <summary>
    /// Resolver for content variation providers
    /// </summary>
    public sealed class ContentSegmentProviderResolver : ManyObjectsResolverBase<ContentSegmentProviderResolver, ContentSegmentProvider>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ContentSegmentProviderResolver"/> class with 
        /// an initial list of provider types.
        /// </summary>
        /// <param name="converters">The list of provider types</param>
        /// <remarks>The resolver is created by the <c>WebBootManager</c> and thus the constructor remains internal.</remarks>
        internal ContentSegmentProviderResolver(IEnumerable<Type> converters)
			: base(converters)
		{ }

        /// <summary>
        /// Initializes a new instance of the <see cref="ContentSegmentProviderResolver"/> class with 
        /// an initial list of provider types.
        /// </summary>
        /// <param name="converters">The list of provider types</param>
        /// <remarks>The resolver is created by the <c>WebBootManager</c> and thus the constructor remains internal.</remarks>
        internal ContentSegmentProviderResolver(params Type[] converters)
            : base(converters)
        { }

        /// <summary>
        /// Gets the providers.
        /// </summary>
        public IEnumerable<ContentSegmentProvider> Providers
        {
            get { return Values; }
        }
    }
}