using System;

namespace Umbraco.Web.PublishedCache.NuCache.DataSource
{
    class VersionsPoco
    {
        public int Id { get; set; }
        public int ParentId { get; set; }

        public Guid? PublishedVersionId { get; set; }
        public DateTime? PublishedUpdateDate { get; set; }
        public Guid? DraftVersionId { get; set; }
        public DateTime? DraftUpdateDate { get; set; }

        public bool HasPublishedVersion { get { return PublishedVersionId.HasValue; } }
        public bool HasDraftVersion { get { return DraftVersionId.HasValue; } }
    }
}
