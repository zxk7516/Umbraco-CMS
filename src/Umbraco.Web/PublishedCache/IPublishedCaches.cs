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
        IPublishedContentCache ContentCache { get; }
        IPublishedMediaCache MediaCache { get; }

        void Resync();
    }
}
