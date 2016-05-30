using System.Collections.Generic;
using System.Linq;
using System.Web.Http;
using Umbraco.Web.Models.ContentEditing;
using Umbraco.Web.Mvc;
using Umbraco.Web.Trees;

namespace Umbraco.Web.Editors
{
    /// <summary>
    /// Used to search the back office
    /// </summary>
    [PluginController("UmbracoApi")]
    public sealed class SearchController
    {
        [HttpGet]
        public IEnumerable<SearchResultItem> Search(string query, string treeAlias)
        {
            var searchTree = SearchableTreeResolver.Current.Find(treeAlias);
            if (searchTree == null)
            {
                return Enumerable.Empty<SearchResultItem>();
            }

            return searchTree.Search(query);
        }
    }
}