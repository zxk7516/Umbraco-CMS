using System;
using System.Collections.Generic;
using System.Linq;
using Umbraco.Core.Events;
using Umbraco.Core.Logging;
using Umbraco.Core.Models;
using Umbraco.Core.Models.EntityBase;
using Umbraco.Core.Persistence;
using Umbraco.Core.Persistence.Repositories;
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

        internal IEnumerable<TreeChange<IContentTypeBase>> ComposeContentTypeChangesForDistributedCache(IEnumerable<IContentTypeBase> contentTypes)
        {
            var changes = new Dictionary<int, TreeChange<IContentTypeBase>>();

            // handle changes:
            // anything other that the alias =>  reload descendants
            // FIXME should be ANYTHING that changes the PUBLISHED CONTENT TYPE
            // ie ID, ALIAS, PROPERTIES
            // and for each property, ALIAS, DATATYPEID, PROPERTYEDITORALIAS
            //
            // FIXME NOT! want them all because ContentTypeCacheRefresher refreshers MORE than the published cache
            // FIXME reindexing all medias?!

            // just the alias => reload just that one

            foreach (var contentType in contentTypes)
            {
                var dirtyProperties = ((ContentType) contentType).GetDirtyProperties();
                var hasOtherThanAliasChanged = dirtyProperties.Any(x => x.InvariantEquals("Alias") == false);
                var hasAliasChanged = ((ContentType)contentType).IsPropertyDirty("Alias");

                if (hasOtherThanAliasChanged)
                {
                    TreeChange<IContentTypeBase> change;
                    if (changes.TryGetValue(contentType.Id, out change))
                        change.ChangeTypes = TreeChangeTypes.RefreshBranch;
                    else
                        changes.Add(contentType.Id, new TreeChange<IContentTypeBase>(contentType, TreeChangeTypes.RefreshBranch));
                }
                else if (hasAliasChanged)
                {
                    if (changes.ContainsKey(contentType.Id) == false)
                        changes.Add(contentType.Id, new TreeChange<IContentTypeBase>(contentType, TreeChangeTypes.RefreshNode));
                }
            }

            return changes.Values;
        }

        internal IEnumerable<IContentTypeBase> ComposeContentTypeChangesForTransactionEvent(IContentTypeBase contentType)
        {
            return ComposeContentTypeChangesForTransactionEvent(new[] {contentType});
        }

        internal IEnumerable<IContentTypeBase> ComposeContentTypeChangesForTransactionEvent(IEnumerable<IContentTypeBase> contentTypes)
        {
            // find all content types impacted by the changes,
            // - content type alias changed
            // - content type property removed, or alias changed
            // - content type composition removed (not testing if composition had properties...)
            //
            // because these are the changes that would impact the raw content data

            // note
            // this is meant to run *after* uow.Commit() so must use WasPropertyDirty() everywhere
            // instead of IsPropertyDirty() since dirty properties have been resetted already

            // hash set handles duplicates
            var changes = new HashSet<IContentTypeBase>(new DelegateEqualityComparer<IContentTypeBase>(
                (x, y) => x.Id == y.Id,
                x => x.Id.GetHashCode()));

            foreach (var contentType in contentTypes)
            {
                var dirty = contentType as IRememberBeingDirty;
                if (dirty == null) throw new Exception("oops");

                // skip new content types
                var isNewContentType = dirty.WasPropertyDirty("HasIdentity");
                if (isNewContentType) continue;

                // alias change?
                var hasAliasChanged = dirty.WasPropertyDirty("Alias");

                // existing property alias change?
                var hasAnyPropertyChangedAlias = contentType.PropertyTypes.Any(propertyType =>
                {
                    var dirtyProperty = propertyType as IRememberBeingDirty;
                    if (dirtyProperty == null) throw new Exception("oops");

                    // skip new properties
                    var isNewProperty = dirtyProperty.WasPropertyDirty("HasIdentity");
                    if (isNewProperty) return false;

                    // alias change?
                    var hasPropertyAliasBeenChanged = dirtyProperty.WasPropertyDirty("Alias");
                    return hasPropertyAliasBeenChanged;
                });

                // removed properties?
                var hasAnyPropertyBeenRemoved = dirty.WasPropertyDirty("HasPropertyTypeBeenRemoved");

                // removed compositions?
                var hasAnyCompositionBeenRemoved = dirty.WasPropertyDirty("HasCompositionTypeBeenRemoved");

                if (hasAliasChanged || hasAnyCompositionBeenRemoved || hasAnyPropertyBeenRemoved || hasAnyPropertyChangedAlias)
                {
                    // add that one
                    changes.Add(contentType);
                }

                if (hasAnyCompositionBeenRemoved || hasAnyPropertyBeenRemoved || hasAnyPropertyChangedAlias)
                {
                    // add all of these that are directly or indirectly composed of that one
                    foreach (var c in contentType.ComposedOf()) 
                        changes.Add(c);
                }
            }

            return changes;
        }

        /// <summary>
        /// Determines which content types are impacted by content types changes, in a way that needs
        /// to be notified to the content service.
        /// </summary>
        /// <param name="contentTypes">The changed content types.</param>
        /// <returns>The impacted content types.</returns>
        internal IEnumerable<IContentTypeBase> ComposeContentTypeChanges(params IContentTypeBase[] contentTypes)
        {
            // hash set handles duplicates
            var notify = new HashSet<IContentTypeBase>(new DelegateEqualityComparer<IContentTypeBase>(
                (x, y) => x.Id == y.Id,
                x => x.Id.GetHashCode()));

            // fixme
            // this method was originally GetContentTypesForXmlUpdates and was targetted at the XML cache
            // so it does NOT handle some situations that would have no impact on the XML cache
            //
            // ALSO it looks like it's never been updated for compositions?
            //
            // situations
            // - local change, eg change the alias of the content type
            //  content cache must know about the change, but no impact to descendants
            // - DAG change, eg change anything WRT properties
            //  
            // eg 'new property' => don't really need to 'rebuild' the xml stuff
            // but need to update the content types = impact on content cache
            //
            // edited & removed properties, must rebuild xml stuff & impact on cache
            // edited property data types, nothing to do & impact the cache
            //
            // TODO try to figure out
            // - refresh the xml/json/serialized in database
            //    ctype alias changed = just that one
            //    ptype alias changed or removed = descendants
            //    composition removed = ptype removed = descendants
            //    composition added, ... = nothing because NO impact on XML
            // - reload the content in cache
            //    if serialized has changed = same as above
            // - update the content type in cache
            //    if the content type has been updated in any way
            //    ctype alias change = just that one
            //    anything else = descendants
            //
            // TODO try this:
            //  pass the contentTypes to the transaction event, let it figure things out
            //  trigger changed ThisType/ThisBranch events?
            //
            // FIXME note that the transaction events CANNOT run the IsDirty, only Was DIRTY * TOO LATE
            // would have the same impact if moving the ContentRepository events to ContentService...
            // BUT could do it by gathering the types before uow.Commit()

            foreach (var contentType in contentTypes)
            {
                // fixme experiment
                var hasOtherThanAliasChanged = ((ContentType)contentType).GetDirtyProperties().Any(x => x.InvariantEquals("Alias") == false);

                var dirty = contentType as IRememberBeingDirty;
                if (dirty == null) throw new Exception("oops");

                // skip new content types
                var isNewContentType = dirty.WasPropertyDirty("HasIdentity");
                if (isNewContentType) continue;

                // fixme - contentType.PropertyTypes is LOCAL ONLY or COMPOSED?
                // fixme - what about ADDED PROPERTIES?
                // fixme - what about properties that CHANGE THEIR DATA TYPE?
                // fixme - what about COMPOSITION CHANGES

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

        #region Change Events

        public class EntityChangeEventArgs : EventArgs
        {
            public EntityChangeEventArgs(IDatabaseUnitOfWork unitOfWork, IEnumerable<IContentTypeBase> entities)
            {
                UnitOfWork = unitOfWork;
                Entities = entities;
            }

            public IEnumerable<IContentTypeBase> Entities { get; private set; }
            public IDatabaseUnitOfWork UnitOfWork { get; private set; }
        }

        public static event TypedEventHandler<ContentTypeServiceBase, EntityChangeEventArgs> TransactionRefreshedEntity;

        protected void OnTransactionRefreshedEntity(IDatabaseUnitOfWork unitOfWork, params IContentTypeBase[] entities)
        {
            var handler = TransactionRefreshedEntity;
            if (handler == null) return;
            var args = new EntityChangeEventArgs(unitOfWork, entities);
            handler(this, args);
        }

        #endregion
    }
}