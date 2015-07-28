using System;
using System.Web.Http;
using Umbraco.Core;
using Umbraco.Core.Models.Rdbms;
using Umbraco.Core.Persistence;
using Umbraco.Core.Services;
using Umbraco.Web.Cache;
using Umbraco.Web.PublishedCache;
using Umbraco.Web.PublishedCache.XmlPublishedCache;
using Umbraco.Web.WebApi;

namespace Umbraco.Web.WebServices
{
    public class XmlDataIntegrityController : UmbracoAuthorizedApiController
    {
        private static FacadeService FacadeService
        {
            get
            {
                var svc = FacadeServiceResolver.Current.Service as FacadeService;
                if (svc == null)
                    throw new NotSupportedException("Unsupported IPublishedCachesService, only the Xml one is supported.");
                return svc;
            }
        }

        [HttpPost]
        public bool FixContentXmlTable()
        {
            FacadeService.RebuildContentAndPreviewXml();
            return FacadeService.VerifyContentAndPreviewXml();
        }

        [HttpPost]
        public bool FixMediaXmlTable()
        {
            FacadeService.RebuildMediaXml();
            return FacadeService.VerifyMediaXml();
        }

        [HttpPost]
        public bool FixMembersXmlTable()
        {
            FacadeService.RebuildMemberXml();
            return FacadeService.VerifyMemberXml();
        }

        [HttpGet]
        public bool CheckContentXmlTable()
        {
            return FacadeService.VerifyContentAndPreviewXml();
        }
        
        [HttpGet]
        public bool CheckMediaXmlTable()
        {
            return FacadeService.VerifyMediaXml();
        }

        [HttpGet]
        public bool CheckMembersXmlTable()
        {
            return FacadeService.VerifyMemberXml();
        }

        [HttpPost]
        public void ReloadXmlCache()
        {
            DistributedCache.Instance.RefreshAllContentCache();
        }
    }
}