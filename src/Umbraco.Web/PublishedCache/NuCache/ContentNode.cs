using System;
using System.Collections.Generic;
using Umbraco.Core.Models;
using Umbraco.Core.Models.PublishedContent;
using Umbraco.Web.PublishedCache.NuCache.DataSource;

namespace Umbraco.Web.PublishedCache.NuCache
{
    // represents a content "node" ie a pair of draft + published versions
    // internal, never exposed, to be accessed from ContentStore (only!)
    internal class ContentNode
    {
        // fixme - special for member?
        public ContentNode(int id, Guid uid, PublishedContentType contentType,
            int level, string path, int sortOrder,
            int parentContentId,
            DateTime createDate, int creatorId)
        {
            Id = id;
            Uid = uid;
            ContentType = contentType;
            Level = level;
            Path = path;
            SortOrder = sortOrder;
            ParentContentId = parentContentId;
            CreateDate = createDate;
            CreatorId = creatorId;

            ChildContentIds = new List<int>();
        }

        public ContentNode(int id, Guid uid, PublishedContentType contentType,
            int level, string path, int sortOrder,
            int parentContentId,
            DateTime createDate, int creatorId,
            ContentData draftData, ContentData publishedData)
        {
            Id = id;
            Uid = uid;
            ContentType = contentType;
            Level = level;
            Path = path;
            SortOrder = sortOrder;
            ParentContentId = parentContentId;
            CreateDate = createDate;
            CreatorId = creatorId;

            ChildContentIds = new List<int>();

            if (draftData == null && publishedData == null)
                throw new ArgumentException("Both draftData and publishedData cannot be null at the same time.");

            if (draftData != null)
                Draft = new PublishedContent(this, draftData).CreateModel();
            if (publishedData != null)
                Published = new PublishedContent(this, publishedData).CreateModel();
        }

        // clone parent
        private ContentNode(ContentNode origin)
        {
            // everything is the same, except for the child items
            // list which is a clone of the original list

            Id = origin.Id;
            Uid = origin.Uid;
            ContentType = origin.ContentType;
            Level = origin.Level;
            Path = origin.Path;
            SortOrder = origin.SortOrder;
            ParentContentId = origin.ParentContentId;
            CreateDate = origin.CreateDate;
            CreatorId = origin.CreatorId;

            var originDraft = origin.Draft == null ? null : PublishedContent.UnwrapIPublishedContent(origin.Draft);
            var originPublished = origin.Published == null ? null : PublishedContent.UnwrapIPublishedContent(origin.Published);

            Draft = originDraft == null ? null : new PublishedContent(this, originDraft).CreateModel();
            Published = originPublished == null ? null : new PublishedContent(this, originPublished).CreateModel();

            ChildContentIds = new List<int>(origin.ChildContentIds);
        }

        // everything that is common to both draft and published versions
        public int Id;
        public Guid Uid;
        public PublishedContentType ContentType;
        public int Level;
        public string Path;
        public int SortOrder;
        public int ParentContentId;
        public List<int> ChildContentIds;
        public DateTime CreateDate;
        public int CreatorId;

        // draft and published version (either can be null, but not both)
        public IPublishedContent Draft;
        public IPublishedContent Published;

        public ContentNode CloneParent()
        {
            return new ContentNode(this);
        }
    }
}
