using System.Collections.Generic;
using System.Linq;
using Umbraco.Core.Logging;
using Umbraco.Core.Models;
using Umbraco.Core.Models.EntityBase;
using Umbraco.Core.Persistence;
using Umbraco.Core.Persistence.UnitOfWork;

namespace Umbraco.Core.Services
{
    public class ContentTypeServiceBase : RepositoryService
    {
        public ContentTypeServiceBase(IDatabaseUnitOfWorkProvider provider, RepositoryFactory repositoryFactory, ILogger logger)
            : base(provider, repositoryFactory, logger)
        { }

        // this is called after some content types are changed, and is used to determine which content types
        // are impacted by the changes in a way that needs to be notified to the content service -- including
        // content types that were not directly changed but may be impacted due to compositions.
        //
        // need to notify of everything that would change the serialized view of any content.
        //
        // things that need to be notified:
        // - renaming a content type
        // - renaming a property type
        // - removing a property type
        //
        // things don't need to be notified:
        // - adding a property type to a content type (missing in serialized view = default value anyway)

        // FIXME - what about composition changes?

        /// <summary>
        /// Determines which content types are impacted by content types changes, in a way that needs
        /// to be notified to the content service.
        /// </summary>
        /// <param name="contentTypes">The changed content types.</param>
        /// <returns>The impacted content types.</returns>
        internal IEnumerable<IContentTypeBase> GetContentTypesToNotify(params IContentTypeBase[] contentTypes)
        {
            // hash set handles duplicates
            var notify = new HashSet<IContentTypeBase>(new DelegateEqualityComparer<IContentTypeBase>(
                (x, y) => x.Id == y.Id,
                x => x.Id.GetHashCode()));

            foreach (var contentType in contentTypes)
            {
                var dirty = contentType as IRememberBeingDirty;
                if (dirty == null) continue;

                // skip new content types
                var isNewContentType = dirty.WasPropertyDirty("HasIdentity");
                if (isNewContentType) continue;

                // existing property alias change?
                var hasAnyPropertyChangedAlias = contentType.PropertyTypes.Any(propertyType =>
                    {
                        var dirtyProperty = propertyType as IRememberBeingDirty;
                        if (dirtyProperty == null) return false;

                        // skip new properties
                        var isNewProperty = dirtyProperty.WasPropertyDirty("HasIdentity");
                        if (isNewProperty) return false;

                        // alias change?
                        var hasPropertyAliasBeenChanged = dirtyProperty.WasPropertyDirty("Alias");
                        return hasPropertyAliasBeenChanged;
                    });

                // removed properties?
                var hasAnyPropertyBeenRemoved = dirty.WasPropertyDirty("HasPropertyTypeBeenRemoved");

                // alias change?
                var hasAliasBeenChanged = dirty.WasPropertyDirty("Alias");

                // skip if nothing changed
                if ((hasAliasBeenChanged || hasAnyPropertyBeenRemoved || hasAnyPropertyChangedAlias) == false) continue;

                // alias changes impact only the current content type whereas property changes impact descendants
                if (hasAnyPropertyBeenRemoved || hasAnyPropertyChangedAlias)
                {
                    foreach (var c in contentType.DescendantsAndSelf())
                        notify.Add(c);
                }
                else
                {
                    notify.Add(contentType);
                }
            }

            return notify;
        }
    }
}