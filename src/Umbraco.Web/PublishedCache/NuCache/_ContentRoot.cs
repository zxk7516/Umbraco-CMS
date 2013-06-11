using System;
using System.Collections.Generic;
using System.Linq;
using Umbraco.Core.Models;

namespace Umbraco.Web.PublishedCache.NuCache
{
    /// <summary>
    /// Represents the root content drop of a bucket.
    /// </summary>
    // internal for unit tests
    internal class ContentRoot : ContentDrop
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ContentRoot"/> class.
        /// </summary>
        public ContentRoot()
        {
            EnsureInitialized(); // must have a ContentStore right from the beginning.
        }

        #region Drops

        public override void Detach()
        {
            throw new InvalidOperationException("Root drop cannot be detached.");
        }

        #endregion

        #region HasContent

        /// <summary>
        /// Gets a value indicating whether the content drop has content.
        /// </summary>
        public override bool HasContent
        {
            get { return ContentStore.IsEmpty == false; }
        }

        /// <summary>
        /// Gets a value indicating whether the drop has local content.
        /// </summary>
        public override bool HasLocalContent
        {
            get { return ContentStore.IsEmpty == false; }
        }

        #endregion

        #region Getters

        /// <summary>
        /// Gets a content identified by its identifier.
        /// </summary>
        /// <param name="id">The content identifier.</param>
        /// <returns>The content identified by the identifier, or null.</returns>
        public override IPublishedContent Get(int id)
        {
            IPublishedContent content;
            return ContentStore.TryGetValue(id, out content) ? content : null;
        }

        /// <summary>
        /// Gets all content satisfying a condition.
        /// </summary>
        /// <param name="predicate">A predicate.</param>
        /// <returns>All content satisfying the specified condition.</returns>
        public override IEnumerable<IPublishedContent> GetAll(Func<IPublishedContent, bool> predicate)
        {
            return predicate == null ? ContentStore.Values : ContentStore.Values.Where(predicate);
        }

        #endregion

        #region Setters

        /// <summary>
        /// Sets a content.
        /// </summary>
        /// <param name="content">The content to set.</param>
        /// <remarks>Only the <see cref="ContentBucket"/> can set a drop's content.</remarks>
        public override void Set(IPublishedContent content)
        {
            if (content == null)
                throw new ArgumentNullException("content");

            ContentStore[content.Id] = content;
        }

        /// <summary>
        /// Removes a content identified by its identifier.
        /// </summary>
        /// <param name="id">The content identifier.</param>
        /// <remarks>Only the <see cref="ContentBucket"/> can remove content from a drop.</remarks>
        public override void Remove(int id)
        {
            IPublishedContent content;
            ContentStore.TryRemove(id, out content);
        }

        #endregion

        #region Infrastructure

        // root has no local content - that one should never get called
        public override bool TryGetNonRoot(int id, out IPublishedContent content)
        {
            content = null;
            return false;
        }

        // root has no local content - that one should never get called
        public override IEnumerable<IPublishedContent> GetNonRoot(Func<IPublishedContent, bool> predicate)
        {
            return Enumerable.Empty<IPublishedContent>();
        }

        #endregion
    }
}
