using Umbraco.Web.PublishedCache.XmlPublishedCache;

namespace Umbraco.Web.PublishedCache
{
    // FIXME
    // this is temp, really - only to give the LB tests a way to reboot the caches
    // but I need to think about a better way to expose that functionnality

    public static class PublishedCachesServiceControl
    {
        public static void ReloadFromDatabase()
        {
            var service = PublishedCachesServiceResolver.Current.Service as PublishedCachesService;
            if (service != null)
                service.XmlStore.ReloadXmlFromDatabase();
        }
    }
}
