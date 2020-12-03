using FreeSql.Internal;
using FreeSql.Internal.CommonProvider;
using FreeSql.Internal.Model;
using FreeSql.Internal.ObjectPool;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;

namespace FreeSql
{
    class FreeSqlTransaction : IFreeSql
    {
        readonly IFreeSql _orm;
        readonly Func<DbTransaction> _resolveTran;

        FreeSqlTransaction(IFreeSql fsql, Func<DbTransaction> resolveTran)
        {
            _orm = fsql;
            _resolveTran = resolveTran;
            //Ado = new ScopeTransactionAdo(_orm.Ado as AdoProvider, resolveTran);
        }

        public static FreeSqlTransaction Create(IFreeSql fsql, Func<DbTransaction> resolveTran)
        {
            if (fsql == null) return null;
            var scopedfsql = fsql as FreeSqlTransaction;
            if (scopedfsql == null) return new FreeSqlTransaction(fsql, resolveTran);
            return Create(scopedfsql._orm, resolveTran);
        }

        //class ScopeTransactionAdo : AdoProvider
        //{
        //    AdoProvider _ado;
        //    public ScopeTransactionAdo(AdoProvider ado, Func<DbTransaction> resolveTran) : base(ado.DataType, null, null)
        //    {
        //        _ado = ado;
        //        base.ResolveTransaction = resolveTran;
        //        base.ConnectionString = ado.ConnectionString;
        //        base.SlaveConnectionStrings = ado.SlaveConnectionStrings;
        //        base.Identifier = ado.Identifier;
        //    }
        //    public override object AddslashesProcessParam(object param, Type mapType, ColumnInfo mapColumn) => _ado.AddslashesProcessParam(param, mapType, mapColumn);
        //    public override DbCommand CreateCommand() => _ado.CreateCommand();
        //    public override DbParameter[] GetDbParamtersByObject(string sql, object obj) => _ado.GetDbParamtersByObject(sql, obj);
        //    public override void ReturnConnection(IObjectPool<DbConnection> pool, Object<DbConnection> conn, Exception ex) => _ado.ReturnConnection(pool, conn, ex);
        //}
        //public IAdo Ado { get; private set; }
        public IAdo Ado => throw new NotSupportedException("IFreeSql 对象被重写，支持事务且只能使用 CRUD 方法");
        public IAop Aop => throw new NotSupportedException("IFreeSql 对象被重写，支持事务且只能使用 CRUD 方法");
        public ICodeFirst CodeFirst => throw new NotSupportedException("IFreeSql 对象被重写，支持事务且只能使用 CRUD 方法");
        public IDbFirst DbFirst => throw new NotSupportedException("IFreeSql 对象被重写，支持事务且只能使用 CRUD 方法");
        public GlobalFilter GlobalFilter => throw new NotSupportedException("IFreeSql 对象被重写，支持事务且只能使用 CRUD 方法");
        public void Dispose() { }

        public void Transaction(Action handler) => throw new NotSupportedException("IFreeSql 对象被重写，支持事务且只能使用 CRUD 方法");
        public void Transaction(IsolationLevel isolationLevel, Action handler) => throw new NotSupportedException("IFreeSql 对象被重写，支持事务且只能使用 CRUD 方法");

        public ISelect<T1> Select<T1>() where T1 : class
        {
            return _orm.Select<T1>().WithTransaction(_resolveTran?.Invoke());
        }
        public ISelect<T1> Select<T1>(object dywhere) where T1 : class => Select<T1>().WhereDynamic(dywhere);

        public IDelete<T1> Delete<T1>() where T1 : class
        {
            return _orm.Delete<T1>().WithTransaction(_resolveTran?.Invoke());
        }
        public IDelete<T1> Delete<T1>(object dywhere) where T1 : class => Delete<T1>().WhereDynamic(dywhere);

        public IUpdate<T1> Update<T1>() where T1 : class
        {
            return _orm.Update<T1>().WithTransaction(_resolveTran?.Invoke());
        }
        public IUpdate<T1> Update<T1>(object dywhere) where T1 : class => Update<T1>().WhereDynamic(dywhere);

        public IInsert<T1> Insert<T1>() where T1 : class
        {
            return _orm.Insert<T1>().WithTransaction(_resolveTran?.Invoke());
        }
        public IInsert<T1> Insert<T1>(T1 source) where T1 : class => Insert<T1>().AppendData(source);
        public IInsert<T1> Insert<T1>(T1[] source) where T1 : class => Insert<T1>().AppendData(source);
        public IInsert<T1> Insert<T1>(List<T1> source) where T1 : class => Insert<T1>().AppendData(source);
        public IInsert<T1> Insert<T1>(IEnumerable<T1> source) where T1 : class => Insert<T1>().AppendData(source);

        public IInsertOrUpdate<T1> InsertOrUpdate<T1>() where T1 : class
        {
            return _orm.InsertOrUpdate<T1>().WithTransaction(_resolveTran?.Invoke());
        }
    }
}
