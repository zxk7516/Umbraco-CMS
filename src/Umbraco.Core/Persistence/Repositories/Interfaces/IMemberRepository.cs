using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Xml.Linq;
using Umbraco.Core.Models;
using Umbraco.Core.Models.Rdbms;
using Umbraco.Core.Persistence.DatabaseModelDefinitions;
using Umbraco.Core.Persistence.Querying;

namespace Umbraco.Core.Persistence.Repositories
{
    public interface IMemberRepository : IRepositoryVersionable<int, IMember>
    {
        void RebuildMemberXml(Func<IMember, XElement> serializer, int groupSize = 5000, IEnumerable<int> contentTypeIds = null);

        /// <summary>
        /// Finds members in a given role
        /// </summary>
        /// <param name="roleName"></param>
        /// <param name="usernameToMatch"></param>
        /// <param name="matchType"></param>
        /// <returns></returns>
        IEnumerable<IMember> FindMembersInRole(string roleName, string usernameToMatch, StringPropertyMatchType matchType = StringPropertyMatchType.StartsWith);

        /// <summary>
        /// Get all members in a specific group
        /// </summary>
        /// <param name="groupName"></param>
        /// <returns></returns>
        IEnumerable<IMember> GetByMemberGroup(string groupName);

        /// <summary>
        /// Checks if a member with the username exists
        /// </summary>
        /// <param name="username"></param>
        /// <returns></returns>
        bool Exists(string username);

        /// <summary>
        /// Gets the count of items based on a complex query
        /// </summary>
        /// <param name="query"></param>
        /// <returns></returns>
        int GetCountByQuery(IQuery<IMember> query);

        /// <summary>
        /// Gets paged member results
        /// </summary>
        /// <param name="query"></param>
        /// <param name="pageIndex"></param>
        /// <param name="pageSize"></param>
        /// <param name="totalRecords"></param>
        /// <param name="orderBy"></param>
        /// <param name="orderDirection"></param>
        /// <param name="filter"></param>
        /// <returns></returns>
        IEnumerable<IMember> GetPagedResultsByQuery(IQuery<IMember> query, int pageIndex, int pageSize, out int totalRecords,
            string orderBy, Direction orderDirection, string filter = "");

        //IEnumerable<IMember> GetPagedResultsByQuery<TDto>(
        //    Sql sql, int pageIndex, int pageSize, out int totalRecords,
        //    Func<IEnumerable<TDto>, int[]> resolveIds);
    }
}