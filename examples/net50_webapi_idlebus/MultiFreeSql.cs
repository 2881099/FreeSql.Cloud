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
            var sel = _ormCurrent.Select<T1>();

            if (_ib.Quantity > 1) return sel; //多 key
            return sel.AsTable((type, oldname) => GetTableName(type, oldname));
        }

        public ISelect<T1> Select<T1>() where T1 : class
        {
            var sel = _ormCurrent.Select<T1>();

            if (_ib.Quantity > 1) return sel; //多 key
            return sel.AsTable((type, oldname) => GetTableName(type, oldname));
        }
        public ISelect<T1> Select<T1>(object dywhere) where T1 : class => Select<T1>().WhereDynamic(dywhere);

        public IDelete<T1> Delete<T1>() where T1 : class
        {
            var del = _ormCurrent.Delete<T1>();
            return del.AsTable(oldname => GetTableName((del as DeleteProvider<T1>)?._table.Type, oldname));
        }
        public IDelete<T1> Delete<T1>(object dywhere) where T1 : class => Delete<T1>().WhereDynamic(dywhere);

        public IUpdate<T1> Update<T1>() where T1 : class
        {
            var up = _ormCurrent.Update<T1>();
            return up.AsTable(oldname => GetTableName((up as UpdateProvider<T1>)?._table.Type, oldname));
        }
        public IUpdate<T1> Update<T1>(object dywhere) where T1 : class => Update<T1>().WhereDynamic(dywhere);

        public IInsert<T1> Insert<T1>() where T1 : class
        {
            var ins = _ormCurrent.Insert<T1>();
            return ins.AsTable(oldname => GetTableName((ins as InsertOrUpdateProvider<T1>)?._table.Type, oldname));
        }
        public IInsert<T1> Insert<T1>(T1 source) where T1 : class => Insert<T1>().AppendData(source);
        public IInsert<T1> Insert<T1>(T1[] source) where T1 : class => Insert<T1>().AppendData(source);
        public IInsert<T1> Insert<T1>(List<T1> source) where T1 : class => Insert<T1>().AppendData(source);
        public IInsert<T1> Insert<T1>(IEnumerable<T1> source) where T1 : class => Insert<T1>().AppendData(source);

        public IInsertOrUpdate<T1> InsertOrUpdate<T1>() where T1 : class
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
