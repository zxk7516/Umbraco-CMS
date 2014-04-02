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
    class PublishedContent : PublishedContentBase, IPublishedContentOrMedia
    {
        private readonly IContent _inner;
        private readonly Lazy<string> _lazyUrlName;
        private readonly Lazy<string> _lazyCreatorName;
        private readonly Lazy<string> _lazyWriterName;
        private readonly PublishedContentType _contentType;
        private readonly IPublishedProperty[] _properties;
        private readonly bool _isPreviewing;
        private readonly PublishedContentCache _cache;

        public PublishedContent(IContent inner, PublishedContentCache cache, bool isPreviewing)
        {
            if (inner == null)
                throw new NullReferenceException("inner");

            _inner = inner;
            _cache = cache;
            _isPreviewing = isPreviewing;

            _lazyUrlName = new Lazy<string>(() => _inner.GetUrlSegment().ToLower());
            _lazyCreatorName = new Lazy<string>(() => _inner.GetCreatorProfile().Name);
            _lazyWriterName = new Lazy<string>(() => _inner.GetWriterProfile().Name);

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
            get { return PublishedItemType.Content; }
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
            get { return _inner.Template == null ? 0 : _inner.Template.Id; }
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
            get { return _inner.WriterId; }
        }

        public override string WriterName
        {
            get { return _lazyWriterName.Value; }
        }

        public override bool IsDraft
        {
            get { return _inner.Published == false; }
        }

        public override IPublishedContent Parent
        {
            get
            {
                // use the cache to get the right (published or draft) version
                return _cache.GetById(_isPreviewing, _inner.ParentId);
            }
        }

        public override IEnumerable<IPublishedContent> Children
        {
            get
            {
                // _inner.Children() contains all children, including unpublished one, as draft when possible
                // that's OK if previewing, else we have to get the published items through the cache
                return _inner
                    .Children()
                    .Select(x => _isPreviewing
                                     ? PublishedContentModelFactory.CreateModel(new PublishedContent(x, _cache, true))
                                     : _cache.GetById(false, x.Id))
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

        // contains every child, published or not!
        IList<int> IPublishedContentOrMedia.ChildIds { get { return _inner.Children().OrderBy(c => c.SortOrder).Select(c => c.Id).ToList(); } }

        INavigableContentType IPublishedContentOrMedia.NavigableContentType { get { return new Navigable.NavigableContentType(_contentType); } }

        bool IPublishedContentOrMedia.IsPreviewing { get { return _isPreviewing; } }

        #endregion
    }
}
