using System;
using System.Collections.Generic;
using System.Linq;
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

        public ContentSourceDto GetContentSource(int id)
        {
            return _databaseContext.Database.Fetch<ContentSourceDto>(new Sql(@"SELECT
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
        }

        public ContentSourceDto GetMediaSource(int id)
        {
            // should be only 1 version for medias

            return _databaseContext.Database.Fetch<ContentSourceDto>(new Sql(@"SELECT
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
        }

        public IEnumerable<ContentSourceDto> GetAllContentSources()
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
", new { objType = ContentObjectType }));
        }

        public IEnumerable<ContentSourceDto> GetAllMediaSources()
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
", new { objType = MediaObjectType }));
        }

        public IEnumerable<ContentSourceDto> GetBranchContentSources(int id)
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
", new { objType = ContentObjectType, /*id =*/ id }));
        }

        public IEnumerable<ContentSourceDto> GetBranchMediaSources(int id)
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
", new { objType = MediaObjectType, /*id =*/ id }));
        }

        public IEnumerable<ContentSourceDto> GetTypeContentSources(IEnumerable<int> ids)
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
WHERE n.nodeObjectType=@objType AND cmsContent.contentType=@ids
ORDER BY n.level, n.sortOrder
", new { objType = ContentObjectType, /*id =*/ ids }));
        }

        public IEnumerable<ContentSourceDto> GetTypeMediaSources(IEnumerable<int> ids)
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
WHERE n.nodeObjectType=@objType AND cmsContent.contentType=@ids
ORDER BY n.level, n.sortOrder
", new { objType = MediaObjectType, /*id =*/ ids }));
        }
    }
}
