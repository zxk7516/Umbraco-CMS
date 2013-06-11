using System.Collections.Generic;
using Umbraco.Core;
using Umbraco.Core.Persistence;

namespace Umbraco.Web.PublishedCache.NuCache.DataSource
{
    class Database
    {
        // fixme - this is completely experimental

        public IEnumerable<VersionsPoco> GetVersions()
        {
            var db = ApplicationContext.Current.DatabaseContext.Database;
            var pocos = db.Fetch<VersionsPoco>(new Sql(@"select 
    node.id Id, node.parentID ParentId,
	docPublished.versionId PublishedVersionId, docPublished.updateDate PublishedUpdateDate,
	docDraft.versionId DraftVersionId, docDraft.updateDate DraftUpdateDate
from umbracoNode node
    left join cmsDocument docPublished on node.id = docPublished.nodeId and docPublished.published = 1
    left join cmsDocument docDraft on node.id = docDraft.nodeId and docDraft.published = 0 and docDraft.newest = 1
where
	node.nodeObjectType = 'C66BA18E-EAF3-4CFF-8A22-41B16D66A972'
	and node.trashed = 0
order by node.id;
"));
            return pocos;
        }

        public IEnumerable<ContentData> GetContent()
        {
            var db = ApplicationContext.Current.DatabaseContext.Database;
            var pocos = db.Fetch<ContentData>(new Sql(@"select
    node.id Id, node.parentId ParentId, node.level Level, node.sortOrder SortOrder,
    node.text Text,
    node.createDate CreateDate, node.nodeUser CreateUserId, 
    doc.versionId VersionId,
    doc.text VersionText,
    doc.updateDate VersionDate, doc.documentUser VersionUserId,
    doc.templateId TemplateId,
    doc.Published Published
from umbracoNode node
    join cmsDocument doc on node.id = doc.nodeId
where
	node.nodeObjectType = 'C66BA18E-EAF3-4CFF-8A22-41B16D66A972'
	and (doc.newest = 1 or doc.published = 1)
	and node.trashed = 0
order by node.id, doc.published;
"));

            // pdata.propertyTypeId PropertyTypeId,
            // but fetching the alias means more data....
            var props = db.Fetch<PropertyPoco>(new Sql(@"select 
    pdata.contentNodeId NodeId, pdata.versionId VersionId,
    ptype.Alias Alias,
    pdata.dataInt ValueInt, pdata.dataDate ValueDateTime, pdata.dataNvarchar ValueVarchar, pdata.dataNtext ValueText
from umbracoNode node
    join cmsDocument doc on node.id = doc.nodeId
    join cmsPropertyData pdata on doc.versionId = pdata.versionId
    join cmsPropertyType ptype on pdata.propertyTypeId = ptype.id
where
	node.nodeObjectType = 'C66BA18E-EAF3-4CFF-8A22-41B16D66A972'
	and (doc.newest = 1 or doc.published = 1)
	and node.trashed = 0
order by node.id, doc.published;
"));

            using (var transaction = db.GetTransaction()) // can't tell the isolation ?!
            {
                transaction.Complete();
            }

            var propsEnumerator = props.GetEnumerator();
            propsEnumerator.MoveNext();

            foreach (var poco in pocos)
            {
                //poco.PropertiesEnumerator = propsEnumerator;
                yield return poco;
            }
        }
    }
}
