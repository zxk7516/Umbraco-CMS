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
        // special ctor with no content data - for members
        public ContentNode(int id, Guid uid, PublishedContentType contentType,
            int level, string path, int sortOrder,
            int parentContentId,
            DateTime createDate, int creatorId)
        {
            Id = id;
            Uid = uid;
            ContentTypeId = contentType.Id;
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
            : this(id, uid, contentType.Id, level, path, sortOrder, parentContentId, createDate, creatorId, draftData, publishedData)
        {
            SetContentType(contentType);
        }

        // 2-phases ctor, must set the content type later
        public ContentNode(int id, Guid uid, int contentTypeId,
            int level, string path, int sortOrder,
            int parentContentId,
            DateTime createDate, int creatorId,
            ContentData draftData, ContentData publishedData)
        {
            Id = id;
            Uid = uid;
            ContentTypeId = contentTypeId;
            Level = level;
            Path = path;
            SortOrder = sortOrder;
            ParentContentId = parentContentId;
            CreateDate = createDate;
            CreatorId = creatorId;

            ChildContentIds = new List<int>();

            if (draftData == null && publishedData == null)
                throw new ArgumentException("Both draftData and publishedData cannot be null at the same time.");

            _draftData = draftData;
            _publishedData = publishedData;
        }

        // two-phase ctor
        public void SetContentType(PublishedContentType contentType)
        {
            ContentType = contentType;

            if (_draftData != null)
                Draft = new PublishedContent(this, _draftData).CreateModel();
            if (_publishedData != null)
                Published = new PublishedContent(this, _publishedData).CreateModel();
        }

        // clone parent
        private ContentNode(ContentNode origin)
        {
            // everything is the same, except for the child items
            // list which is a clone of the original list

            Id = origin.Id;
            Uid = origin.Uid;
            ContentTypeId = origin.ContentTypeId;
            ContentType = origin.ContentType;
            Level = origin.Level;
            Path = origin.Path;
            SortOrder = origin.SortOrder;
            ParentContentId = origin.ParentContentId;
            CreateDate = origin.CreateDate;
            CreatorId = origin.CreatorId;

            _draftData = origin._draftData;
            _publishedData = origin._publishedData;

            var originDraft = origin.Draft == null ? null : PublishedContent.UnwrapIPublishedContent(origin.Draft);
            var originPublished = origin.Published == null ? null : PublishedContent.UnwrapIPublishedContent(origin.Published);

            Draft = originDraft == null ? null : new PublishedContent(this, originDraft).CreateModel();
            Published = originPublished == null ? null : new PublishedContent(this, originPublished).CreateModel();

            ChildContentIds = new List<int>(origin.ChildContentIds);
        }

        // clone with new content type
        public ContentNode(ContentNode origin, PublishedContentType contentType)
        {
            Id = origin.Id;
            Uid = origin.Uid;
            ContentType = origin.ContentType;
            Level = origin.Level;
            Path = origin.Path;
            SortOrder = origin.SortOrder;
            ParentContentId = origin.ParentContentId;
            CreateDate = origin.CreateDate;
            CreatorId = origin.CreatorId;

            ChildContentIds = origin.ChildContentIds;

            _draftData = origin._draftData;
            _publishedData = origin._publishedData;

            SetContentType(contentType);
        }

        // everything that is common to both draft and published versions
        public readonly int Id;
        public readonly Guid Uid;
        public readonly int ContentTypeId;
        public PublishedContentType ContentType;
        public readonly int Level;
        public readonly string Path;
        public readonly int SortOrder;
        public readonly int ParentContentId;
        public List<int> ChildContentIds;
        public readonly DateTime CreateDate;
        public readonly int CreatorId;

        // draft and published version (either can be null, but not both)
        private readonly ContentData _draftData;
        private readonly ContentData _publishedData;
        public IPublishedContent Draft;
        public IPublishedContent Published;

        public ContentNode CloneParent()
        {
            return new ContentNode(this);
        }
    }
}
