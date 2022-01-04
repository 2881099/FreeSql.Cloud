using FreeSql;
using FreeSql.Internal;
using FreeSql.Internal.CommonProvider;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace net50_webapi_idlebus
{
    public class MultiFreeSql : MultiFreeSql<string> { }

    public partial class MultiFreeSql<TDBKey> : BaseDbProvider, IFreeSql
    {
        internal TDBKey _dbkeyMaster;
        internal AsyncLocal<TDBKey> _dbkeyCurrent = new AsyncLocal<TDBKey>();
        BaseDbProvider _ormMaster => _ib.Get(_dbkeyMaster) as BaseDbProvider;
        BaseDbProvider _ormCurrent => _ib.Get(object.Equals(_dbkeyCurrent.Value, default(TDBKey)) ? _dbkeyMaster : _dbkeyCurrent.Value) as BaseDbProvider;
        internal IdleBus<TDBKey, IFreeSql> _ib;

        public MultiFreeSql()
        {
            _ib = new IdleBus<TDBKey, IFreeSql>();
            _ib.Notice += (_, __) => { };
        }

        public override IAdo Ado => _ormCurrent.Ado;
        public override IAop Aop => _ormCurrent.Aop;
        public override ICodeFirst CodeFirst => _ormCurrent.CodeFirst;
        public override IDbFirst DbFirst => _ormCurrent.DbFirst;
        public override GlobalFilter GlobalFilter => _ormCurrent.GlobalFilter;
        public override void Dispose() => _ib.Dispose();

        public override CommonExpression InternalCommonExpression => _ormCurrent.InternalCommonExpression;
        public override CommonUtils InternalCommonUtils => _ormCurrent.InternalCommonUtils;

        public override ISelect<T1> CreateSelectProvider<T1>(object dywhere)
        {
            return _ormCurrent.CreateSelectProvider<T1>(dywhere).AsTable((type, oldname) =>
            {
                if (this.GlobalAsTable._applyTables.TryGetValue(type, out var list)) return list.FirstOrDefault();
                return oldname;
            });
        }
        public override IDelete<T1> CreateDeleteProvider<T1>(object dywhere)
        {
            var del = _ormCurrent.CreateDeleteProvider<T1>(dywhere);
            return del.AsTable(oldname =>
            {
                if (this.GlobalAsTable._applyTables.TryGetValue((del as DeleteProvider<T1>)?._table.Type, out var list)) return list.FirstOrDefault();
                return oldname;
            });
        }
        public override IUpdate<T1> CreateUpdateProvider<T1>(object dywhere)
        {
            var up = _ormCurrent.CreateUpdateProvider<T1>(dywhere);
            return up.AsTable(oldname =>
            {
                if (this.GlobalAsTable._applyTables.TryGetValue((up as UpdateProvider<T1>)?._table.Type, out var list)) return list.FirstOrDefault();
                return oldname;
            });
        }
        public override IInsert<T1> CreateInsertProvider<T1>()
        {
            var ins = _ormCurrent.CreateInsertProvider<T1>();
            return ins.AsTable(oldname =>
            {
                if (this.GlobalAsTable._applyTables.TryGetValue((ins as InsertProvider<T1>)?._table.Type, out var list)) return list.FirstOrDefault();
                return oldname;
            });
        }
        public override IInsertOrUpdate<T1> CreateInsertOrUpdateProvider<T1>()
        {
            var insup = _ormCurrent.CreateInsertOrUpdateProvider<T1>();
            return insup.AsTable(oldname =>
            {
                if (this.GlobalAsTable._applyTables.TryGetValue((insup as InsertOrUpdateProvider<T1>)?._table.Type, out var list)) return list.FirstOrDefault();
                return oldname;
            });
        }

        public GlobalAsTable GlobalAsTable { get; } = new GlobalAsTable();
    }
    public class GlobalAsTable
    {
        internal readonly Dictionary<Type, List<string>> _applyTables = new Dictionary<Type, List<string>>();
        public GlobalAsTable Apply(Type type, string tableName)
        {
            if (_applyTables.TryGetValue(type, out var list) == false) _applyTables.Add(type, list = new List<string>());
            list.Add(tableName);
            return this;
        }
    }

    public static class MultiFreeSqlExtensions
    {
        public static ISelect<T1> DisableGlobalAsTable<T1>(this ISelect<T1> that)
        {
            var s0p = (that as Select0Provider);
            if (s0p != null) s0p._tableRules.Clear();
            return that;
        }
        public static IInsert<T1> DisableGlobalAsTable<T1>(this IInsert<T1> that) where T1 : class
        {
            var s0p = (that as InsertProvider<T1>);
            if (s0p != null) s0p._tableRule = null;
            return that;
        }
        public static IUpdate<T1> DisableGlobalAsTable<T1>(this IUpdate<T1> that) where T1 : class
        {
            var s0p = (that as UpdateProvider<T1>);
            if (s0p != null) s0p._tableRule = null;
            return that;
        }
        public static IDelete<T1> DisableGlobalAsTable<T1>(this IDelete<T1> that) where T1 : class
        {
            var s0p = (that as DeleteProvider<T1>);
            if (s0p != null) s0p._tableRule = null;
            return that;
        }
        public static IInsertOrUpdate<T1> DisableGlobalAsTable<T1>(this IInsertOrUpdate<T1> that) where T1 : class
        {
            var s0p = (that as InsertOrUpdateProvider<T1>);
            if (s0p != null) s0p._tableRule = null;
            return that;
        }

        public static IFreeSql Change<TDBKey>(this IFreeSql fsql, TDBKey dbkey)
        {
            var multiFsql = fsql as MultiFreeSql<TDBKey>;
            if (multiFsql == null) throw new Exception("fsql 类型不是 MultiFreeSql<TDBKey>");
            multiFsql._dbkeyCurrent.Value = dbkey;
            return multiFsql;
        }

        public static IFreeSql Register<TDBKey>(this IFreeSql fsql, TDBKey dbkey, Func<IFreeSql> create)
        {
            var multiFsql = fsql as MultiFreeSql<TDBKey>;
            if (multiFsql == null) throw new Exception("fsql 类型不是 MultiFreeSql<TDBKey>");
            if (multiFsql._ib.TryRegister(dbkey, create))
                if (multiFsql._ib.GetKeys().Length == 1)
                    multiFsql._dbkeyMaster = dbkey;
            return multiFsql;
        }
    }
}
