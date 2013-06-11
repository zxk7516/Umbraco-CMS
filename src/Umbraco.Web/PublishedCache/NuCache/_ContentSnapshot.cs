using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Umbraco.Web.PublishedCache.NuCache
{
    // represent a content snapshot ie a static view over contents and medias
    // there will be one per request or, more precisely, one per UmbracoContext
    class ContentSnapshot
    {
        private ContentDrop _publishedDrop;
        private ContentDrop _draftDrop;
        private ContentDrop _mediaDrop;

        //// transparently associates a snapshot to an UmbracoContext
        //static readonly ConditionalWeakTable<UmbracoContext, ContentSnapshot> Snapshots
        //    = new ConditionalWeakTable<UmbracoContext, ContentSnapshot>();

        // called by the conditional weak table -- must be public -- but don't use it
        public ContentSnapshot()
        { }

        internal ContentSnapshot(ContentDrop publishedDrop, ContentDrop draftDrop, ContentDrop mediaDrop)
        {
            // fixme - and whata about "Caches" providing all sorts of data caches?
            _publishedDrop = publishedDrop;
            _draftDrop = draftDrop;
            _mediaDrop = mediaDrop;
        }

        //// called by PublishedCaches.GetSnapshot
        //internal static ContentSnapshot GetOrCreateSnapshot(UmbracoContext context, PublishedCaches caches)
        //{
        //    // GetOrCreateValue() is thread-safe but EnsureInitialized() is not
        //    // this is because we assume that Umbraco will never ask PublishedCaches
        //    // to create both media and content cache simultaneously
        //    return Snapshots.GetOrCreateValue(context).EnsureInitialized(caches);
        //}

        //private ContentSnapshot EnsureInitialized(PublishedCaches caches)
        //{
        //    if (Caches == null)
        //    {
        //        Caches = caches;
        //        Caches.GetDrops(out _publishedDrop, out _draftDrop, out _mediaDrop);
        //    }
        //    return this;
        //}

        public Facade Caches { get; private set; }
        public ContentDrop PublishedDrop { get { return _publishedDrop; } }
        public ContentDrop DraftDrop { get { return _draftDrop; } }
        public ContentDrop MediaDrop { get { return _mediaDrop; } }

        // this is not thread-safe, you'd better know what you're doing!
        internal void Refresh()
        {
            // fixme
            throw new NotImplementedException();
            //Caches.GetDrops(out _publishedDrop, out _draftDrop, out _mediaDrop);
            ResetDataCaches();
        }

        #region Data caches

        // thread-safety: might invoke Caches.GetDataCaches more than once but will
        // get the same value anyway... and object refs read/write are atomic... so
        // this is a simple form of Lazy that should be thread-safe enough.

        // Caches.GetDataCaches may return the same cache to several snapshots as
        // long as the key drops are the same, so we're actually creating new caches
        // only when drops change. And only on-demand.

        // so one might think we're going to create too many caches, if the content
        // keeps changing... but remember there's a threshold on drops creation, so
        // if drops can't be created more than once per second, then we can't be
        // creating that many caches...

        // a cache that depends on (published-drop)
        // contains: recursive property objects (not values) for content, when not previewing
        //  because they may change when the published tree changes
        private ConcurrentDictionary<string, object> _publishedDataCache;
        public ConcurrentDictionary<string, object> PublishedDataCache
        {
            get
            {
                throw new NotImplementedException();
                //return _publishedDataCache ?? (_publishedDataCache
                //    = Caches.GetDataCaches(ConditionalCaches.WeakReferences.Create(_publishedDrop)));
            }
        }

        // a cache that depends on (media-drop)
        // contains: recursive property objects (not values) for media
        //  because they may change when the media tree changes
        private ConcurrentDictionary<string, object> _mediaDataCache;
        public ConcurrentDictionary<string, object> MediaDataCache
        {
            get
            {
                throw new NotImplementedException();
                //return _mediaDataCache ?? (_mediaDataCache
                //    = Caches.GetDataCaches(ConditionalCaches.WeakReferences.Create(_mediaDrop)));
            }
        }

        // a cache that depends on (published-drop, draft-drop)
        // contains: recursive property objects (not values) for content, when previewing
        //  because they may change when the published tree or the draft tree change
        private ConcurrentDictionary<string, object> _publishedDraftDataCache;
        public ConcurrentDictionary<string, object> PublishedDraftDataCache
        {
            get
            {
                throw new NotImplementedException();
                //return _publishedDraftDataCache ?? (_publishedDraftDataCache
                //    = Caches.GetDataCaches(ConditionalCaches.WeakReferences.Create(_publishedDrop, _draftDrop)));
            }
        }

        // a cache that depends on (published-drop, media-drop)
        // contains: property (converted) values for content and media, when not previewing
        //  because they may change when the published tree or the media tree changes
        private ConcurrentDictionary<string, object> _publishedMediaDataCache;
        public ConcurrentDictionary<string, object> PublishedMediaDataCache
        {
            get
            {
                throw new NotImplementedException();
                //return _publishedMediaDataCache ?? (_publishedMediaDataCache
                //    = Caches.GetDataCaches(ConditionalCaches.WeakReferences.Create(_publishedDrop, _mediaDrop)));
            }
        }

        // a cache that depends on (published-drop, draft-drop, media-drop)
        // contains: property (converted) values for content and media, when previewing
        //  because they may change when any tree changes
        private ConcurrentDictionary<string, object> _publishedDraftMediaDataCache;
        public ConcurrentDictionary<string, object> PublishedDraftMediaDataCache
        {
            get {
                throw new NotImplementedException();
                //return _publishedDraftMediaDataCache ?? (_publishedDraftMediaDataCache
                //    = Caches.GetDataCaches(ConditionalCaches.WeakReferences.Create(_publishedDrop, _draftDrop, _mediaDrop)));
            }
        }

        private void ResetDataCaches()
        {
            _publishedDataCache = null;
            _mediaDataCache = null;
            _publishedDraftDataCache = null;
            _publishedMediaDataCache = null;
            _publishedDraftMediaDataCache = null;
        }

        #endregion
    }
}
