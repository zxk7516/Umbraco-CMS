using System.Collections.Generic;
using Umbraco.Core.Models;

namespace Umbraco.Core.Persistence.Repositories
{
    public interface IRelationRepository : IRepositoryQueryable<int, IRelation>
    {
        IEnumerable<IRelation> GetByParentId(int id, string relationTypeAlias);
        IEnumerable<IRelation> GetByParentIds(int[] ids, string relationTypeAlias);
        IEnumerable<IRelation> GetByChildIds(int[] ids, string relationTypeAlias);
        IEnumerable<IRelation> GetByParentOrChildId(int id, string relationTypeAlias);
    }
}