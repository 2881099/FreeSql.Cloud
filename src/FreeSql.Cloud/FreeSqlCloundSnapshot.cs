﻿using FreeSql.Internal;
using FreeSql.Internal.CommonProvider;
using FreeSql.Internal.Model;
using FreeSql.Internal.ObjectPool;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;

namespace FreeSql
{
    class FreeSqlCloundSnapshot<TDBKey> : IFreeSql
    {
        readonly FreeSqlCloud<TDBKey> _fsqlc;
        readonly TDBKey _current;
        readonly Action _dispose;

        public FreeSqlCloundSnapshot(FreeSqlCloud<TDBKey> fsqlc, TDBKey current, Action dispose)
        {
            _fsqlc = fsqlc;
            _current = current;
            _dispose = dispose;
        }

        public IAdo Ado => _fsqlc.GetBySnapshot(_current).Ado;
        public IAop Aop => _fsqlc.GetBySnapshot(_current).Aop;
        public ICodeFirst CodeFirst => _fsqlc.GetBySnapshot(_current).CodeFirst;
        public IDbFirst DbFirst => _fsqlc.GetBySnapshot(_current).DbFirst;
        public GlobalFilter GlobalFilter => _fsqlc.GetBySnapshot(_current).GlobalFilter;
        public void Dispose()
        {
            //示例
            //using (_fsqlc.Change("db2"))
            //{

            //}
            _dispose?.Invoke();
        }

        public void Transaction(Action handler) => _fsqlc.GetBySnapshot(_current).Transaction(handler);
        public void Transaction(IsolationLevel isolationLevel, Action handler) => _fsqlc.GetBySnapshot(_current).Transaction(isolationLevel, handler);

        public ISelect<T1> Select<T1>() where T1 : class => _fsqlc.GetBySnapshot(_fsqlc.GetEntitySteeringDBKey(nameof(Select), typeof(T1), _current)).Select<T1>();
        public ISelect<T1> Select<T1>(object dywhere) where T1 : class => Select<T1>().WhereDynamic(dywhere);

        public IDelete<T1> Delete<T1>() where T1 : class => _fsqlc.GetBySnapshot(_fsqlc.GetEntitySteeringDBKey(nameof(Delete), typeof(T1), _current)).Delete<T1>();
        public IDelete<T1> Delete<T1>(object dywhere) where T1 : class => Delete<T1>().WhereDynamic(dywhere);

        public IUpdate<T1> Update<T1>() where T1 : class => _fsqlc.GetBySnapshot(_fsqlc.GetEntitySteeringDBKey(nameof(Update), typeof(T1), _current)).Update<T1>();
        public IUpdate<T1> Update<T1>(object dywhere) where T1 : class => Update<T1>().WhereDynamic(dywhere);

        public IInsert<T1> Insert<T1>() where T1 : class => _fsqlc.GetBySnapshot(_fsqlc.GetEntitySteeringDBKey(nameof(Insert), typeof(T1), _current)).Insert<T1>();
        public IInsert<T1> Insert<T1>(T1 source) where T1 : class => Insert<T1>().AppendData(source);
        public IInsert<T1> Insert<T1>(T1[] source) where T1 : class => Insert<T1>().AppendData(source);
        public IInsert<T1> Insert<T1>(List<T1> source) where T1 : class => Insert<T1>().AppendData(source);
        public IInsert<T1> Insert<T1>(IEnumerable<T1> source) where T1 : class => Insert<T1>().AppendData(source);

        public IInsertOrUpdate<T1> InsertOrUpdate<T1>() where T1 : class => _fsqlc.GetBySnapshot(_fsqlc.GetEntitySteeringDBKey(nameof(InsertOrUpdate), typeof(T1), _current)).InsertOrUpdate<T1>();
    }
}
