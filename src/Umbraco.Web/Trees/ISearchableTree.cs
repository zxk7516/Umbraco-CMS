using System.Collections.Generic;
using Umbraco.Web.Models.ContentEditing;

namespace Umbraco.Web.Trees
{
    public interface ISearchableTree
    {
        string TreeAlias { get; }
        IEnumerable<SearchResultItem> Search(string searchText);
    }
}