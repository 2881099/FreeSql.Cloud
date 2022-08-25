using FreeSql.Cloud.Abstract;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;

namespace FreeSql
{
    /// <summary>
    /// 跟随切换数据库的仓储实现<para></para>
    /// _<para></para>
    /// UnitOfWorkManagerCloud.Begin（优先）<para></para>
    /// 或者 FreeSqlCloud.Change 都会切换 RepositoryCloud 到对应的数据库<para></para>
    /// _<para></para>
    /// FreeSql.Repository 默认的仓储对象，初始化就固定了 IFreeSql（无法跟随切换）
    /// </summary>
    /// <typeparam name="TEntity"></typeparam>
    class RepositoryCloud<TEntity> : IBaseRepository<TEntity> where TEntity : class
    {
        protected virtual IBaseRepository<TEntity> CreateRepository(IFreeSql fsql)
        {
            return fsql.GetRepository<TEntity>();
        }

        internal readonly FreeSqlCloudBase _cloud;
        internal readonly UnitOfWorkManagerCloud _uowManager;
        internal readonly Dictionary<string, IBaseRepository<TEntity>> _repos = new Dictionary<string, IBaseRepository<TEntity>>();
        /// <summary>
        /// 跟随 cloud.Change 切换
        /// </summary>
        /// <param name="cloud"></param>
        public RepositoryCloud(FreeSqlCloudBase cloud)
        {
            _cloud = cloud;
        }
        /// <summary>
        /// 跟随 uowManager.Begin 工作单元切换（优先）<para></para>
        /// 或者<para></para>
        /// 跟随 cloud.Change 切换
        /// </summary>
        /// <param name="cloud"></param>
        /// <param name="uowManager"></param>
        public RepositoryCloud(FreeSqlCloudBase cloud, UnitOfWorkManagerCloud uowManager)
        {
            _cloud = uowManager?.Cloud ?? cloud;
            _uowManager = uowManager;
        }

        public void Dispose()
        {
            ForEachRepos(repo => repo.Dispose());
            _repos.Clear();
        }
        protected void ForEachRepos(Action<IBaseRepository<TEntity>> action)
        {
            foreach (var repo in _repos.Values) action(repo);
        }
        IBaseRepository<TEntity> _firstRepository;
        protected IBaseRepository<TEntity> CurrentRepository
        {
            get
            {
                if (_uowManager == null && _cloud == null) return _repos.Values.First();
                var dbkey = _uowManager != null ? _uowManager.GetDBKey() : _cloud.GetDBKey();
                if (_repos.TryGetValue(dbkey, out var repo) == false)
                {
                    _repos.Add(dbkey, repo = CreateRepository(_cloud.Use(dbkey)));
                    if (_uowManager != null) _uowManager.GetUnitOfWorkManager(dbkey).Binding(repo);
                    if (_firstRepository == null) _firstRepository = repo;
                    else
                    {
                        repo.DbContextOptions = _firstRepository.DbContextOptions;
                        if (_asTypeEntityType != null) repo.AsType(_asTypeEntityType);
                        if (_asTableRule != null) repo.AsTable(_asTableRule);
                    }
                }
                return repo;
            }
        }

        public DbContextOptions DbContextOptions
        {
            get => CurrentRepository.DbContextOptions;
            set => ForEachRepos(repo => repo.DbContextOptions = value);
        }
        Type _asTypeEntityType;
        public void AsType(Type entityType)
        {
            _asTypeEntityType = entityType;
            ForEachRepos(repo => repo.AsType(entityType));
        }
        Func<string, string> _asTableRule;
        public void AsTable(Func<string, string> rule)
        {
            _asTableRule = rule;
            ForEachRepos(repo => repo.AsTable(rule));
        }
        public IUnitOfWork UnitOfWork
        {
            get => CurrentRepository.UnitOfWork;
            set => CurrentRepository.UnitOfWork = value;
        }

        public IFreeSql Orm => CurrentRepository.Orm;
        public Type EntityType => CurrentRepository.EntityType;
        public IDataFilter<TEntity> DataFilter => CurrentRepository.DataFilter;
        public ISelect<TEntity> Select => CurrentRepository.Select;
        public IUpdate<TEntity> UpdateDiy => CurrentRepository.UpdateDiy;
        public ISelect<TEntity> Where(Expression<Func<TEntity, bool>> exp) => CurrentRepository.Where(exp);
        public ISelect<TEntity> WhereIf(bool condition, Expression<Func<TEntity, bool>> exp) => CurrentRepository.WhereIf(condition, exp);

        public void Attach(TEntity entity) => CurrentRepository.Attach(entity);
        public void Attach(IEnumerable<TEntity> entity) => CurrentRepository.Attach(entity);
        public IBaseRepository<TEntity> AttachOnlyPrimary(TEntity data) => CurrentRepository.AttachOnlyPrimary(data);
        public Dictionary<string, object[]> CompareState(TEntity newdata) => CurrentRepository.CompareState(newdata);
        public void FlushState() => CurrentRepository.FlushState();

        public void BeginEdit(List<TEntity> data) => CurrentRepository.BeginEdit(data);
        public int EndEdit(List<TEntity> data = null) => CurrentRepository.EndEdit(data);

        public TEntity Insert(TEntity entity) => CurrentRepository.Insert(entity);
        public List<TEntity> Insert(IEnumerable<TEntity> entitys) => CurrentRepository.Insert(entitys);
        public TEntity InsertOrUpdate(TEntity entity) => CurrentRepository.InsertOrUpdate(entity);
        public void SaveMany(TEntity entity, string propertyName) => CurrentRepository.SaveMany(entity, propertyName);

        public int Update(TEntity entity) => CurrentRepository.Update(entity);
        public int Update(IEnumerable<TEntity> entitys) => CurrentRepository.Update(entitys);

        public int Delete(TEntity entity) => CurrentRepository.Delete(entity);
        public int Delete(IEnumerable<TEntity> entitys) => CurrentRepository.Delete(entitys);
        public int Delete(Expression<Func<TEntity, bool>> predicate) => CurrentRepository.Delete(predicate);
        public List<object> DeleteCascadeByDatabase(Expression<Func<TEntity, bool>> predicate) => CurrentRepository.DeleteCascadeByDatabase(predicate);

#if net40
#else
        public Task<TEntity> InsertAsync(TEntity entity, CancellationToken cancellationToken = default) => CurrentRepository.InsertAsync(entity, cancellationToken);
        public Task<List<TEntity>> InsertAsync(IEnumerable<TEntity> entitys, CancellationToken cancellationToken = default) => CurrentRepository.InsertAsync(entitys, cancellationToken);
        public Task<TEntity> InsertOrUpdateAsync(TEntity entity, CancellationToken cancellationToken = default) => CurrentRepository.InsertOrUpdateAsync(entity, cancellationToken);
        public Task SaveManyAsync(TEntity entity, string propertyName, CancellationToken cancellationToken = default) => CurrentRepository.SaveManyAsync(entity, propertyName, cancellationToken);

        public Task<int> UpdateAsync(TEntity entity, CancellationToken cancellationToken = default) => CurrentRepository.UpdateAsync(entity, cancellationToken);
        public Task<int> UpdateAsync(IEnumerable<TEntity> entitys, CancellationToken cancellationToken = default) => CurrentRepository.UpdateAsync(entitys, cancellationToken);

        public Task<int> DeleteAsync(TEntity entity, CancellationToken cancellationToken = default) => CurrentRepository.DeleteAsync(entity, cancellationToken);
        public Task<int> DeleteAsync(IEnumerable<TEntity> entitys, CancellationToken cancellationToken = default) => CurrentRepository.DeleteAsync(entitys, cancellationToken);
        public Task<int> DeleteAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default) => CurrentRepository.DeleteAsync(predicate, cancellationToken);
        public Task<List<object>> DeleteCascadeByDatabaseAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default) => CurrentRepository.DeleteCascadeByDatabaseAsync(predicate, cancellationToken);
#endif
    }

//    class RepositoryCloud<TDBKey, TEntity, TKey> : RepositoryCloud<TDBKey, TEntity>, IBaseRepository<TEntity, TKey>
//        where TEntity : class
//    {
//        /// <summary>
//        /// 跟随 cloud.Change 切换
//        /// </summary>
//        /// <param name="cloud"></param>
//        public RepositoryCloud(FreeSqlCloud<TDBKey> cloud) : base(cloud) { }
//        /// <summary>
//        /// 跟随 uowManager.Begin 工作单元切换（优先）<para></para>
//        /// 或者<para></para>
//        /// 跟随 cloud.Change 切换
//        /// </summary>
//        /// <param name="cloud"></param>
//        /// <param name="uowManager"></param>
//        public RepositoryCloud(FreeSqlCloud<TDBKey> cloud, UnitOfWorkManagerCloud<TDBKey> uowManager) : base(cloud, uowManager) { }

//        TEntity CheckTKeyAndReturnIdEntity(TKey id)
//        {
//            var repo = CurrentRepository;
//            var tb = repo.Orm.CodeFirst.GetTableByEntity(repo.EntityType);
//            if (tb.Primarys.Length != 1) throw new Exception(DbContextStrings.EntityType_PrimaryKeyIsNotOne(repo.EntityType.Name));
//            if (tb.Primarys[0].CsType.NullableTypeOrThis() != typeof(TKey).NullableTypeOrThis()) throw new Exception(DbContextStrings.EntityType_PrimaryKeyError(repo.EntityType.Name, typeof(TKey).FullName));
//            var obj = tb.Type.CreateInstanceGetDefaultValue();
//            repo.Orm.SetEntityValueWithPropertyName(tb.Type, obj, tb.Primarys[0].CsName, id);
//            var ret = obj as TEntity;
//            if (ret == null) throw new Exception(DbContextStrings.EntityType_CannotConvert(repo.EntityType.Name, typeof(TEntity).Name));
//            return ret;
//        }

//        public virtual TEntity Get(TKey id) => Select.WhereDynamic(CheckTKeyAndReturnIdEntity(id)).ToOne();
//        public virtual TEntity Find(TKey id) => Select.WhereDynamic(CheckTKeyAndReturnIdEntity(id)).ToOne();
//        public virtual int Delete(TKey id) => Delete(CheckTKeyAndReturnIdEntity(id));

//#if net40
//#else
//        public Task<TEntity> GetAsync(TKey id, CancellationToken cancellationToken = default) => Select.WhereDynamic(CheckTKeyAndReturnIdEntity(id)).ToOneAsync(cancellationToken);
//        public Task<TEntity> FindAsync(TKey id, CancellationToken cancellationToken = default) => Select.WhereDynamic(CheckTKeyAndReturnIdEntity(id)).ToOneAsync(cancellationToken);
//        public Task<int> DeleteAsync(TKey id, CancellationToken cancellationToken = default) => DeleteAsync(CheckTKeyAndReturnIdEntity(id), cancellationToken);
//#endif
//    }
}
