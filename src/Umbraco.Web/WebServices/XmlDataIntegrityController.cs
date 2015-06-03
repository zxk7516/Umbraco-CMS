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
        private static PublishedCachesService PublishedCachesService
        {
            get
            {
                var svc = PublishedCachesServiceResolver.Current.Service as PublishedCachesService;
                if (svc == null)
                    throw new NotSupportedException("Unsupported IPublishedCachesService, only the Xml one is supported.");
                return svc;
            }
        }

        [HttpPost]
        public bool FixContentXmlTable()
        {
            PublishedCachesService.RebuildContentAndPreviewXml();
            return PublishedCachesService.VerifyContentAndPreviewXml();
        }

        [HttpPost]
        public bool FixMediaXmlTable()
        {
            PublishedCachesService.RebuildMediaXml();
            return PublishedCachesService.VerifyMediaXml();
        }

        [HttpPost]
        public bool FixMembersXmlTable()
        {
            PublishedCachesService.RebuildMemberXml();
            return PublishedCachesService.VerifyMemberXml();
        }

        [HttpGet]
        public bool CheckContentXmlTable()
        {
            return PublishedCachesService.VerifyContentAndPreviewXml();
        }
        
        [HttpGet]
        public bool CheckMediaXmlTable()
        {
            return PublishedCachesService.VerifyMediaXml();
        }

        [HttpGet]
        public bool CheckMembersXmlTable()
        {
            return PublishedCachesService.VerifyMemberXml();
        }

        [HttpPost]
        public void ReloadXmlCache()
        {
            DistributedCache.Instance.RefreshAllContentCache();
        }
    }
}