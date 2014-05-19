using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using Newtonsoft.Json.Linq;
using Umbraco.Core;
using Umbraco.Web.Mvc;
using Umbraco.Web.Routing.Segments;
using Umbraco.Web.Security;

namespace Umbraco.Web.Editors
{
    [PluginController("UmbracoApi")]
    public class SegmentDashboardController : UmbracoAuthorizedJsonController
    {
        /// <summary>
        /// Returns a json array of provider information
        /// </summary>
        /// <returns></returns>
        public JArray GetProviders()
        {
            var providers = ContentSegmentProviderResolver.Current.Providers.ToArray();

            var status = ContentSegmentProvidersStatus.GetProviderStatus();

            //var providerSpecificConfig = providers
            //    .OfType<ConfigurableSegmentProvider>()
            //    .Select(x => x.ReadConfiguration());

            var result = providers
                .Select(x => new
                {
                    typeName = ContentSegmentProvider.GetTypeName(x),
                    displayName = ContentSegmentProvider.GetDisplayName(x),
                    description = ContentSegmentProvider.GetDescription(x),
                    configurable = x is ConfigurableSegmentProvider
                })
                .Select(x => new
                {
                    x.typeName,
                    x.displayName,
                    x.description,
                    x.configurable,
                    enabled = status.ContainsKey(x.typeName) && status[x.typeName]
                });

            return JArray.FromObject(result);
        }

        public HttpResponseMessage PostToggleProvider([FromUri]string typeName)
        {
            var providers = ContentSegmentProviderResolver.Current.Providers.ToArray();
            var status = ContentSegmentProvidersStatus.GetProviderStatus();

            var provider = providers.FirstOrDefault(x => ContentSegmentProvider.GetTypeName(x) == typeName);
            if (provider != null)
            {
                if (status.ContainsKey(typeName))
                {
                    //reverse
                    status[typeName] = (status[typeName] == false);
                }
                else
                {
                    //if it doesn't exist it's not currently enabled
                    status[typeName] = true;
                }

                ContentSegmentProvidersStatus.SaveProvidersStatus(status);
            }

            return Request.CreateResponse(HttpStatusCode.OK);
        }
    }
}