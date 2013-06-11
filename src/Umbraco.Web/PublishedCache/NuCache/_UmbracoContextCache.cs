using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace Umbraco.Web.PublishedCache.NuCache
{
    static class UmbracoContextCache
    {
        static readonly ConditionalWeakTable<UmbracoContext, ConcurrentDictionary<Property, object>> Caches
            = new ConditionalWeakTable<UmbracoContext, ConcurrentDictionary<Property, object>>();

        public static ConcurrentDictionary<Property, object> Current
        {
            get
            {
                var umbracoContext = UmbracoContext.Current;

                // will get or create a value
                // a ConditionalWeakTable is thread-safe
                // does not prevent the context from being disposed, and then the dictionary will be disposed too
                return umbracoContext == null ? null : Caches.GetOrCreateValue(umbracoContext);
            }
        }
    }
}
