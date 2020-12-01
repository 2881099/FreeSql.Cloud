using FreeSql.Internal;
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
        internal IdleBus<IFreeSql> _ib = new IdleBus<IFreeSql>();
        internal IdleScheduler.Scheduler _scheduer;

        public FreeSqlCloud Change(string name)
        {
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
                    _scheduer = new IdleScheduler.Scheduler(new IdleScheduler.TaskHandlers.FreeSqlHandler(_orm));
                    _master.CodeFirst.SyncStructure<TccMaster>();
                }
                _ib.Get(name).CodeFirst.SyncStructure<TccTask>();
            }
            return this;
        }

        public TccFluent StartTcc(string tid) => new TccFluent(this, tid);

        public IAdo Ado => _orm.Ado;
        public IAop Aop => _orm.Aop;
        public ICodeFirst CodeFirst => _orm.CodeFirst;
        public IDbFirst DbFirst => _orm.DbFirst;
        public GlobalFilter GlobalFilter => _orm.GlobalFilter;
        public void Dispose()
        {
            _ib.Dispose();
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
