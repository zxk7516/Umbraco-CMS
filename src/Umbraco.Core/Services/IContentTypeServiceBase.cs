using System;
using System.Collections.Generic;
using Umbraco.Core.Models;

namespace Umbraco.Core.Services
{
    /// <summary>
    /// Provides a common base interface for <see cref="IContentTypeService"/>, <see cref="IMediaTypeService"/> and <see cref="IMemberTypeService"/>.
    /// </summary>
    /// <typeparam name="TItem">The type of the item.</typeparam>
    public interface IContentTypeServiceBase<TItem> : IService
        where TItem: IContentTypeComposition
    {
        TItem Get(int id);
        TItem Get(Guid key);
        TItem Get(string alias);

        IEnumerable<TItem> GetAll(params int[] ids);

        IEnumerable<TItem> GetDescendants(int id, bool andSelf); // parent-child axis
        IEnumerable<TItem> GetComposedOf(int id); // composition axis

        IEnumerable<TItem> GetChildren(int id);
        bool HasChildren(int id);

        void Save(TItem item, int userId = 0);
        void Save(IEnumerable<TItem> items, int userId = 0);

        void Delete(TItem item, int userId = 0);
        void Delete(IEnumerable<TItem> item, int userId = 0);


        Attempt<string[]> ValidateComposition(TItem compo);


        Attempt<int> CreateContainer(int parentContainerId, string name, int userId = 0);
        void SaveContainer(EntityContainer container, int userId = 0);
        EntityContainer GetContainer(int containerId);
        EntityContainer GetContainer(Guid containerId);
        void DeleteContainer(int containerId, int userId = 0);

        Attempt<OperationStatus<MoveOperationStatusType>> Move(TItem moving, int containerId);
    }
}
