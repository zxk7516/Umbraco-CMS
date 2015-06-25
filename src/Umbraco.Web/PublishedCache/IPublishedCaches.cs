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
        /// Gets the <see cref="IDomainCache"/>.
        /// </summary>
        IDomainCache DomainCache { get; }
    }
}
