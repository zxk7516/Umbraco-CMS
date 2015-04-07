using System.Collections.Generic;

namespace Umbraco.Core.Services
{
    /// <summary>
    /// Provides a common base interface for <see cref="IContentTypeService"/>, <see cref="IMediaTypeService"/> and <see cref="IMemberTypeService"/>.
    /// </summary>
    /// <typeparam name="TItem">The type of the item.</typeparam>
    public interface IContentTypeServiceBase<TItem> : IService
    {
        TItem Get(int id);
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
    }
}
