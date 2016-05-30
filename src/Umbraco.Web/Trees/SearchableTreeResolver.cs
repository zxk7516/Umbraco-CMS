using System;
using System.Collections.Generic;
using System.Linq;
using Umbraco.Core.Logging;
using Umbraco.Core.ObjectResolution;

namespace Umbraco.Web.Trees
{
    public class SearchableTreeResolver : LazyManyObjectsResolverBase<SearchableTreeResolver, ISearchableTree>
    {
        public SearchableTreeResolver(
            IServiceProvider serviceProvider, ILogger logger, Func<IEnumerable<Type>> typeListProducerList)
            : base(serviceProvider, logger, typeListProducerList, ObjectLifetimeScope.Application)
        {
        }

        private readonly IDictionary<string, Lazy<ISearchableTree>> _explicitTrees = new Dictionary<string, Lazy<ISearchableTree>>();

        /// <summary>
        /// Returns all instances of ISearchableTree
        /// </summary>
        /// <remarks>
        /// This checks if an explicit tree has been registered and if so will use that instead of the one resolved from the types
        /// </remarks>
        public IEnumerable<ISearchableTree> SearchableTrees
        {
            get
            {
                foreach (var tree in Values)
                {
                    if (_explicitTrees.ContainsKey(tree.TreeAlias))
                    {
                        yield return _explicitTrees[tree.TreeAlias].Value;
                    }
                    else
                    {
                        yield return tree;
                    }
                }
            }
        }

        /// <summary>
        /// Gets the ISearchableTree instance for the specified tree alias
        /// </summary>
        /// <param name="treeAlias"></param>
        /// <returns></returns>
        public ISearchableTree Find(string treeAlias)
        {
            return SearchableTrees.FirstOrDefault(x => x.TreeAlias == treeAlias);
        }

        /// <summary>
        /// Registers an explicit instance to use for the tree alias
        /// </summary>
        /// <param name="treeAlias"></param>
        /// <param name="tree"></param>
        public void RegisterSearchableTreeType(string treeAlias, Lazy<ISearchableTree> tree)
        {
            _explicitTrees.Add(treeAlias, tree);
        }

    }
}