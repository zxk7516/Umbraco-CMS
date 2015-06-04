using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Umbraco.Core;
using Umbraco.Core.Persistence;

namespace Umbraco.Web.PublishedCache.NuCache.DataSource
{
    // provides efficient database access for NuCache
    class Database
    {
        // though these should be system constants too!
        private readonly static Guid ContentObjectType = Guid.Parse(Constants.ObjectTypes.Document);
        private readonly static Guid MediaObjectType = Guid.Parse(Constants.ObjectTypes.Media);
        //private readonly static Guid MemberObjectType = Guid.Parse(Constants.ObjectTypes.Member);

        private readonly DatabaseContext _databaseContext;

        public Database(DatabaseContext databaseContext)
        {
            _databaseContext = databaseContext;
        }

        public ContentNodeKit GetContentSource(int id)
        {
            var dto = _databaseContext.Database.Fetch<ContentSourceDto>(new Sql(@"SELECT
n.id Id, n.uniqueId Uid,
cmsContent.contentType ContentTypeId,
n.level Level, n.path Path, n.sortOrder SortOrder, n.parentId ParentId,
n.createDate CreateDate, n.nodeUser CreatorId,
docDraft.text DraftName, docDraft.versionId DraftVersion, docDraft.updateDate DraftVersionDate, docDraft.documentUser DraftWriterId, docDraft.templateId DraftTemplateId,
nuDraft.data DraftData,
docPub.text PubName, docPub.versionId PubVersion, docPub.updateDate PubVersionDate, docPub.documentUser PubWriterId, docPub.templateId PubTemplateId,
nuPub.data PubData
FROM umbracoNode n
JOIN cmsContent ON (cmsContent.nodeId=n.id)
LEFT JOIN cmsDocument docDraft ON (docDraft.nodeId=n.id AND docDraft.newest=1 AND docDraft.published=0)
LEFT JOIN cmsDocument docPub ON (docPub.nodeId=n.id AND docPub.published=1)
LEFT JOIN cmsContentNu nuDraft ON (nuDraft.nodeId=n.id AND nuDraft.published=0)
LEFT JOIN cmsContentNu nuPub ON (nuPub.nodeId=n.id AND nuPub.published=1)
WHERE n.nodeObjectType=@objType AND n.id=@id
", new { objType = ContentObjectType, /*id =*/ id })).FirstOrDefault();
            return dto == null ? new ContentNodeKit() : CreateContentNodeKit(dto);
        }

        public ContentNodeKit GetMediaSource(int id)
        {
            // should be only 1 version for medias

            var dto = _databaseContext.Database.Fetch<ContentSourceDto>(new Sql(@"SELECT
n.id Id, n.uniqueId Uid,
cmsContent.contentType ContentTypeId,
n.level Level, n.path Path, n.sortOrder SortOrder, n.parentId ParentId,
n.createDate CreateDate, n.nodeUser CreatorId,
n.text PubName, ver.versionId PubVersion, ver.versionDate PubVersionDate,
nuPub.data PubData
FROM umbracoNode n
JOIN cmsContent ON (cmsContent.nodeId=n.id)
JOIN cmsContentVersion ver ON (ver.contentId=n.id)
LEFT JOIN cmsContentNu nuPub ON (nuPub.nodeId=n.id AND nuPub.published=1)
WHERE n.nodeObjectType=@objType AND n.id=@id
", new { objType = MediaObjectType, /*id =*/ id })).FirstOrDefault();
            return dto == null ? new ContentNodeKit() : CreateMediaNodeKit(dto);
        }

        // we want arrays, we want them all loaded, not an enumerable

        public IEnumerable<ContentNodeKit> GetAllContentSources()
        {
            return _databaseContext.Database.Query<ContentSourceDto>(new Sql(@"SELECT
n.id Id, n.uniqueId Uid,
cmsContent.contentType ContentTypeId,
n.level Level, n.path Path, n.sortOrder SortOrder, n.parentId ParentId,
n.createDate CreateDate, n.nodeUser CreatorId,
docDraft.text DraftName, docDraft.versionId DraftVersion, docDraft.updateDate DraftVersionDate, docDraft.documentUser DraftWriterId, docDraft.templateId DraftTemplateId,
nuDraft.data DraftData,
docPub.text PubName, docPub.versionId PubVersion, docPub.updateDate PubVersionDate, docPub.documentUser PubWriterId, docPub.templateId PubTemplateId,
nuPub.data PubData
FROM umbracoNode n
JOIN cmsContent ON (cmsContent.nodeId=n.id)
LEFT JOIN cmsDocument docDraft ON (docDraft.nodeId=n.id AND docDraft.newest=1 AND docDraft.published=0)
LEFT JOIN cmsDocument docPub ON (docPub.nodeId=n.id AND docPub.published=1)
LEFT JOIN cmsContentNu nuDraft ON (nuDraft.nodeId=n.id AND nuDraft.published=0)
LEFT JOIN cmsContentNu nuPub ON (nuPub.nodeId=n.id AND nuPub.published=1)
WHERE n.nodeObjectType=@objType
ORDER BY n.level, n.sortOrder
", new { objType = ContentObjectType })).Select(CreateContentNodeKit);
        }

        public IEnumerable<ContentNodeKit> GetAllMediaSources()
        {
            // should be only 1 version for medias

            return _databaseContext.Database.Query<ContentSourceDto>(new Sql(@"SELECT
n.id Id, n.uniqueId Uid,
cmsContent.contentType ContentTypeId,
n.level Level, n.path Path, n.sortOrder SortOrder, n.parentId ParentId,
n.createDate CreateDate, n.nodeUser CreatorId,
n.text PubName, ver.versionId PubVersion, ver.versionDate PubVersionDate,
nuPub.data PubData
FROM umbracoNode n
JOIN cmsContent ON (cmsContent.nodeId=n.id)
JOIN cmsContentVersion ver ON (ver.contentId=n.id)
LEFT JOIN cmsContentNu nuPub ON (nuPub.nodeId=n.id AND nuPub.published=1)
WHERE n.nodeObjectType=@objType
ORDER BY n.level, n.sortOrder
", new { objType = MediaObjectType })).Select(CreateMediaNodeKit);
        }

        public IEnumerable<ContentNodeKit> GetBranchContentSources(int id)
        {
            return _databaseContext.Database.Query<ContentSourceDto>(new Sql(@"SELECT
n.id Id, n.uniqueId Uid,
cmsContent.contentType ContentTypeId,
n.level Level, n.path Path, n.sortOrder SortOrder, n.parentId ParentId,
n.createDate CreateDate, n.nodeUser CreatorId,
docDraft.text DraftName, docDraft.versionId DraftVersion, docDraft.updateDate DraftVersionDate, docDraft.documentUser DraftWriterId, docDraft.templateId DraftTemplateId,
nuDraft.data DraftData,
docPub.text PubName, docPub.versionId PubVersion, docPub.updateDate PubVersionDate, docPub.documentUser PubWriterId, docPub.templateId PubTemplateId,
nuPub.data PubData
FROM umbracoNode n
JOIN umbracoNode x ON (n.id=x.id OR n.path LIKE " + _databaseContext.SqlSyntax.GetConcat("x.path", "',%'") + @")
JOIN cmsContent ON (cmsContent.nodeId=n.id)
LEFT JOIN cmsDocument docDraft ON (docDraft.nodeId=n.id AND docDraft.newest=1 AND docDraft.published=0)
LEFT JOIN cmsDocument docPub ON (docPub.nodeId=n.id AND docPub.published=1)
LEFT JOIN cmsContentNu nuDraft ON (nuDraft.nodeId=n.id AND nuDraft.published=0)
LEFT JOIN cmsContentNu nuPub ON (nuPub.nodeId=n.id AND nuPub.published=1)
WHERE n.nodeObjectType=@objType AND x.id=@id
ORDER BY n.level, n.sortOrder
", new { objType = ContentObjectType, /*id =*/ id })).Select(CreateContentNodeKit);
        }

        public IEnumerable<ContentNodeKit> GetBranchMediaSources(int id)
        {
            // should be only 1 version for medias

            return _databaseContext.Database.Query<ContentSourceDto>(new Sql(@"SELECT
n.id Id, n.uniqueId Uid,
cmsContent.contentType ContentTypeId,
n.level Level, n.path Path, n.sortOrder SortOrder, n.parentId ParentId,
n.createDate CreateDate, n.nodeUser CreatorId,
n.text PubName, ver.versionId PubVersion, ver.versionDate PubVersionDate,
nuPub.data PubData
FROM umbracoNode n
JOIN umbracoNode x ON (n.id=x.id OR n.path LIKE " + _databaseContext.SqlSyntax.GetConcat("x.path", "',%'") + @")
JOIN cmsContent ON (cmsContent.nodeId=n.id)
JOIN cmsContentVersion ver ON (ver.contentId=n.id)
LEFT JOIN cmsContentNu nuPub ON (nuPub.nodeId=n.id AND nuPub.published=1)
WHERE n.nodeObjectType=@objType AND x.id=@id
ORDER BY n.level, n.sortOrder
", new { objType = MediaObjectType, /*id =*/ id })).Select(CreateMediaNodeKit);
        }

        public IEnumerable<ContentNodeKit> GetTypeContentSources(IEnumerable<int> ids)
        {
            return _databaseContext.Database.Query<ContentSourceDto>(new Sql(@"SELECT
n.id Id, n.uniqueId Uid,
cmsContent.contentType ContentTypeId,
n.level Level, n.path Path, n.sortOrder SortOrder, n.parentId ParentId,
n.createDate CreateDate, n.nodeUser CreatorId,
docDraft.text DraftName, docDraft.versionId DraftVersion, docDraft.updateDate DraftVersionDate, docDraft.documentUser DraftWriterId, docDraft.templateId DraftTemplateId,
nuDraft.data DraftData,
docPub.text PubName, docPub.versionId PubVersion, docPub.updateDate PubVersionDate, docPub.documentUser PubWriterId, docPub.templateId PubTemplateId,
nuPub.data PubData
FROM umbracoNode n
JOIN cmsContent ON (cmsContent.nodeId=n.id)
LEFT JOIN cmsDocument docDraft ON (docDraft.nodeId=n.id AND docDraft.newest=1 AND docDraft.published=0)
LEFT JOIN cmsDocument docPub ON (docPub.nodeId=n.id AND docPub.published=1)
LEFT JOIN cmsContentNu nuDraft ON (nuDraft.nodeId=n.id AND nuDraft.published=0)
LEFT JOIN cmsContentNu nuPub ON (nuPub.nodeId=n.id AND nuPub.published=1)
WHERE n.nodeObjectType=@objType AND cmsContent.contentType IN (@ids)
ORDER BY n.level, n.sortOrder
", new { objType = ContentObjectType, /*id =*/ ids })).Select(CreateContentNodeKit);
        }

        public IEnumerable<ContentNodeKit> GetTypeMediaSources(IEnumerable<int> ids)
        {
            // should be only 1 version for medias

            return _databaseContext.Database.Query<ContentSourceDto>(new Sql(@"SELECT
n.id Id, n.uniqueId Uid,
cmsContent.contentType ContentTypeId,
n.level Level, n.path Path, n.sortOrder SortOrder, n.parentId ParentId,
n.createDate CreateDate, n.nodeUser CreatorId,
n.text PubName, ver.versionId PubVersion, ver.versionDate PubVersionDate,
nuPub.data PubData
FROM umbracoNode n
JOIN cmsContent ON (cmsContent.nodeId=n.id)
JOIN cmsContentVersion ver ON (ver.contentId=n.id)
LEFT JOIN cmsContentNu nuPub ON (nuPub.nodeId=n.id AND nuPub.published=1)
WHERE n.nodeObjectType=@objType AND cmsContent.contentType IN (@ids)
ORDER BY n.level, n.sortOrder
", new { objType = MediaObjectType, /*id =*/ ids })).Select(CreateMediaNodeKit);
        }

        private static ContentNodeKit CreateContentNodeKit(ContentSourceDto dto)
        {
            if (dto.DraftVersion != Guid.Empty && dto.DraftData == null)
                throw new Exception();

            ContentData d = null;
            ContentData p = null;

            if (dto.DraftVersion != Guid.Empty)
            {
                if (dto.DraftData == null)
                    throw new Exception("Missing cmsContentNu content for node " + dto.Id + ", consider rebuilding.");
                d = new ContentData
                {
                    Name = dto.DraftName,
                    Published = false,
                    TemplateId = dto.DraftTemplateId,
                    Version = dto.DraftVersion,
                    VersionDate = dto.DraftVersionDate,
                    WriterId = dto.DraftWriterId,
                    Properties = JsonConvert.DeserializeObject<Dictionary<string, object>>(dto.DraftData)
                };
            }

            if (dto.PubVersion != Guid.Empty)
            {
                if (dto.PubData == null)
                    throw new Exception("Missing cmsContentNu content for node " + dto.Id + ", consider rebuilding.");
                p = new ContentData
                {
                    Name = dto.PubName,
                    Published = true,
                    TemplateId = dto.PubTemplateId,
                    Version = dto.PubVersion,
                    VersionDate = dto.PubVersionDate,
                    WriterId = dto.PubWriterId,
                    Properties = JsonConvert.DeserializeObject<Dictionary<string, object>>(dto.PubData)
                };
            }

            var n = new ContentNode(dto.Id, dto.Uid,
                dto.Level, dto.Path, dto.SortOrder, dto.ParentId, dto.CreateDate, dto.CreatorId);

            var s = new ContentNodeKit
            {
                Node = n,
                ContentTypeId = dto.ContentTypeId,
                DraftData = d,
                PublishedData = p
            };

            return s;
        }

        private static ContentNodeKit CreateMediaNodeKit(ContentSourceDto dto)
        {
            if (dto.PubData == null)
                throw new Exception("No data for media " + dto.Id);

            var p = new ContentData
            {
                Name = dto.PubName,
                Published = true,
                TemplateId = -1,
                Version = dto.PubVersion,
                VersionDate = dto.PubVersionDate,
                WriterId = dto.CreatorId, // what-else?
                Properties = JsonConvert.DeserializeObject<Dictionary<string, object>>(dto.PubData)
            };

            var n = new ContentNode(dto.Id, dto.Uid,
                dto.Level, dto.Path, dto.SortOrder, dto.ParentId, dto.CreateDate, dto.CreatorId);

            var s = new ContentNodeKit
            {
                Node = n,
                ContentTypeId = dto.ContentTypeId,
                PublishedData = p
            };

            return s;
        }
    }
}
