using System;
using System.Collections.Generic;
using System.Linq;
using Umbraco.Core.Models;
using Umbraco.Core.Models.EntityBase;
using Umbraco.Core.Models.Rdbms;
using Umbraco.Core.Persistence.Caching;
using Umbraco.Core.Persistence.Factories;
using Umbraco.Core.Persistence.Querying;
using Umbraco.Core.Persistence.UnitOfWork;

namespace Umbraco.Core.Persistence.Repositories
{
    /// <summary>
    /// Represents a repository for doing CRUD operations for <see cref="Relation"/>
    /// </summary>
    internal class RelationRepository : PetaPocoRepositoryBase<int, IRelation>, IRelationRepository
    {
        private readonly IRelationTypeRepository _relationTypeRepository;

        public RelationRepository(IDatabaseUnitOfWork work, IRelationTypeRepository relationTypeRepository)
            : base(work)
        {
            _relationTypeRepository = relationTypeRepository;
        }

        public RelationRepository(IDatabaseUnitOfWork work, IRepositoryCacheProvider cache, IRelationTypeRepository relationTypeRepository)
            : base(work, cache)
        {
            _relationTypeRepository = relationTypeRepository;
        }

        #region Overrides of RepositoryBase<int,Relation>

        protected override IRelation PerformGet(int id)
        {
            var sql = GetBaseQuery(false);
            sql.Where(GetBaseWhereClause(), new { Id = id });

            var dto = Database.FirstOrDefault<RelationDto>(sql);
            if (dto == null)
                return null;

            var relationType = _relationTypeRepository.Get(dto.RelationType);
            if (relationType == null)
                throw new Exception(string.Format("RelationType with Id: {0} doesn't exist", dto.RelationType));

            var factory = new RelationFactory(relationType);
            var entity = factory.BuildEntity(dto);
            // on initial construction we don't want to have dirty properties tracked
            // http://issues.umbraco.org/issue/U4-1946
            ((TracksChangesEntityBase)entity).ResetDirtyProperties(false);

            return entity;
        }

        protected override IEnumerable<IRelation> PerformGetAll(params int[] ids)
        {
            //TODO: Performance here is no good, we can easily do a query with an SQL 'IN' operator
            // to acheive this!!!

            if (ids.Any())
            {
                foreach (var id in ids)
                {
                    yield return Get(id);
                }
            }
            else
            {
                var dtos = Database.Fetch<RelationDto>("WHERE id > 0");
                foreach (var dto in dtos)
                {
                    yield return Get(dto.Id);
                }
            }
        }

        protected override IEnumerable<IRelation> PerformGetByQuery(IQuery<IRelation> query)
        {
            var sqlClause = GetBaseQuery(false);
            var translator = new SqlTranslator<IRelation>(sqlClause, query);
            var sql = translator.Translate();

            var dtos = Database.Fetch<RelationDto>(sql);

            //TODO: This is gonna be pretty horrible for performance, should really just convert the already fetched list.
            foreach (var dto in dtos)
            {
                yield return Get(dto.Id);
            }
        }

        #endregion

        #region Overrides of PetaPocoRepositoryBase<int,Relation>

        protected override Sql GetBaseQuery(bool isCount)
        {
            var sql = new Sql();
            sql.Select(isCount ? "COUNT(*)" : "*")
               .From<RelationDto>();
            return sql;
        }

        protected override string GetBaseWhereClause()
        {
            return "umbracoRelation.id = @Id";
        }

        protected override IEnumerable<string> GetDeleteClauses()
        {
            var list = new List<string>
                           {
                               "DELETE FROM umbracoRelation WHERE id = @Id"
                           };
            return list;
        }

        protected override Guid NodeObjectTypeId
        {
            get { throw new NotImplementedException(); }
        }

        #endregion

        #region Unit of Work Implementation

        protected override void PersistNewItem(IRelation entity)
        {
            ((Entity)entity).AddingEntity();

            var factory = new RelationFactory(entity.RelationType);
            var dto = factory.BuildDto(entity);

            var id = Convert.ToInt32(Database.Insert(dto));
            entity.Id = id;

            ((ICanBeDirty)entity).ResetDirtyProperties();
        }

        protected override void PersistUpdatedItem(IRelation entity)
        {
            ((Entity)entity).UpdatingEntity();

            var factory = new RelationFactory(entity.RelationType);
            var dto = factory.BuildDto(entity);
            Database.Update(dto);

            ((ICanBeDirty)entity).ResetDirtyProperties();
        }

        #endregion

        public IEnumerable<IRelation> GetByParentId(int id, string relationTypeAlias)
        {
            //TODO: Caching?

            var relationType = _relationTypeRepository.GetByAlias(relationTypeAlias);
            if (relationType == null)
            {
                return Enumerable.Empty<IRelation>();
            }

            var sql = new Sql();
            sql.Select("umbracoRelation.*")
                .From<RelationDto>()
                .InnerJoin<RelationTypeDto>()
                .On<RelationDto, RelationTypeDto>(dto => dto.RelationType, dto => dto.Id)
                .Where<RelationDto>(dto => dto.ParentId == id)
                .Where<RelationTypeDto>(dto => dto.Alias == relationTypeAlias);

            var factory = new RelationFactory(relationType);

            return Database.Fetch<RelationDto>(sql).Select(factory.BuildEntity).ToArray();
        }

        public IEnumerable<IRelation> GetByParentIds(int[] ids, string relationTypeAlias)
        {
            //TODO: Caching?

            var relationType = _relationTypeRepository.GetByAlias(relationTypeAlias);
            if (relationType == null)
            {
                return Enumerable.Empty<IRelation>();
            }

            var sql = new Sql();
            sql.Select("umbracoRelation.*")
                .From<RelationDto>()
                .InnerJoin<RelationTypeDto>()
                .On<RelationDto, RelationTypeDto>(dto => dto.RelationType, dto => dto.Id)
                .Where("umbracoRelation.parentId IN (@ids)", new {ids = ids})
                .Where<RelationTypeDto>(dto => dto.Alias == relationTypeAlias);

            var factory = new RelationFactory(relationType);

            return Database.Fetch<RelationDto>(sql).Select(factory.BuildEntity).ToArray();
        }

        public IEnumerable<IRelation> GetByChildIds(int[] ids, string relationTypeAlias)
        {
            //TODO: Caching?

            var relationType = _relationTypeRepository.GetByAlias(relationTypeAlias);
            if (relationType == null)
            {
                return Enumerable.Empty<IRelation>();
            }

            var sql = new Sql();
            sql.Select("umbracoRelation.*")
                .From<RelationDto>()
                .InnerJoin<RelationTypeDto>()
                .On<RelationDto, RelationTypeDto>(dto => dto.RelationType, dto => dto.Id)
                .Where("umbracoRelation.childId IN (@ids)", new { ids = ids })
                .Where<RelationTypeDto>(dto => dto.Alias == relationTypeAlias);

            var factory = new RelationFactory(relationType);

            return Database.Fetch<RelationDto>(sql).Select(factory.BuildEntity).ToArray();
        }

        public IEnumerable<IRelation> GetByParentOrChildId(int id, string relationTypeAlias)
        {
            //TODO: Caching?

            var relationType = _relationTypeRepository.GetByAlias(relationTypeAlias);
            if (relationType == null)
            {
                return Enumerable.Empty<IRelation>();
            }

            var sql = new Sql();
            sql.Select("umbracoRelation.*")
                .From<RelationDto>()
                .InnerJoin<RelationTypeDto>()
                .On<RelationDto, RelationTypeDto>(dto => dto.RelationType, dto => dto.Id)
                .Where<RelationDto>(dto => dto.ParentId == id || dto.ChildId == id)
                .Where<RelationTypeDto>(dto => dto.Alias == relationTypeAlias);

            var factory = new RelationFactory(relationType);

            return Database.Fetch<RelationDto>(sql).Select(factory.BuildEntity).ToArray();
        }
    }
}