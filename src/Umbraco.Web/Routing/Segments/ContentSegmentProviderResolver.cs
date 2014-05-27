using System;
using System.Collections.Generic;
using Umbraco.Core.ObjectResolution;
using System.Linq;

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

        public IEnumerable<ContentVariantAttribute> GetAssignableVariants(IDictionary<string, bool> segmentProviderStatus)
        {
            //These are the assignable variants based on the installed providers (statically advertised variants)
            // that are enabled via the back office. If they are not enabled, they will not show up.

            var assignableSegments = this.Providers
                //don't lookup anything in any providers that are not enabled
                .Where(provider => segmentProviderStatus[provider.GetType().FullName] == true)
                .Select(provider => new
                {
                    instance = provider,
                    //get the keys that have been allowed
                    enabledVariants = provider.ReadVariantConfiguration()
                        .Where(vari => vari.Value) // the value == true
                        .Select(vari => vari.Key).ToArray() // get the key
                })
                //only allow the onces that are enabled
                .SelectMany(x => x.instance.AssignableContentVariants.Where(vari => x.enabledVariants.Contains(vari.SegmentMatchKey)));

            return assignableSegments;
        }
    }
}