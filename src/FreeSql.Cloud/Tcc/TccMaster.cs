using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace FreeSql.Cloud.Tcc
{
    public partial class TccMaster<TDBKey>
    {
        FreeSqlCloud<TDBKey> _cloud;
        string _tid;
        string _title;
        TccOptions _options;
        List<TccUnitInfo> _thenUnitInfos = new List<TccUnitInfo>();
        List<ITccUnit> _thenUnits = new List<ITccUnit>();

        internal TccMaster(FreeSqlCloud<TDBKey> cloud, string tid, string title, TccOptions options)
        {
            if (string.IsNullOrWhiteSpace(tid)) throw new ArgumentNullException(nameof(tid));
            _cloud = cloud;
            _tid = tid;
            _title = title;
            if (options == null) options = new TccOptions();
            _options = new TccOptions
            {
                MaxRetryCount = options.MaxRetryCount,
                RetryInterval = options.RetryInterval
            };
        }

        /// <summary>
        /// 编排分布式事务单元<para></para>
        /// * Try/Confirm/Cancel 使用 Orm 属性统一了事务；<para></para>
        /// * Confirm/Cancel 内部已经过滤了重复执行；
        /// </summary>
        /// <typeparam name="TUnit"></typeparam>
        /// <param name="dbkey">选择数据库，Try/Confirm/Cancel 使用 Orm 属性统一了事务，并且内部处理了幂等操作</param>
        /// <param name="state">无状态数据</param>
        /// <returns></returns>
        public TccMaster<TDBKey> Then<TUnit>(TDBKey dbkey, object state = null) where TUnit : ITccUnit => Then(typeof(TUnit), dbkey, true, state);

        TccMaster<TDBKey> Then(Type tccUnitType, TDBKey dbkey, bool isdbkey, object state)
        {
            if (tccUnitType == null) throw new ArgumentNullException(nameof(tccUnitType));
            var unitTypeBase = typeof(TccUnit<>);
            if (state == null && tccUnitType.BaseType.GetGenericTypeDefinition() == typeof(TccUnit<>)) unitTypeBase = unitTypeBase.MakeGenericType(tccUnitType.BaseType.GetGenericArguments()[0]);
            else unitTypeBase = unitTypeBase.MakeGenericType(state.GetType());
            if (unitTypeBase.IsAssignableFrom(tccUnitType) == false) throw new ArgumentException($"{tccUnitType.DisplayCsharp(false)} 必须继承 {unitTypeBase.DisplayCsharp(false)}");
            var unitCtors = tccUnitType.GetConstructors();
            if (unitCtors.Length != 1 && unitCtors[0].GetParameters().Length > 0) throw new ArgumentException($"{tccUnitType.FullName} 不能使用构造函数");

            var unitTypeConved = Type.GetType(tccUnitType.AssemblyQualifiedName);
            if (unitTypeConved == null) throw new ArgumentException($"{tccUnitType.FullName} 无效");
            var unit = unitTypeConved.CreateInstanceGetDefaultValue() as ITccUnit;
            (unit as ITccUnitSetter)?.SetState(state);
            _thenUnits.Add(unit);
            _thenUnitInfos.Add(new TccUnitInfo
            {
                Description = unitTypeConved.GetDescription(),
                Index = _thenUnitInfos.Count + 1,
                Stage = TccUnitStage.Try,
                State = state == null ? null : Newtonsoft.Json.JsonConvert.SerializeObject(state),
                StateTypeName = state?.GetType().AssemblyQualifiedName,
                Tid = _tid,
                TypeName = tccUnitType.AssemblyQualifiedName,
            });
            if (isdbkey) _thenUnitInfos.Last().DbKey = dbkey.ToString();
            return this;
        }

        /// <summary>
        /// 执行 TCC 事务<para></para>
        /// 返回值 true: 事务完成并且 Confirm 成功<para></para>
        /// 返回值 false: 事务完成但是 Cancel 已取消<para></para>
        /// 返回值 null: 等待最终一致性
        /// </summary>
        /// <returns></returns>
#if net40
        public bool? Execute()
#else
        async public Task<bool?> ExecuteAsync()
#endif
        {
            if (_cloud._ib.Quantity == 0) throw new ArgumentException($"必须注册可用的数据库");
            var units = _thenUnits.ToArray();

            var masterInfo = new TccMasterInfo
            {
                Tid = _tid,
                Title = _title,
                Total = _thenUnitInfos.Count,
                Status = TccMasterStatus.Pending,
                RetryCount = 0,
                MaxRetryCount = _options.MaxRetryCount,
                RetryInterval = (int)_options.RetryInterval.TotalSeconds,
            };
#if net40
            _cloud._ormMaster.Insert(masterInfo).ExecuteAffrows();
#else
            await _cloud._ormMaster.Insert(masterInfo).ExecuteAffrowsAsync();
#endif
            if (_cloud._distributeTraceEnable) _cloud._distributedTraceCall($"TCC ({masterInfo.Tid}, {masterInfo.Title}) Created successful, retry count: {_options.MaxRetryCount}, interval: {_options.RetryInterval.TotalSeconds}S");
            var unitInfos = new List<TccUnitInfo>();

            Exception unitException = null;
            for (var idx = 0; idx < _thenUnitInfos.Count; idx++)
            {
                try
                {
                    var ormMaster = _cloud._ormMaster;
#if net40
                    using (var conn = ormMaster.Ado.MasterPool.Get())
#else
                    using (var conn = await ormMaster.Ado.MasterPool.GetAsync())
#endif
                    {
                        var tran = conn.Value.BeginTransaction();
                        var tranIsCommited = false;
                        try
                        {
                            (units[idx] as ITccUnitSetter)?.SetUnit(_thenUnitInfos[idx]);
                            var tranOrm = FreeSqlTransaction.Create(ormMaster, () => tran);
#if net40
                            tranOrm.Insert(_thenUnitInfos[idx]).ExecuteAffrows();
                            InvokeUnit(_cloud, _thenUnitInfos[idx], units[idx], InvokeUnitMethod.Try, tranOrm);
#else
                            await tranOrm.Insert(_thenUnitInfos[idx]).ExecuteAffrowsAsync();
                            await InvokeUnitAsync(_cloud, _thenUnitInfos[idx], units[idx], InvokeUnitMethod.Try, tranOrm);
#endif
                            tran.Commit();
                            tranIsCommited = true;
                            unitInfos.Add(_thenUnitInfos[idx]);
                        }
                        finally
                        {
                            if (tranIsCommited == false)
                                tran.Rollback();
                        }
                    }
                    if (_cloud._distributeTraceEnable) _cloud._distributedTraceCall($"TCC ({masterInfo.Tid}, {masterInfo.Title}) Unit{_thenUnitInfos[idx].Index}{(string.IsNullOrWhiteSpace(_thenUnitInfos[idx].Description) ? "" : $"({_thenUnitInfos[idx].Description})")} TRY successful\r\n    State: {_thenUnitInfos[idx].State}\r\n    Type:  {_thenUnitInfos[idx].TypeName}");
                }
                catch (Exception ex)
                {
                    unitException = ex.InnerException?.InnerException ?? ex.InnerException ?? ex;
                    if (_cloud._distributeTraceEnable) _cloud._distributedTraceCall($"TCC ({masterInfo.Tid}, {masterInfo.Title}) Unit{_thenUnitInfos[idx].Index}{(string.IsNullOrWhiteSpace(_thenUnitInfos[idx].Description) ? "" : $"({_thenUnitInfos[idx].Description})")} TRY failed, ready to CANCEL, -ERR {unitException.Message}\r\n    State: {_thenUnitInfos[idx].State}\r\n    Type:  {_thenUnitInfos[idx].TypeName}");
                    break;
                }
            }
#if net40
            return ConfimCancel(_cloud, masterInfo, unitInfos, units, true);
#else
            return await ConfimCancelAsync(_cloud, masterInfo, unitInfos, units,  true);
#endif
        }


        static void SetTccState(ITccUnit unit, TccUnitInfo unitInfo)
        {
            if (string.IsNullOrWhiteSpace(unitInfo.StateTypeName)) return;
            if (unitInfo.State == null) return;
            var stateType = Type.GetType(unitInfo.StateTypeName);
            if (stateType == null) return;
            (unit as ITccUnitSetter)?.SetState(Newtonsoft.Json.JsonConvert.DeserializeObject(unitInfo.State, stateType));
        }


#if net40
        static void ConfimCancel(FreeSqlCloud<TDBKey> cloud, string tid, bool retry)
        {
            var masterInfo = cloud._ormMaster.Select<TccMasterInfo>().Where(a => a.Tid == tid && a.Status == TccMasterStatus.Pending && a.RetryCount <= a.MaxRetryCount).First();
            if (masterInfo == null) return;
            var unitInfos = cloud._ormMaster.Select<TccUnitInfo>().Where(a => a.Tid == tid).OrderBy(a => a.Index).ToList();
            var units = LocalGetUnits();
            ConfimCancel(cloud, masterInfo, unitInfos, units, retry);
#else
        async static Task ConfimCancelAsync(FreeSqlCloud<TDBKey> cloud, string tid, bool retry)
        {
            var masterInfo = await cloud._ormMaster.Select<TccMasterInfo>().Where(a => a.Tid == tid && a.Status == TccMasterStatus.Pending && a.RetryCount <= a.MaxRetryCount).FirstAsync();
            if (masterInfo == null) return;
            var unitInfos = await cloud._ormMaster.Select<TccUnitInfo>().Where(a => a.Tid == tid).OrderBy(a => a.Index).ToListAsync();
            var units = LocalGetUnits();
            await ConfimCancelAsync(cloud, masterInfo, unitInfos, units, retry);
#endif
            ITccUnit[] LocalGetUnits() => unitInfos.Select(unitInfo =>
            {
                try
                {
                    var unitTypeDefault = Type.GetType(unitInfo.TypeName).CreateInstanceGetDefaultValue() as ITccUnit;
                    if (unitTypeDefault == null)
                    {
                        if (cloud._distributeTraceEnable) cloud._distributedTraceCall($"TCC ({masterInfo.Tid}, {masterInfo.Title}) Data error, cannot create as ITccUnit, {unitInfo.TypeName}");
                        throw new ArgumentException($"TCC ({masterInfo.Tid}, {masterInfo.Title}) Data error, cannot create as ITccUnit, {unitInfo.TypeName}");
                    }
                    return unitTypeDefault;
                }
                catch
                {
                    if (cloud._distributeTraceEnable) cloud._distributedTraceCall($"TCC ({masterInfo.Tid}, {masterInfo.Title}) Data error, cannot create as ITccUnit, {unitInfo.TypeName}");
                    throw new ArgumentException($"TCC ({masterInfo.Tid}, {masterInfo.Title}) Data error, cannot create as ITccUnit, {unitInfo.TypeName}");
                }
            })
            .ToArray();
        }

#if net40
        static bool? ConfimCancel(FreeSqlCloud<TDBKey> cloud, TccMasterInfo masterInfo, List<TccUnitInfo> unitInfos, ITccUnit[] units, bool retry)
#else
        async static Task<bool?> ConfimCancelAsync(FreeSqlCloud<TDBKey> cloud, TccMasterInfo masterInfo, List<TccUnitInfo> unitInfos, ITccUnit[] units, bool retry)
#endif
        {
            var isConfirm = unitInfos.Count == masterInfo.Total;
            var successCount = 0;
            for (var idx = masterInfo.Total - 1; idx >= 0; idx--)
            {
                var unitInfo = unitInfos.Where(tt => tt.Index == idx + 1 && tt.Stage == TccUnitStage.Try).FirstOrDefault();
                try
                {
                    if (unitInfo != null)
                    {
                        if ((units[idx] as ITccUnitSetter)?.StateIsValued != true)
                            SetTccState(units[idx], unitInfo);
                        var ormMaster = cloud._ormMaster;
#if net40
                        using (var conn = ormMaster.Ado.MasterPool.Get())
#else
                        using (var conn = await ormMaster.Ado.MasterPool.GetAsync())
#endif
                        {
                            var tran = conn.Value.BeginTransaction();
                            var tranIsCommited = false;
                            try
                            {
                                var tranOrm = FreeSqlTransaction.Create(ormMaster, () => tran);
                                (units[idx] as ITccUnitSetter)?.SetUnit(unitInfo);
                                var update = tranOrm.Update<TccUnitInfo>()
                                    .Where(a => a.Tid == masterInfo.Tid && a.Index == idx + 1 && a.Stage == TccUnitStage.Try)
                                    .Set(a => a.Stage, isConfirm ? TccUnitStage.Confirm : TccUnitStage.Cancel);
#if net40
                                if (update.ExecuteAffrows() == 1)
                                {
                                    if (isConfirm) InvokeUnit(cloud, unitInfo, units[idx], InvokeUnitMethod.Confirm, tranOrm);
                                    else InvokeUnit(cloud, unitInfo, units[idx], InvokeUnitMethod.Cancel, tranOrm);
                                }
#else
                                if (await update.ExecuteAffrowsAsync() == 1)
                                {
                                    if (isConfirm) await InvokeUnitAsync(cloud, unitInfo, units[idx], InvokeUnitMethod.Confirm, tranOrm);
                                    else await InvokeUnitAsync(cloud, unitInfo, units[idx], InvokeUnitMethod.Cancel, tranOrm);
                                }
#endif
                                tran.Commit();
                                tranIsCommited = true;
                            }
                            finally
                            {
                                if (tranIsCommited == false)
                                    tran.Rollback();
                            }
                        }
                        if (cloud._distributeTraceEnable) cloud._distributedTraceCall($"TCC ({masterInfo.Tid}, {masterInfo.Title}) Unit{unitInfo.Index}{(string.IsNullOrWhiteSpace(unitInfo.Description) ? "" : $"({unitInfo.Description})")} {(isConfirm ? "CONFIRM" : "CANCEL")} successful{(masterInfo.RetryCount > 0 ? $" after {masterInfo.RetryCount} retries" : "")}\r\n    State: {unitInfo.State}\r\n    Type:  {unitInfo.TypeName}");
                    }
                    successCount++;
                }
                catch(Exception ex)
                {
                    if (unitInfo != null)
                        if (cloud._distributeTraceEnable) cloud._distributedTraceCall($"TCC ({masterInfo.Tid}, {masterInfo.Title}) Unit{unitInfo.Index}{(string.IsNullOrWhiteSpace(unitInfo.Description) ? "" : $"({unitInfo.Description})")} {(isConfirm ? "CONFIRM" : "CANCEL")} failed{(masterInfo.RetryCount > 0 ? $" after {masterInfo.RetryCount} retries" : "")}, -ERR {ex.Message}\r\n    State: {unitInfo.State}\r\n    Type:  {unitInfo.TypeName}");
                }
            }
            if (successCount == masterInfo.Total)
            {
                var update = cloud._ormMaster.Update<TccMasterInfo>()
                    .Where(a => a.Tid == masterInfo.Tid && a.Status == TccMasterStatus.Pending)
                    .Set(a => a.RetryCount + 1)
                    .Set(a => a.RetryTime == DateTime.UtcNow)
                    .Set(a => a.Status, isConfirm ? TccMasterStatus.Confirmed : TccMasterStatus.Canceled)
                    .Set(a => a.FinishTime == DateTime.UtcNow);
#if net40
                update.ExecuteAffrows();
#else
                await update.ExecuteAffrowsAsync();
#endif
                if (cloud._distributeTraceEnable) cloud._distributedTraceCall($"TCC ({masterInfo.Tid}, {masterInfo.Title}) Completed, all units {(isConfirm ? "CONFIRM" : "CANCEL")} successfully{(masterInfo.RetryCount > 0 ? $" after {masterInfo.RetryCount} retries" : "")}");
                return isConfirm;
            }
            else
            {
                var update = cloud._ormMaster.Update<TccMasterInfo>()
                    .Where(a => a.Tid == masterInfo.Tid && a.Status == TccMasterStatus.Pending && a.RetryCount < a.MaxRetryCount)
                    .Set(a => a.RetryCount + 1)
                    .Set(a => a.RetryTime == DateTime.UtcNow);
#if net40
                var affrows = update.ExecuteAffrows();
#else
                var affrows = await update.ExecuteAffrowsAsync();
#endif
                if (affrows == 1)
                {
                    if (retry)
                    {
                        //if (cloud.TccTraceEnable) cloud.OnTccTrace($"TCC ({tcc.Tid}, {tcc.Title}) Not completed, waiting to try again, current tasks {cloud._scheduler.QuantityTempTask}");
                        cloud._scheduler.AddTempTask(TimeSpan.FromSeconds(masterInfo.RetryInterval), GetTempTask(cloud, masterInfo.Tid, masterInfo.Title, masterInfo.RetryInterval));
                    }
                }
                else
                {
                    update = cloud._ormMaster.Update<TccMasterInfo>()
                        .Where(a => a.Tid == masterInfo.Tid && a.Status == TccMasterStatus.Pending)
                        .Set(a => a.Status, TccMasterStatus.ManualOperation);
#if net40
                    update.ExecuteAffrows();
#else
                    await update.ExecuteAffrowsAsync();
#endif
                    if (cloud._distributeTraceEnable) cloud._distributedTraceCall($"TCC ({masterInfo.Tid}, {masterInfo.Title}) Not completed, waiting for manual operation 【人工干预】");
                }
                return null;
            }
        }
        internal static Action GetTempTask(FreeSqlCloud<TDBKey> cloud, string tid, string title, int retryInterval)
        {
            return () =>
            {
                try
                {
#if net40
                    ConfimCancel(cloud, tid, true);
#else
                    ConfimCancelAsync(cloud, tid, true).Wait();
#endif
                }
                catch
                {
                    try
                    {
                        cloud._ormMaster.Update<TccMasterInfo>()
                            .Where(a => a.Tid == tid && a.Status == TccMasterStatus.Pending)
                            .Set(a => a.RetryCount + 1)
                            .Set(a => a.RetryTime == DateTime.UtcNow)
                            .ExecuteAffrows();
                    }
                    catch { }
                    //if (cloud.TccTraceEnable) cloud.OnTccTrace($"TCC ({tid}, {title}) Not completed, waiting to try again, current tasks {cloud._scheduler.QuantityTempTask}");
                    cloud._scheduler.AddTempTask(TimeSpan.FromSeconds(retryInterval), GetTempTask(cloud, tid, title, retryInterval));
                }
            };
        }
    }
}
