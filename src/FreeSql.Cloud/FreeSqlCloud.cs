using FreeSql.Cloud.Abstract;
using FreeSql.Cloud.Model;
using FreeSql.Cloud.Saga;
using FreeSql.Cloud.Tcc;
using FreeSql.Internal;
using System;
using System.Collections.Generic;
using System.Data;

namespace FreeSql
{
	//public class FreeSqlCloud : FreeSqlCloud<string> { }
	public partial class FreeSqlCloud<TDBKey> : FreeSqlCloudBase, IFreeSql
    {
        internal override string GetDBKey() => _dbkey.ToInvariantCultureToString();
        public override IFreeSql Use(DBKeyString dbkey) => Use((TDBKey)typeof(TDBKey).FromObject(dbkey?.ToString()));
        public override IFreeSql Change(DBKeyString dbkey) => Change((TDBKey)typeof(TDBKey).FromObject(dbkey?.ToString()));

        public string DistributeKey { get; }
        public Action<string> DistributeTrace;

        #region EntitySteering
        /// <summary>
        /// 实体类型转向配置，如 User -> db2，之后直接使用 fsqlc.Select&lt;User&gt;()，而不需要 fsqlc.Change("db2").Select&lt;User&gt;()
        /// </summary>
        public Action<object, EntitySteeringEventArgs> EntitySteering;
        public class EntitySteeringEventArgs
        {
            public string MethodName { get; internal set; }
            public Type EntityType { get; internal set; }
            /// <summary>
            /// 可用的目标DBKey
            /// </summary>
            public TDBKey[] AvailableDBKeys { get; internal set; }

            internal bool _dbkeyChanged;
            internal TDBKey _dbkey;
            /// <summary>
            /// 转向的目标DBKey，重写该值可转向到其他目标DBKey
            /// </summary>
            public TDBKey DBKey
            {
                get => _dbkey;
                set
                {
                    _dbkeyChanged = true;
                    _dbkey = value;
                }
            }
        }
        #endregion

        internal TDBKey _dbkeyMaster;

        internal AsyncLocalAccessor<TDBKey> _dbkeyCurrent;
        internal TDBKey _dbkey
        {
            get
            {
                var val = _dbkeyCurrent.Value;
                if (typeof(TDBKey) == typeof(string) && val == null) return _dbkeyMaster;
                return val;
            }
        }
        internal IFreeSql _ormMaster => _ib.Get(_dbkeyMaster);
        internal IFreeSql _ormCurrent => _ib.Get(_dbkey);
        internal IdleBus<TDBKey, IFreeSql> _ib;
        internal FreeScheduler.Scheduler _scheduler;
        internal bool _distributeTraceEnable => DistributeTrace != null;
        internal void _distributedTraceCall(string log)
        {
            DistributeTrace?.Invoke($"{DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss")} 【{(DistributeKey ?? "FreeSql.Cloud")}】{log}");
        }

        public FreeSqlCloud() : this(null) { }
        public FreeSqlCloud(string distributeKey)
        {
            DistributeKey = distributeKey?.Trim();
            if (string.IsNullOrWhiteSpace(DistributeKey)) DistributeKey = null;
            _ib = new IdleBus<TDBKey, IFreeSql>(TimeSpan.FromMinutes(3));
            _ib.Notice += (_, __) => { };
            _dbkeyCurrent = new AsyncLocalAccessor<TDBKey>(() =>
            {
                if (typeof(TDBKey) == typeof(string) && _dbkeyMaster == null) return (TDBKey)typeof(TDBKey).FromObject("");
                return _dbkeyMaster;
            });
		}

        /// <summary>
        /// 切换数据库（同一线程，或异步await 后续操作有效）<para></para>
        /// 注意：单次有效请使用 Use(dbkey)
        /// </summary>
        /// <param name="dbkey"></param>
        /// <returns></returns>
        public IFreeSql Change(TDBKey dbkey)
        {
            var oldkey = _dbkey;
            if (_distributeTraceEnable && object.Equals(dbkey, oldkey) == false) _distributedTraceCall($"数据库切换[Change] {oldkey} -> {dbkey}");
            _dbkeyCurrent.Value = dbkey;
            return new FreeSqlCloundSnapshot<TDBKey>(this, dbkey, () => _dbkeyCurrent.Value = oldkey);
        }
        /// <summary>
        /// 临时使用数据库（单次有效）
        /// </summary>
        /// <param name="dbkey"></param>
        /// <returns></returns>
        public IFreeSql Use(TDBKey dbkey)
        {
            var oldkey = _dbkey;
            if (_distributeTraceEnable && object.Equals(dbkey, oldkey) == false) _distributedTraceCall($"数据库使用[Use] {dbkey}");
            return new FreeSqlCloundSnapshot<TDBKey>(this, dbkey, null);
        }
        internal IFreeSql GetBySnapshot(TDBKey dbkey)
        {
            return _ib.Get(dbkey);
        }

        public bool RemoveRegister(TDBKey dbkey) => _ib.TryRemove(dbkey, false);
        public bool ExistsRegister(TDBKey dbkey) => _ib.Exists(dbkey);
        public FreeSqlCloud<TDBKey> Register(TDBKey dbkey, Func<IFreeSql> create, TimeSpan? idle = null)
        {
            if (idle == null || idle <= TimeSpan.Zero) idle = TimeSpan.FromMinutes(3);
            if (_ib.TryRegister(dbkey, create, idle.Value))
            {
                if (!string.IsNullOrWhiteSpace(DistributeKey))
                {
                    var orm = _ib.Get(dbkey);
                    orm.Aop.ConfigEntity += (_, e) =>
                    {
                        if (e.EntityType == typeof(UnitInvokedInfo)) e.ModifyResult.Name = $"unit_invoked_{DistributeKey}";
                    };
                    orm.CodeFirst.SyncStructure<UnitInvokedInfo>(); //StartTcc(tid).Then<TccUnit1>(DbEnum.db2, null) 幂等判断表

                    //orm.CodeFirst.ConfigEntity<SagaUnitInvokeInfo>(a => a.Name($"saga_{DistributeKey}_unit_invoke"));
                    //orm.CodeFirst.SyncStructure<SagaUnitInvokeInfo>();
                }
                if (_ib.GetKeys().Length == 1)
                {
                    _dbkeyMaster = dbkey;
                    _dbkeyCurrent.Value = dbkey;
					if (!string.IsNullOrWhiteSpace(DistributeKey))
                    {
                        if (_distributeTraceEnable) _distributedTraceCall($"{dbkey} 注册成功, 并存储 TCC/SAGA 事务相关数据");
                        _scheduler = new FreeSchedulerBuilder()
                            .OnExecuting(task => { })
                            .Build();

                        _ormMaster.Aop.ConfigEntity += (_, e) =>
                        {
                            if (e.EntityType == typeof(TccMasterInfo)) e.ModifyResult.Name = $"tcc_{DistributeKey}";
                            if (e.EntityType == typeof(TccUnitInfo)) e.ModifyResult.Name = $"tcc_{DistributeKey}_unit";
                            if (e.EntityType == typeof(SagaMasterInfo)) e.ModifyResult.Name = $"saga_{DistributeKey}";
                            if (e.EntityType == typeof(SagaUnitInfo)) e.ModifyResult.Name = $"saga_{DistributeKey}_unit";
                        };
                        _ormMaster.CodeFirst.SyncStructure<TccMasterInfo>();
                        _ormMaster.CodeFirst.SyncStructure<TccUnitInfo>();
                        _ormMaster.CodeFirst.SyncStructure<SagaMasterInfo>();
                        _ormMaster.CodeFirst.SyncStructure<SagaUnitInfo>();

                        #region 加载历史未未成 TCC 事务
                        var tccPendings = _ormMaster.Select<TccMasterInfo>()
                            .Where(a => a.Status == TccMasterStatus.Pending && a.RetryCount < a.MaxRetryCount)
                            .OrderBy(a => a.CreateTime)
                            .ToList();
                        foreach (var pending in tccPendings)
                            _scheduler.AddTempTask(TimeSpan.FromSeconds(pending.RetryInterval), TccMaster<TDBKey>.GetTempTask(this, pending.Tid, pending.Title, pending.RetryInterval));
                        if (_distributeTraceEnable) _distributedTraceCall($"成功加载历史未完成 TCC 事务 {tccPendings.Count} 个");
                        #endregion

                        #region 加载历史未未成 SAGA 事务
                        var sagaPendings = _ormMaster.Select<SagaMasterInfo>()
                            .Where(a => a.Status == SagaMasterStatus.Pending && a.RetryCount < a.MaxRetryCount)
                            .OrderBy(a => a.CreateTime)
                            .ToList();
                        foreach (var pending in sagaPendings)
                            _scheduler.AddTempTask(TimeSpan.FromSeconds(pending.RetryInterval), SagaMaster<TDBKey>.GetTempTask(this, pending.Tid, pending.Title, pending.RetryInterval));
                        if (_distributeTraceEnable) _distributedTraceCall($"成功加载历史未完成 SAGA 事务 {sagaPendings.Count} 个");
                        #endregion
                    }
                }
            }
            return this;
        }
        public TccMaster<TDBKey> StartTcc(string tid, string title, TccOptions options = null)
        {
            if (string.IsNullOrWhiteSpace(DistributeKey)) throw new Exception("未开启 TCC 事务，请检查 ctor 构造方法");
            if (_scheduler.QuantityTempTask > 10_0000)
            {
                if (_distributeTraceEnable) _distributedTraceCall($"TCC({tid}, {title}) 系统繁忙创建失败, 当前未完成事务 {_scheduler.QuantityTempTask} 个");
                throw new Exception($"TCC({tid}, {title}) 系统繁忙创建失败, 当前未完成事务 {_scheduler.QuantityTempTask} 个");
            }
            return new TccMaster<TDBKey>(this, tid, title, options);
        }
        public SagaMaster<TDBKey> StartSaga(string tid, string title, SagaOptions options = null)
        {
            if (string.IsNullOrWhiteSpace(DistributeKey)) throw new Exception("未开启 TCC 事务，请检查 ctor 构造方法");
            if (_scheduler.QuantityTempTask > 10_0000)
            {
                if (_distributeTraceEnable) _distributedTraceCall($"SAGA({tid}, {title}) 系统繁忙创建失败, 当前未完成事务 {_scheduler.QuantityTempTask} 个");
                throw new Exception($"SAGA({tid}, {title}) 系统繁忙创建失败, 当前未完成事务 {_scheduler.QuantityTempTask} 个");
            }
            return new SagaMaster<TDBKey>(this, tid, title, options);
        }

        public IAdo Ado => _ormCurrent.Ado;
        public IAop Aop => _ormCurrent.Aop;
        public ICodeFirst CodeFirst => _ormCurrent.CodeFirst;
        public IDbFirst DbFirst => _ormCurrent.DbFirst;
        public GlobalFilter GlobalFilter => _ormCurrent.GlobalFilter;
        public void Dispose()
        {
            if (_distributeTraceEnable && _scheduler != null) _distributedTraceCall($"准备释放, 当前未完成事务 {_scheduler.QuantityTempTask} 个");
            _scheduler?.Dispose();
            _ib.Dispose();
            if (_distributeTraceEnable) _distributedTraceCall($"成功释放");
        }

        public void Transaction(Action handler) => _ormCurrent.Transaction(handler);
        public void Transaction(IsolationLevel isolationLevel, Action handler) => _ormCurrent.Transaction(isolationLevel, handler);

        IFreeSql GetCrudOrm(string methodName, Type entityType)
        {
            if (EntitySteering != null)
            {
                var args = new EntitySteeringEventArgs
                {
                    MethodName = methodName,
                    EntityType = entityType,
                    AvailableDBKeys = _ib.GetKeys(a => a == null || a.Ado.MasterPool.IsAvailable),
                    _dbkey = _dbkey
                };
                EntitySteering(this, args);
                if (args._dbkeyChanged) return _ib.Get(args.DBKey);
            }
            return _ormCurrent;
        }

        public ISelect<T1> Select<T1>() where T1 : class
        {
            return GetCrudOrm(nameof(Select), typeof(T1)).Select<T1>();
        }
        public ISelect<T1> Select<T1>(object dywhere) where T1 : class => Select<T1>().WhereDynamic(dywhere);

        public IDelete<T1> Delete<T1>() where T1 : class
        {
            return GetCrudOrm(nameof(Delete), typeof(T1)).Delete<T1>();
        }
        public IDelete<T1> Delete<T1>(object dywhere) where T1 : class => Delete<T1>().WhereDynamic(dywhere);

        public IUpdate<T1> Update<T1>() where T1 : class
        {
            return GetCrudOrm(nameof(Update), typeof(T1)).Update<T1>();
        }
        public IUpdate<T1> Update<T1>(object dywhere) where T1 : class => Update<T1>().WhereDynamic(dywhere);

        public IInsert<T1> Insert<T1>() where T1 : class
        {
            return GetCrudOrm(nameof(Insert), typeof(T1)).Insert<T1>();
        }
        public IInsert<T1> Insert<T1>(T1 source) where T1 : class => Insert<T1>().AppendData(source);
        public IInsert<T1> Insert<T1>(T1[] source) where T1 : class => Insert<T1>().AppendData(source);
        public IInsert<T1> Insert<T1>(List<T1> source) where T1 : class => Insert<T1>().AppendData(source);
        public IInsert<T1> Insert<T1>(IEnumerable<T1> source) where T1 : class => Insert<T1>().AppendData(source);

        public IInsertOrUpdate<T1> InsertOrUpdate<T1>() where T1 : class
        {
            return GetCrudOrm(nameof(InsertOrUpdate), typeof(T1)).InsertOrUpdate<T1>();
        }

    }
}
