using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Umbraco.Core.Events;
using Umbraco.Core.Logging;
using Umbraco.Core.Models;
using Umbraco.Core.Persistence;
using Umbraco.Core.Persistence.Querying;
using Umbraco.Core.Persistence.UnitOfWork;

namespace Umbraco.Core.Services
{
    internal class MediaTypeService : ContentTypeServiceBase, IMediaTypeService
    {
        private readonly IMediaService _mediaService;

        public MediaTypeService(IDatabaseUnitOfWorkProvider provider, RepositoryFactory repositoryFactory, ILogger logger, IMediaService mediaService)
            : base(provider, repositoryFactory, logger)
        {
            if (mediaService == null) throw new ArgumentNullException("mediaService");
            _mediaService = mediaService;
        }

        #region Media - Get, Has, Is

        /// <summary>
        /// Gets an <see cref="IMediaType"/> object by its Id
        /// </summary>
        /// <param name="id">Id of the <see cref="IMediaType"/> to retrieve</param>
        /// <returns><see cref="IMediaType"/></returns>
        public IMediaType GetMediaType(int id)
        {
            return WithReadLockedMediaTypes(repository => repository.Get(id));
        }

        /// <summary>
        /// Gets an <see cref="IMediaType"/> object by its Alias
        /// </summary>
        /// <param name="alias">Alias of the <see cref="IMediaType"/> to retrieve</param>
        /// <returns><see cref="IMediaType"/></returns>
        public IMediaType GetMediaType(string alias)
        {
            var query = Query<IMediaType>.Builder.Where(x => x.Alias == alias);
            return WithReadLockedMediaTypes(repository => repository.GetByQuery(query).FirstOrDefault());
        }

        /// <summary>
        /// Gets a list of all available <see cref="IMediaType"/> objects
        /// </summary>
        /// <param name="ids">Optional list of ids</param>
        /// <returns>An Enumerable list of <see cref="IMediaType"/> objects</returns>
        public IEnumerable<IMediaType> GetAllMediaTypes(params int[] ids)
        {
            return WithReadLockedMediaTypes(repository => repository.GetAll(ids));
        }

        /// <summary>
        /// Gets a list of children for a <see cref="IMediaType"/> object
        /// </summary>
        /// <param name="id">Id of the Parent</param>
        /// <returns>An Enumerable list of <see cref="IMediaType"/> objects</returns>
        public IEnumerable<IMediaType> GetMediaTypeChildren(int id)
        {
            var query = Query<IMediaType>.Builder.Where(x => x.ParentId == id);
            return WithReadLockedMediaTypes(repository => repository.GetByQuery(query));
        }

        /// <summary>
        /// Checks whether an <see cref="IMediaType"/> item has any children
        /// </summary>
        /// <param name="id">Id of the <see cref="IMediaType"/></param>
        /// <returns>True if the media type has any children otherwise False</returns>
        public bool MediaTypeHasChildren(int id)
        {
            var query = Query<IMediaType>.Builder.Where(x => x.ParentId == id);
            return WithReadLockedMediaTypes(repository => repository.Count(query) > 0);
        }

        #endregion

        #region Media - Save, Delete

        /// <summary>
        /// Saves a single <see cref="IMediaType"/> object
        /// </summary>
        /// <param name="mediaType"><see cref="IMediaType"/> to save</param>
        /// <param name="userId">Optional Id of the user saving the MediaType</param>
        public void Save(IMediaType mediaType, int userId = 0)
        {
            if (SavingMediaType.IsRaisedEventCancelled(new SaveEventArgs<IMediaType>(mediaType), this))
                return;

            WithWriteLockedMediaAndMediaTypes(repository =>
            {
                // validate the DAG transform, within the lock
                ValidateLocked(mediaType); // throws if invalid

                mediaType.CreatorId = userId;
                repository.AddOrUpdate(mediaType); // also updates contents
                repository.UnitOfWork.Commit(); // commits the UOW but NOT the transaction

                // figure out content types impacted by the changes through composition
                var changedTypes = ComposeContentTypeChangesForTransactionEvent(mediaType).ToArray();
                OnTransactionRefreshedEntity(repository.UnitOfWork, changedTypes);
            });

            // fixme - raise distributed events
            // raise event fot that type only, because it's a distributed event,
            // so impacted types (through composition) will be determined locally
            // fixme - can we do it OR do we need extra infos eg RefreshTypeLocally, RefreshTypeComposition
            //Changed.RaiseEvent(new ChangeEventArgs(mediaType), this);
            //ApplyChangesToContent(changedTypes);

            SavedMediaType.RaiseEvent(new SaveEventArgs<IMediaType>(mediaType, false), this);
            Audit(AuditType.Save, string.Format("Save MediaType performed by user"), userId, mediaType.Id);
        }

        /// <summary>
        /// Saves a collection of <see cref="IMediaType"/> objects
        /// </summary>
        /// <param name="mediaTypes">Collection of <see cref="IMediaType"/> to save</param>
        /// <param name="userId">Optional Id of the user savging the MediaTypes</param>
        public void Save(IEnumerable<IMediaType> mediaTypes, int userId = 0)
        {
            var mediaTypesA = mediaTypes.ToArray();

            if (SavingMediaType.IsRaisedEventCancelled(new SaveEventArgs<IMediaType>(mediaTypesA), this))
                return;

            WithWriteLockedMediaAndMediaTypes(repository =>
            {
                // all-or-nothing, validate the DAG transforms, within the lock
                foreach (var mediaType in mediaTypesA)
                    ValidateLocked(mediaType); // throws if invalid

                foreach (var mediaType in mediaTypesA)
                {
                    mediaType.CreatorId = userId;
                    repository.AddOrUpdate(mediaType); // also updates contents
                }

                repository.UnitOfWork.Commit(); // commits the UOW but NOT the transaction

                // figure out content types impacted by the changes through composition
                var changedTypes = ComposeContentTypeChangesForTransactionEvent(mediaTypesA).ToArray();
                OnTransactionRefreshedEntity(repository.UnitOfWork, changedTypes);
            });

            // fixme - raise distributed events
            // raise event fot that type only, because it's a distributed event,
            // so impacted types (through composition) will be determined locally
            // fixme - can we do it OR do we need extra infos eg RefreshTypeLocally, RefreshTypeComposition
            //Changed.RaiseEvent(new ChangeEventArgs(contentTypesA), this);

            SavedMediaType.RaiseEvent(new SaveEventArgs<IMediaType>(mediaTypesA, false), this);
            Audit(AuditType.Save, string.Format("Save MediaTypes performed by user"), userId, -1);
        }

        /// <summary>
        /// Deletes a single <see cref="IMediaType"/> object
        /// </summary>
        /// <param name="mediaType"><see cref="IMediaType"/> to delete</param>
        /// <param name="userId">Optional Id of the user deleting the MediaType</param>
        /// <remarks>Deleting a <see cref="IMediaType"/> will delete all the <see cref="IMedia"/> objects based on this <see cref="IMediaType"/></remarks>
        public void Delete(IMediaType mediaType, int userId = 0)
        {
            if (DeletingMediaType.IsRaisedEventCancelled(new DeleteEventArgs<IMediaType>(mediaType), this))
                return;

            WithWriteLockedMediaAndMediaTypes(repository =>
            {
                // all descendants are going to be deleted
                var descendantsAndSelf = mediaType.DescendantsAndSelf().ToArray();

                // all impacted (through composition) probably lose some properties
                var impacted = descendantsAndSelf.SelectMany(x => x.ComposedOf())
                    .Distinct()
                    .Except(descendantsAndSelf) // will be deleted anyway
                    .ToArray();

                // delete content
                foreach (var d in descendantsAndSelf)
                    _mediaService.DeleteMediaOfType(d.Id);

                // finally delete the content type
                // - recursively deletes all descendants
                // - deletes all associated property data
                //  (contents of any descendant type have been deleted but
                //   contents of any composed (impacted) type remain but
                //   need to have their property data cleared)
                repository.Delete(mediaType);

                repository.UnitOfWork.Commit(); // commits the UOW but NOT the transaction

                // no need for 'transaction event' for deleted content
                // because deleting content does trigger its own event
                //
                // need 'transaction event' for content that have changed
                // ie those that were composed of the deleted content OR
                // of any of its descendants

                OnTransactionRefreshedEntity(repository.UnitOfWork, impacted);
            });

            // fixme - raise distributed events

            DeletedMediaType.RaiseEvent(new DeleteEventArgs<IMediaType>(mediaType, false), this);
            Audit(AuditType.Delete, string.Format("Delete MediaType performed by user"), userId, mediaType.Id);
        }

        /// <summary>
        /// Deletes a collection of <see cref="IMediaType"/> objects
        /// </summary>
        /// <param name="mediaTypes">Collection of <see cref="IMediaType"/> to delete</param>
        /// <param name="userId"></param>
        /// <remarks>Deleting a <see cref="IMediaType"/> will delete all the <see cref="IMedia"/> objects based on this <see cref="IMediaType"/></remarks>
        public void Delete(IEnumerable<IMediaType> mediaTypes, int userId = 0)
        {
            var mediaTypesA = mediaTypes.ToArray();

            if (DeletingMediaType.IsRaisedEventCancelled(new DeleteEventArgs<IMediaType>(mediaTypesA), this))
                return;

            WithWriteLockedMediaAndMediaTypes(repository =>
            {
                // all descendants are going to be deleted
                var allDescendantsAndSelf = mediaTypesA.SelectMany(x => x.DescendantsAndSelf())
                    .Distinct()
                    .ToArray();

                // all impacted (through composition) probably lose some properties
                var impacted = allDescendantsAndSelf.SelectMany(x => x.ComposedOf())
                    .Distinct()
                    .Except(allDescendantsAndSelf) // will be deleted anyway
                    .ToArray();

                // delete content
                foreach (var d in allDescendantsAndSelf)
                    _mediaService.DeleteMediaOfType(d.Id);

                // finally delete the content types
                // (see notes in overload)
                foreach (var mediaType in mediaTypesA)
                    repository.Delete(mediaType);

                repository.UnitOfWork.Commit(); // commits the UOW but NOT the transaction

                // (see notes in overload)
                OnTransactionRefreshedEntity(repository.UnitOfWork, impacted);
            });

            // fixme - raise distributed events
            // allDescendantsAndSelf: report REMOVE
            // impacted: report REFRESH
            // BUT async => just report WTF?!

            DeletedMediaType.RaiseEvent(new DeleteEventArgs<IMediaType>(mediaTypesA, false), this);
            Audit(AuditType.Delete, string.Format("Delete MediaTypes performed by user"), userId, -1);
        }

        #endregion

        #region Media - Move

        // not implemented
        // (see content)

        #endregion

    }
}
