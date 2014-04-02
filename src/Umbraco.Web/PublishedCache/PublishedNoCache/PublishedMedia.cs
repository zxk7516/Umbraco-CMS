using System;
using System.Collections.Generic;
using System.Linq;
using Umbraco.Core;
using Umbraco.Core.Models.PublishedContent;
using Umbraco.Core.Strings;
using Umbraco.Core.Models;
using Umbraco.Core.Xml.XPath;
using Umbraco.Web.Models;

namespace Umbraco.Web.PublishedCache.PublishedNoCache
{
    class PublishedMedia : PublishedContentBase, IPublishedContentOrMedia
    {
        private readonly IMedia _inner;
        private readonly Lazy<string> _lazyUrlName;
        private readonly Lazy<string> _lazyCreatorName;
        private readonly PublishedContentType _contentType;
        private readonly IPublishedProperty[] _properties;
        private readonly bool _isPreviewing;
        private readonly PublishedMediaCache _cache;

        public PublishedMedia(IMedia inner, PublishedMediaCache cache, bool isPreviewing)
        {
            if (inner == null)
                throw new NullReferenceException("inner");

            _inner = inner;
            _cache = cache;
            _isPreviewing = isPreviewing;

            _lazyUrlName = new Lazy<string>(() => _inner.GetUrlSegment().ToLower());
            _lazyCreatorName = new Lazy<string>(() => _inner.GetCreatorProfile().Name);

            _contentType = new PublishedContentType(_inner.ContentType);

            _properties = Models.PublishedProperty.MapProperties(_contentType.PropertyTypes, _inner.Properties,
                (t, p, v) => new PublishedProperty(t, this, v))
                .ToArray();
        }

        #region IPublishedContent

        public override int Id
        {
            get { return _inner.Id; }
        }

        public override int DocumentTypeId
        {
            get { return _inner.ContentTypeId; }
        }

        public override string DocumentTypeAlias
        {
            get { return _inner.ContentType.Alias; }
        }

        public override PublishedItemType ItemType
        {
            get { return PublishedItemType.Media; }
        }

        public override string Name
        {
            get { return _inner.Name; }
        }

        public override int Level
        {
            get { return _inner.Level; }
        }

        public override string Path
        {
            get { return _inner.Path; }
        }

        public override int SortOrder
        {
            get { return _inner.SortOrder; }
        }

        public override Guid Version
        {
            get { return _inner.Version; }
        }

        public override int TemplateId
        {
            get { return -1; }
        }

        public override string UrlName
        {
            get { return _lazyUrlName.Value; }
        }

        public override DateTime CreateDate
        {
            get { return _inner.CreateDate; }
        }

        public override DateTime UpdateDate
        {
            get { return _inner.UpdateDate; }
        }

        public override int CreatorId
        {
            get { return _inner.CreatorId; }
        }

        public override string CreatorName
        {
            get { return _lazyCreatorName.Value; }
        }

        public override int WriterId
        {
            // hack for U4-1132
            get { return _inner.CreatorId; }
        }

        public override string WriterName
        {
            // hack for U4-1132
            get { return _lazyCreatorName.Value; }
        }

        public override bool IsDraft
        {
            get { return false; }
        }

        public override IPublishedContent Parent
        {
            get
            {
                // use the cache to get a content that know if previewing or not
                return _cache.GetById(_isPreviewing, _inner.ParentId);
            }
        }

        public override IEnumerable<IPublishedContent> Children
        {
            get
            {
                // _inner.Children() contains all children, but for medias that's not an issue
                // that's OK if previewing, else we have to get the published items through the cache
                return _inner
                    .Children()
                    .Select(x => PublishedContentModelFactory.CreateModel(new PublishedMedia(x, _cache, _isPreviewing)))
                    .Where(x => x != null)
                    .OrderBy(x => x.SortOrder);
            }
        }

        public override ICollection<IPublishedProperty> Properties
        {
            get { return _properties; }
        }

        public override IPublishedProperty GetProperty(string alias)
        {
            return _properties.FirstOrDefault(x => x.PropertyTypeAlias.InvariantEquals(alias));
        }

        public override PublishedContentType ContentType
        {
            get { return _contentType; }
        }

        #endregion

        #region Internal

        // note: this is the "no cache" published cache so nothing is cached here
        // which means that it is going to be a disaster, performance-wise

        IPublishedProperty[] IPublishedContentOrMedia.PropertiesArray { get { return _properties; } }

        int IPublishedContentOrMedia.ParentId { get { return _inner.ParentId; } }

        IList<int> IPublishedContentOrMedia.ChildIds { get { return _inner.Children().OrderBy(c => c.SortOrder).Select(c => c.Id).ToList(); } }

        INavigableContentType IPublishedContentOrMedia.NavigableContentType { get { return new Navigable.NavigableContentType(_contentType); } }

        bool IPublishedContentOrMedia.IsPreviewing { get { return _isPreviewing; } }

        #endregion
    }
}
