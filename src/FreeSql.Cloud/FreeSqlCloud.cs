using FreeSql.Cloud.Saga;
using FreeSql.Cloud.Tcc;
using FreeSql.Internal;
using System;
using System.Collections.Generic;
using System.Data;
using System.Threading;

namespace FreeSql
{
    //public class FreeSqlCloud : FreeSqlCloud<string> { }
    public partial class FreeSqlCloud<TDBKey> : IFreeSql
    {
        public string DistributeKey { get; }
        public Action<string> DistributeTrace;

        internal TDBKey _dbkeyMaster;
#if net40
        internal ThreadLocal<TDBKey> _dbkeyCurrent = new ThreadLocal<TDBKey>();
#else
        internal AsyncLocal<TDBKey> _dbkeyCurrent = new AsyncLocal<TDBKey>();
#endif
        internal IFreeSql _ormMaster => _ib.Get(_dbkeyMaster);
        internal IFreeSql _ormCurrent => _ib.Get(object.Equals(_dbkeyCurrent.Value, default(TDBKey)) ? _dbkeyMaster : _dbkeyCurrent.Value);
        internal IdleBus<TDBKey, IFreeSql> _ib;
        internal IdleScheduler.Scheduler _scheduler;
        internal bool _distributeTraceEnable => DistributeTrace != null;
        internal void _distributedTraceCall(string log)
        {
            DistributeTrace?.Invoke($"{DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss")} 【{DistributeKey}】{log}");
        }

        public FreeSqlCloud(string distributeKey = "master")
        {
            DistributeKey = distributeKey;
            _ib = new IdleBus<TDBKey, IFreeSql>();
            _ib.Notice += (_, __) => { };
        }

        public IFreeSql Change(TDBKey dbkey)
        {
            if (_distributeTraceEnable) _distributedTraceCall($"数据库切换 {dbkey}");
            _dbkeyCurrent.Value = dbkey;
            return new FreeSqlCloundSnapshot<TDBKey>(this, dbkey);
        }
        internal IFreeSql GetBySnapshot(TDBKey dbkey)
        {
            _dbkeyCurrent.Value = dbkey;
            return this;
        }

        public FreeSqlCloud<TDBKey> Register(TDBKey dbkey, Func<IFreeSql> create)
        {
            if (_ib.TryRegister(dbkey, create))
            {
                if (_ib.GetKeys().Length == 1)
                {
                    _dbkeyMaster = dbkey;
                    if (_distributeTraceEnable) _distributedTraceCall($"{dbkey} 注册成功, 并存储 TCC/SAGA 事务相关数据");
                    _scheduler = new IdleScheduler.Scheduler(new IdleScheduler.TaskHandlers.TestHandler());

                    _ormMaster.CodeFirst.ConfigEntity<TccMasterInfo>(a => a.Name($"tcc_{DistributeKey}"));
                    _ormMaster.CodeFirst.SyncStructure<TccMasterInfo>();
                    _ormMaster.CodeFirst.ConfigEntity<TccUnitInfo>(a => a.Name($"tcc_{DistributeKey}_unit"));
                    _ormMaster.CodeFirst.SyncStructure<TccUnitInfo>();

                    _ormMaster.CodeFirst.ConfigEntity<SagaMasterInfo>(a => a.Name($"saga_{DistributeKey}"));
                    _ormMaster.CodeFirst.SyncStructure<SagaMasterInfo>();
                    _ormMaster.CodeFirst.ConfigEntity<SagaUnitInfo>(a => a.Name($"saga_{DistributeKey}_unit"));
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
            return this;
        }
        public TccMaster<TDBKey> StartTcc(string tid, string title, TccOptions options = null)
        {
            if (_scheduler.QuantityTempTask > 10_0000)
            {
                if (_distributeTraceEnable) _distributedTraceCall($"TCC({tid}, {title}) 系统繁忙创建失败, 当前未完成事务 {_scheduler.QuantityTempTask} 个");
                throw new Exception($"TCC({tid}, {title}) 系统繁忙创建失败, 当前未完成事务 {_scheduler.QuantityTempTask} 个");
            }
            return new TccMaster<TDBKey>(this, tid, title, options);
        }
        public SagaMaster<TDBKey> StartSaga(string tid, string title, SagaOptions options = null)
        {
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
            if (_distributeTraceEnable) _distributedTraceCall($"准备释放, 当前未完成事务 {_scheduler.QuantityTempTask} 个");
            _scheduler?.Dispose();
            _ib.Dispose();
            if (_distributeTraceEnable) _distributedTraceCall($"成功释放");
        }

        public void Transaction(Action handler) => _ormCurrent.Transaction(handler);
        public void Transaction(IsolationLevel isolationLevel, Action handler) => _ormCurrent.Transaction(isolationLevel, handler);

        public ISelect<T1> Select<T1>() where T1 : class
        {
            return _ormCurrent.Select<T1>();
        }
        public ISelect<T1> Select<T1>(object dywhere) where T1 : class => Select<T1>().WhereDynamic(dywhere);

        public IDelete<T1> Delete<T1>() where T1 : class
        {
            return _ormCurrent.Delete<T1>();
        }
        public IDelete<T1> Delete<T1>(object dywhere) where T1 : class => Delete<T1>().WhereDynamic(dywhere);

        public IUpdate<T1> Update<T1>() where T1 : class
        {
            return _ormCurrent.Update<T1>();
        }
        public IUpdate<T1> Update<T1>(object dywhere) where T1 : class => Update<T1>().WhereDynamic(dywhere);

        public IInsert<T1> Insert<T1>() where T1 : class
        {
            return _ormCurrent.Insert<T1>();
        }
        public IInsert<T1> Insert<T1>(T1 source) where T1 : class => Insert<T1>().AppendData(source);
        public IInsert<T1> Insert<T1>(T1[] source) where T1 : class => Insert<T1>().AppendData(source);
        public IInsert<T1> Insert<T1>(List<T1> source) where T1 : class => Insert<T1>().AppendData(source);
        public IInsert<T1> Insert<T1>(IEnumerable<T1> source) where T1 : class => Insert<T1>().AppendData(source);

        public IInsertOrUpdate<T1> InsertOrUpdate<T1>() where T1 : class
        {
            return _ormCurrent.InsertOrUpdate<T1>();
        }
    }
}
