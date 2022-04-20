using FreeSql;
using FreeSql.Internal;
using FreeSql.Internal.CommonProvider;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace net50_webapi_tenant
{
    public partial class TenantFreeSql : BaseDbProvider, IFreeSql
    {
        internal string _dbkeyMaster;
        internal AsyncLocal<string> _dbkeyCurrent = new AsyncLocal<string>();
        IFreeSql _ormMaster => _ib.Get(_dbkeyMaster);
        IFreeSql _ormCurrent => _ib.Get(object.Equals(_dbkeyCurrent.Value, null) ? _dbkeyMaster : _dbkeyCurrent.Value);
        internal IdleBus<IFreeSql> _ib;

        public override IAdo Ado => _ormCurrent.Ado;
        public override IAop Aop => _ormCurrent.Aop;
        public override ICodeFirst CodeFirst => _ormCurrent.CodeFirst;
        public override IDbFirst DbFirst => _ormCurrent.DbFirst;
        public override GlobalFilter GlobalFilter => _ormCurrent.GlobalFilter;
        public override void Dispose() => _ib.Dispose();

        public override ISelect<T1> CreateSelectProvider<T1>(object dywhere)
        {
            var sel = (_ormCurrent as BaseDbProvider).CreateSelectProvider<T1>(dywhere);

            if (_ib.Quantity > 1) return sel; //多 key
            return sel.AsTable((type, oldname) => GetTableName(type, oldname));
        }
        public override IDelete<T1> CreateDeleteProvider<T1>(object dywhere)
        {
            var del = (_ormCurrent as BaseDbProvider).CreateDeleteProvider<T1>(dywhere);
            return del.AsTable(oldname => GetTableName((del as DeleteProvider<T1>)?._table.Type, oldname));
        }
        public override IInsert<T1> CreateInsertProvider<T1>()
        {
            var ins = (_ormCurrent as BaseDbProvider).CreateInsertProvider<T1>();
            return ins.AsTable(oldname => GetTableName((ins as InsertProvider<T1>)?._table.Type, oldname));
        }
        public override IUpdate<T1> CreateUpdateProvider<T1>(object dywhere)
        {
            var up = (_ormCurrent as BaseDbProvider).CreateUpdateProvider<T1>(dywhere);
            return up.AsTable(oldname => GetTableName((up as UpdateProvider<T1>)?._table.Type, oldname));
        }
        public override IInsertOrUpdate<T1> CreateInsertOrUpdateProvider<T1>()
        {
            var insup = _ormCurrent.InsertOrUpdate<T1>();
            return insup.AsTable(oldname => GetTableName((insup as InsertOrUpdateProvider<T1>)?._table.Type, oldname));
        }

        internal string GetTableName(Type type, string oldname)
        {
            if (_globalAsTables.TryGetValue(type, out var list))
                foreach (var item in list)
                {
                    var newname = item?.Invoke();
                    if (string.IsNullOrEmpty(newname) == false) return newname;
                }
            return oldname;
        }
        internal readonly Dictionary<Type, List<Func<string>>> _globalAsTables = new Dictionary<Type, List<Func<string>>>();
        public void RegisterGlobalAsTable(Type type, Func<string> tableName)
        {
            if (_globalAsTables.TryGetValue(type, out var list) == false) _globalAsTables.Add(type, list = new List<Func<string>>());
            list.Add(tableName);
        }
    }

    public static class TenantFreeSqlExtensions
    {
        public static IFreeSql Change(this IFreeSql fsql, string dbkey)
        {
            var tenantFsql = fsql as TenantFreeSql;
            if (tenantFsql == null) throw new Exception("fsql 类型不是 TenantFreeSql");
            tenantFsql._dbkeyCurrent.Value = dbkey;
            return tenantFsql;
        }

        public static IFreeSql Register(this IFreeSql fsql, string dbkey, Func<IFreeSql> create)
        {
            var tenantFsql = fsql as TenantFreeSql;
            if (tenantFsql == null) throw new Exception("fsql 类型不是 TenantFreeSql");
            if (tenantFsql._ib.TryRegister(dbkey, create))
                if (tenantFsql._ib.GetKeys().Length == 1)
                    tenantFsql._dbkeyMaster = dbkey;
            return tenantFsql;
        }
    }
}
