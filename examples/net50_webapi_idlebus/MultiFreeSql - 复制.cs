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
            return _ormCurrent.CreateSelectProvider<T1>(dywhere);
        }
        public override IDelete<T1> CreateDeleteProvider<T1>(object dywhere)
        {
            return _ormCurrent.CreateDeleteProvider<T1>(dywhere);
        }
        public override IUpdate<T1> CreateUpdateProvider<T1>(object dywhere)
        {
            return _ormCurrent.CreateUpdateProvider<T1>(dywhere);
        }
        public override IInsert<T1> CreateInsertProvider<T1>()
        {
            return _ormCurrent.CreateInsertProvider<T1>();
        }
        public override IInsertOrUpdate<T1> CreateInsertOrUpdateProvider<T1>()
        {
            return _ormCurrent.CreateInsertOrUpdateProvider<T1>();
        }
    }

    public static class MultiFreeSqlExtensions
    {
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