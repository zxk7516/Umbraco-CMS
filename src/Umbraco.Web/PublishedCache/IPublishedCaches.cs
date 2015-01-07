using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Umbraco.Web.PublishedCache
{
    /// <summary>
    /// Provides caches (content and media).
    /// </summary>
    /// <remarks>Groups caches that _may_ be related.</remarks>
    public interface IPublishedCaches
    {
        /// <summary>
        /// Gets the <see cref="IPublishedContentCache"/>.
        /// </summary>
        IPublishedContentCache ContentCache { get; }

        /// <summary>
        /// Gets the <see cref="IPublishedMediaCache"/>.
        /// </summary>
        IPublishedMediaCache MediaCache { get; }

        /// <summary>
        /// Gets the <see cref="IPublishedMemberCache"/>.
        /// </summary>
        IPublishedMemberCache MemberCache { get; }

        /// <summary>
        /// Resynchronizes caches with their corresponding repositories.
        /// </summary>
        /// <remarks>
        /// <para>Caches in an IPublishedCaches should be immutable ie should not reflect changes
        /// made to their corresponding repositories, except for changes made within the context of the current
        /// IPublishedCaches.</para>
        /// <para>Ie other requests should not impact caches, but if the current request causes some changes,
        /// then these changes should be reflected in its current caches.</para>
        /// <para>NOTE that the XML cache does not exactly guarantees this, as it will effectively fully
        /// resynchronize any time the current request causes some changes.</para>
        /// <para>This method forces the caches to resync with the repositories.</para>
        /// </remarks>
        void Resync();
    }
}
