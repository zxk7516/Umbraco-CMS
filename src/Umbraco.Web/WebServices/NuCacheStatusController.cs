using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web.Http;
using Umbraco.Web.Cache;
using Umbraco.Web.PublishedCache;
using Umbraco.Web.PublishedCache.NuCache;
using Umbraco.Web.WebApi;

namespace Umbraco.Web.WebServices
{
    public class NuCacheStatusController : UmbracoAuthorizedApiController
    {
        private static FacadeService FacadeService
        {
            get
            {
                var svc = PublishedCachesServiceResolver.Current.Service as FacadeService;
                if (svc == null)
                    throw new NotSupportedException("Not running NuCache.");
                return svc;
            }
        }

        [HttpPost]
        public bool RebuildDbCache()
        {
            FacadeService.RebuildContentDbCache();
            FacadeService.RebuildMediaDbCache();
            FacadeService.RebuildMemberDbCache();
            return VerifyDbCache();
        }

        [HttpGet]
        public bool VerifyDbCache()
        {
            return FacadeService.VerifyContentDbCache()
                && FacadeService.VerifyMediaDbCache()
                && FacadeService.VerifyMemberDbCache();
        }

        [HttpPost]
        public void ReloadCache()
        {
            DistributedCache.Instance.RefreshAllPublishedContentCache();
        }
    }
}
