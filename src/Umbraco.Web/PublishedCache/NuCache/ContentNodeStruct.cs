using Umbraco.Web.PublishedCache.NuCache.DataSource;

namespace Umbraco.Web.PublishedCache.NuCache
{
    struct ContentNodeStruct
    {
        public ContentNode Node;
        public int ContentTypeId;
        public ContentData DraftData;
        public ContentData PublishedData;

        public bool IsEmpty { get { return Node == null; } }
    }
}
