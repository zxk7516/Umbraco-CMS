using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Umbraco.Web.PublishedCache
{
    interface IPublishedCachesFactory
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
         * nothing of what is "current". In most cases, the IPublishedCaches object is created
         * and registered against an UmbracoContext, and that context is then used as "current".
         * 
         * But for tests we need to have a way to specify what's the "current" object & preview.
         * 
         */

        IPublishedCaches CreatePublishedCaches(bool preview);
        IPublishedCaches GetPublishedCaches();
    }
}
