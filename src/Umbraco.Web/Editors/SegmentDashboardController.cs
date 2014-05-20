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

            var result = providers
                .Select(x => new
                {
                    typeName = x.GetType().FullName,
                    displayName = ContentSegmentProvider.GetDisplayName(x),
                    description = ContentSegmentProvider.GetDescription(x),
                    asConfigurable = x as ConfigurableSegmentProvider
                })
                .Select(x => new
                {
                    x.typeName,
                    x.displayName,
                    x.description,
                    configurable = x.asConfigurable != null,
                    config = x.asConfigurable != null ? x.asConfigurable.ReadConfiguration() : null,
                    enabled = status.ContainsKey(x.typeName) && status[x.typeName]
                });

            return JArray.FromObject(result);
        }

        public HttpResponseMessage PostSaveProviderConfig([FromUri]string typeName, IEnumerable<SegmentProviderMatch> config)
        {
            //TODO: We need to validate the config to ensure there are no nulls like null keys

            var providers = ContentSegmentProviderResolver.Current.Providers.ToArray();
            var provider = providers.FirstOrDefault(x => x.GetType().FullName == typeName) as ConfigurableSegmentProvider;
            if (provider == null)
            {
                return Request.CreateErrorResponse(HttpStatusCode.BadRequest, "No provider found with name " + typeName + " or the provider type is not configurable");
            }
            provider.SaveConfiguration(config);

            return Request.CreateResponse(HttpStatusCode.OK);
        }

        public HttpResponseMessage PostToggleProvider([FromUri]string typeName)
        {
            var providers = ContentSegmentProviderResolver.Current.Providers.ToArray();
            var status = ContentSegmentProvidersStatus.GetProviderStatus();

            var provider = providers.FirstOrDefault(x => x.GetType().FullName == typeName);
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