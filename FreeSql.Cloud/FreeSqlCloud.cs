using FreeSql.Internal;
using IdleScheduler;
using System;
using System.Collections.Generic;
using System.Data;
using System.Threading;

namespace FreeSql.Cloud
{
    public partial class FreeSqlCloud : IFreeSql
    {
        internal string _masterName;
        internal AsyncLocal<string> _currentName = new AsyncLocal<string>();
        internal IFreeSql _orm => _ib.Get(_currentName.Value ?? _masterName);
        internal IFreeSql _master => _ib.Get(_masterName);
        internal IdleBus<IFreeSql> _ib;
        internal IdleScheduler.Scheduler _tccScheduler;
        internal string _tccMaster;
        public event EventHandler<string> TccTrace;
        internal bool TccTraceEnable => TccTrace != null;
        internal void OnTccTrace(string log)
        {
            TccTrace?.Invoke(this, $"{DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss")} 【{_tccMaster}】{log}");
        }

        public FreeSqlCloud(string tccMaster = "master")
        {
            _tccMaster = tccMaster;
            _ib = new IdleBus<IFreeSql>();
            _ib.Notice += (_, __) => { };
        }

        public FreeSqlCloud Change(string name)
        {
            if (TccTraceEnable) OnTccTrace($"数据库切换 {name}");
            _currentName.Value = name;
            return this;
        }

        public FreeSqlCloud Register(string name, Func<IFreeSql> create)
        {
            if (_ib.TryRegister(name, create))
            {
                if (string.IsNullOrEmpty(_masterName))
                {
                    _masterName = name;
                    if (TccTraceEnable) OnTccTrace($"{name} 注册成功, 它将存储 TCC 事务相关数据");
                    _tccScheduler = new IdleScheduler.Scheduler(new IdleScheduler.TaskHandlers.TestHandler());
                    _master.CodeFirst.ConfigEntity<TccMasterInfo>(a => a.Name($"tcc_{_tccMaster}"));
                    _master.CodeFirst.SyncStructure<TccMasterInfo>();

                    var pendings = _master.Select<TccMasterInfo>()
                        .Where(a => a.Status == TccMasterStatus.Pending && a.RetryCount < a.MaxRetryCount)
                        .OrderBy(a => a.CreateTime)
                        .ToList();
                    if (TccTraceEnable) OnTccTrace($"准备加载历史未完成 TCC 事务 {pendings.Count} 个");
                    foreach (var pending in pendings)
                        _tccScheduler.AddTempTask(TimeSpan.FromSeconds(pending.RetryInterval), TccMaster.GetTempTask(this, pending.Tid, pending.Title, pending.RetryInterval));
                    if (TccTraceEnable) OnTccTrace($"成功加载历史未完成 TCC 事务 {pendings.Count} 个");
                }
                var fsql = _ib.Get(name);
                fsql.CodeFirst.ConfigEntity<TccUnitInfo>(a => a.Name($"tcc_{_tccMaster}_unit"));
                fsql.CodeFirst.SyncStructure<TccUnitInfo>();
            }
            return this;
        }
        public TccMaster StartTcc(string tid, string title, TccOptions options = null)
        {
            if (_tccScheduler.QuantityTempTask > 10_0000)
            {
                if (TccTraceEnable) OnTccTrace($"TCC({tid}, {title}) 系统繁忙创建失败, 当前未完成事务 {_tccScheduler.QuantityTempTask} 个");
                throw new Exception($"TCC({tid}, {title}) 系统繁忙创建失败, 当前未完成事务 {_tccScheduler.QuantityTempTask} 个");
            }
            return new TccMaster(this, tid, title, options);
        }

        public IAdo Ado => _orm.Ado;
        public IAop Aop => _orm.Aop;
        public ICodeFirst CodeFirst => _orm.CodeFirst;
        public IDbFirst DbFirst => _orm.DbFirst;
        public GlobalFilter GlobalFilter => _orm.GlobalFilter;
        public void Dispose()
        {
            if (TccTraceEnable) OnTccTrace($"准备释放, 当前未完成事务 {_tccScheduler.QuantityTempTask} 个");
            _tccScheduler?.Dispose();
            _ib.Dispose();
            if (TccTraceEnable) OnTccTrace($"成功释放");
        }

        public void Transaction(Action handler) => _orm.Transaction(handler);
        public void Transaction(IsolationLevel isolationLevel, Action handler) => _orm.Transaction(isolationLevel, handler);

        public ISelect<T1> Select<T1>() where T1 : class
        {
            return _orm.Select<T1>();
        }
        public ISelect<T1> Select<T1>(object dywhere) where T1 : class => Select<T1>().WhereDynamic(dywhere);

        public IDelete<T1> Delete<T1>() where T1 : class
        {
            return _orm.Delete<T1>();
        }
        public IDelete<T1> Delete<T1>(object dywhere) where T1 : class => Delete<T1>().WhereDynamic(dywhere);

        public IUpdate<T1> Update<T1>() where T1 : class
        {
            return _orm.Update<T1>();
        }
        public IUpdate<T1> Update<T1>(object dywhere) where T1 : class => Update<T1>().WhereDynamic(dywhere);

        public IInsert<T1> Insert<T1>() where T1 : class
        {
            return _orm.Insert<T1>();
        }
        public IInsert<T1> Insert<T1>(T1 source) where T1 : class => Insert<T1>().AppendData(source);
        public IInsert<T1> Insert<T1>(T1[] source) where T1 : class => Insert<T1>().AppendData(source);
        public IInsert<T1> Insert<T1>(List<T1> source) where T1 : class => Insert<T1>().AppendData(source);
        public IInsert<T1> Insert<T1>(IEnumerable<T1> source) where T1 : class => Insert<T1>().AppendData(source);

        public IInsertOrUpdate<T1> InsertOrUpdate<T1>() where T1 : class
        {
            return _orm.InsertOrUpdate<T1>();
        }
    }
}
