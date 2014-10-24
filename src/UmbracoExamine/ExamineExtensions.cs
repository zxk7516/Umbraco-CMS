using System;
using Examine.SearchCriteria;

namespace UmbracoExamine
{
    public static class ExamineExtensions
    {
        /// <summary>
        /// Used for backwards compatability - ParentId method should be used on strongly typed criteria
        /// </summary>
        /// <param name="searchCriteria"></param>
        /// <param name="parentId"></param>
        /// <returns></returns>
        [Obsolete("Use ParentId on strongly typed Umbraco search critiera")]
        public static IBooleanOperation ParentId(this ISearchCriteria searchCriteria, int parentId)
        {
            return searchCriteria.Field("parentID", parentId);
        }

        [Obsolete("Use NodeName on strongly typed Umbraco search critiera")]
        public static IBooleanOperation NodeName(this ISearchCriteria searchCriteria, string name)
        {
            return searchCriteria.Field(UmbracoContentIndexer.NodeTypeAliasFieldName, name);
        }
    }
}