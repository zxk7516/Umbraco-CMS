using System;
using System.Collections.Generic;
using System.Xml.Linq;
using Umbraco.Core.Models;
using Umbraco.Core.Models.EntityBase;

namespace Umbraco.Core.Services
{
    /// <summary>
    /// Defines the ContentTypeService, which is an easy access to operations involving <see cref="IContentType"/>
    /// </summary>
    public interface IContentTypeService : IContentTypeServiceBase<IContentType>
    {
#error 'course, refactor this!
        Attempt<string[]> ValidateComposition(IContentTypeComposition compo);

        Attempt<int> CreateContentTypeContainer(int parentId, string name, int userId = 0);
        Attempt<int> CreateMediaTypeContainer(int parentId, string name, int userId = 0);
        void SaveContentTypeContainer(EntityContainer container, int userId = 0);
        void SaveMediaTypeContainer(EntityContainer container, int userId = 0);
        EntityContainer GetContentTypeContainer(int containerId);
        EntityContainer GetContentTypeContainer(Guid containerId);
        EntityContainer GetMediaTypeContainer(int containerId);
        EntityContainer GetMediaTypeContainer(Guid containerId);
        void DeleteMediaTypeContainer(int folderId, int userId = 0);
        void DeleteContentTypeContainer(int containerId, int userId = 0);
//error

        /// <summary>
        /// Gets all property type aliases.
        /// </summary>
        /// <returns></returns>
        IEnumerable<string> GetAllPropertyTypeAliases();

        /// <summary>
        /// Copies a content type as a child under the specified parent if specified (otherwise to the root)
        /// </summary>
        /// <param name="original">
        /// The content type to copy
        /// </param>
        /// <param name="alias">
        /// The new alias of the content type
        /// </param>
        /// <param name="name">
        /// The new name of the content type
        /// </param>
        /// <param name="parentId">
        /// The parent to copy the content type to, default is -1 (root)
        /// </param>
        /// <returns></returns>
        IContentType Copy(IContentType original, string alias, string name, int parentId = -1);

        /// <summary>
        /// Copies a content type as a child under the specified parent if specified (otherwise to the root)
        /// </summary>
        /// <param name="original">
        /// The content type to copy
        /// </param>
        /// <param name="alias">
        /// The new alias of the content type
        /// </param>
        /// <param name="name">
        /// The new name of the content type
        /// </param>
        /// <param name="parent">
        /// The parent to copy the content type to
        /// </param>
        /// <returns></returns>
        IContentType Copy(IContentType original, string alias, string name, IContentType parent);
#error 'course we don't want these here
        Attempt<OperationStatus<MoveOperationStatusType>> MoveMediaType(IMediaType toMove, int containerId);
        Attempt<OperationStatus<MoveOperationStatusType>> MoveContentType(IContentType toMove, int containerId);
    }
}