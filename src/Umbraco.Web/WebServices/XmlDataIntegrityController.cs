using System;
using System.Web.Http;
using Umbraco.Core;
using Umbraco.Core.Models.Rdbms;
using Umbraco.Core.Persistence;
using Umbraco.Core.Services;
using Umbraco.Web.WebApi;

namespace Umbraco.Web.WebServices
{

    public class XmlDataIntegrityController : UmbracoAuthorizedApiController
    {
        // fixme - not handling preview here

        [HttpPost]
        public bool FixContentXmlTable()
        {
            // fixme - should be done in the XmlCache, not in the service
            var svc = (ContentService)Services.ContentService;
            svc.RebuildContentXml();
            return CheckContentXmlTable();
        }

        [HttpPost]
        public bool FixMediaXmlTable()
        {
            // fixme - should be done in the XmlCache, not in the service
            var svc = (MediaService)Services.MediaService;
            svc.RebuildMediaXml();
            return CheckMediaXmlTable();
        }

        [HttpPost]
        public bool FixMembersXmlTable()
        {
            // fixme - should be done in the XmlCache, not in the service
            var svc = (MemberService)Services.MemberService;
            svc.RebuildMemberXml();
            return CheckMembersXmlTable();
        }

        // fixme - what's below is ... should belong to XmlCache ONLY

        [HttpGet]
        public bool CheckContentXmlTable()
        {
            var totalPublished = Services.ContentService.CountPublished();
            var subQuery = new Sql()
                .Select("Count(DISTINCT cmsContentXml.nodeId)")
                .From<ContentXmlDto>()
                .InnerJoin<DocumentDto>()
                .On<DocumentDto, ContentXmlDto>(left => left.NodeId, right => right.NodeId);
            var totalXml = ApplicationContext.DatabaseContext.Database.ExecuteScalar<int>(subQuery);

            return totalXml == totalPublished;
        }
        
        [HttpGet]
        public bool CheckMediaXmlTable()
        {
            var total = Services.MediaService.Count();
            var mediaObjectType = Guid.Parse(Constants.ObjectTypes.Media);
            var subQuery = new Sql()
                .Select("Count(*)")
                .From<ContentXmlDto>()
                .InnerJoin<NodeDto>()
                .On<ContentXmlDto, NodeDto>(left => left.NodeId, right => right.NodeId)
                .Where<NodeDto>(dto => dto.NodeObjectType == mediaObjectType);
            var totalXml = ApplicationContext.DatabaseContext.Database.ExecuteScalar<int>(subQuery);

            return totalXml == total;
        }

        [HttpGet]
        public bool CheckMembersXmlTable()
        {
            var total = Services.MemberService.Count();
            var memberObjectType = Guid.Parse(Constants.ObjectTypes.Member);
            var subQuery = new Sql()
                .Select("Count(*)")
                .From<ContentXmlDto>()
                .InnerJoin<NodeDto>()
                .On<ContentXmlDto, NodeDto>(left => left.NodeId, right => right.NodeId)
                .Where<NodeDto>(dto => dto.NodeObjectType == memberObjectType);
            var totalXml = ApplicationContext.DatabaseContext.Database.ExecuteScalar<int>(subQuery);

            return totalXml == total;
        }

        
    }
}