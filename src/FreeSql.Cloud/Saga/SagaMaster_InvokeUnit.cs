using FreeSql.Cloud.Model;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace FreeSql.Cloud.Saga
{
    partial class SagaMaster<TDBKey>
    {

        enum InvokeUnitMethod { Commit, Cancel }

#if net40
        static void InvokeUnit(FreeSqlCloud<TDBKey> cloud, SagaUnitInfo unitInfo, ISagaUnit unit, InvokeUnitMethod method, IFreeSql masterTranOrm)
        {
            void LocalInvokeUnit(IFreeSql orm)
#else
        async static Task InvokeUnitAsync(FreeSqlCloud<TDBKey> cloud, SagaUnitInfo unitInfo, ISagaUnit unit, InvokeUnitMethod method, IFreeSql masterTranOrm)
        {
            async Task LocalInvokeUnitAsync(IFreeSql orm)
#endif

            {
                if (orm != null)
                {
                    try
                    {
                        switch (method)
                        {
                            case InvokeUnitMethod.Commit:
                            case InvokeUnitMethod.Cancel:
                                var insert = orm.Insert(new UnitInvokedInfo { Id = $"SAGA:{unitInfo.Tid},{unitInfo.Index},{method}" });
#if net40
                                insert.ExecuteAffrows();
#else
                                await insert.ExecuteAffrowsAsync();
#endif
                                break;
                        }
                    }
                    catch
                    {
                        return; //利用唯一约束做幂等判断，已经执行过
                    }
                }
#if net40
                switch (method)
                {
                    case InvokeUnitMethod.Commit: unit.Commit(); break;
                    case InvokeUnitMethod.Cancel: unit.Cancel(); break;
                }
#else
                switch (method)
                {
                    case InvokeUnitMethod.Commit: await unit.Commit(); break;
                    case InvokeUnitMethod.Cancel: await unit.Cancel(); break;
                }
#endif
            }

            if (string.IsNullOrWhiteSpace(unitInfo.DbKey))
            {
#if net40
                LocalInvokeUnit(null);
#else
                await LocalInvokeUnitAsync(null);
#endif
                return;
            }

            var dbkey = (TDBKey)typeof(TDBKey).FromObject(unitInfo.DbKey);
            var unitSetter = unit as ISagaUnitSetter;

            if (object.Equals(cloud._dbkeyMaster, dbkey))
            {
                try
                {
                    unitSetter?.SetOrm(masterTranOrm);

#if net40
                    LocalInvokeUnit(masterTranOrm);
#else
                    await LocalInvokeUnitAsync(masterTranOrm);
#endif
                }
                finally
                {
                    unitSetter?.SetOrm(null);
                }
                return;
            }


            var unitFsql = cloud.Use(dbkey);
#if net40
            using (var conn = unitFsql.Ado.MasterPool.Get())
#else
            using (var conn = await unitFsql.Ado.MasterPool.GetAsync())
#endif
            {
                var tran = conn.Value.BeginTransaction();
                var TranIsCommited = false;

                try
                {
                    var tranOrm = FreeSqlTransaction.Create(unitFsql, () => tran);
                    unitSetter?.SetOrm(tranOrm);

#if net40
                    LocalInvokeUnit(tranOrm);
#else
                    await LocalInvokeUnitAsync(tranOrm);
#endif

                    tran.Commit();
                    TranIsCommited = true;
                }
                finally
                {
                    unitSetter?.SetOrm(null);
                    if (TranIsCommited == false)
                        tran.Rollback();
                }
            }
        }
    }
}
