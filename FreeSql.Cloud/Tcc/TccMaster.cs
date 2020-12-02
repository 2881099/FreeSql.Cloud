using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace FreeSql.Cloud
{
    public class TccOptions
    {
        public int MaxRetryCount { get; set; } = 30;
        public TimeSpan RetryInterval { get; set; } = TimeSpan.FromSeconds(60);
    }

    public class TccMaster
    {
        FreeSqlCloud _cloud;
        string _tid;
        string _title;
        TccOptions _options;
        List<TccUnitInfo> _thens = new List<TccUnitInfo>();
        List<ITccUnit> _thenTccs = new List<ITccUnit>();

        internal TccMaster(FreeSqlCloud cloud, string tid, string title, TccOptions options)
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

        public TccMaster Then(Type tccType, string cloudName) => Then<string>(tccType, cloudName, null);
        public TccMaster Then<TState>(Type tccType, string cloudName, TState state, IsolationLevel? isolationLevel = null)
        {
            if (tccType == null) throw new ArgumentNullException(nameof(tccType));
            var tccBaseType = typeof(TccUnit<>);
            if (state == null && tccType.BaseType.GetGenericTypeDefinition() == typeof(TccUnit<>)) tccBaseType = tccBaseType.MakeGenericType(tccType.BaseType.GetGenericArguments()[0]);
            else tccBaseType = tccBaseType.MakeGenericType(typeof(TState));
            if (tccBaseType.IsAssignableFrom(tccType) == false) throw new ArgumentException($"{tccType.FullName} 必须继承 TccBase<{typeof(TState).Name}>");
            var tccTypeCtors = tccType.GetConstructors();
            if (tccTypeCtors.Length != 1 && tccTypeCtors[0].GetParameters().Length > 0) throw new ArgumentException($"{tccType.FullName} 不能使用构造函数");
            if (string.IsNullOrWhiteSpace(cloudName)) throw new ArgumentNullException(nameof(cloudName));
            if (_cloud._ib.Exists(cloudName) == false) throw new KeyNotFoundException($"{cloudName} 不存在");

            var tccTypeConved = Type.GetType(tccType.AssemblyQualifiedName);
            if (tccTypeConved == null) throw new ArgumentException($"{tccType.FullName} 无效");
            var tccUnit = tccTypeConved.CreateInstanceGetDefaultValue() as ITccUnit;
            (tccUnit as ITccUnitSetter)?.SetState(state);
            _thenTccs.Add(tccUnit);
            _thens.Add(new TccUnitInfo
            {
                CloudName = cloudName,
                Description = tccTypeConved.GetCustomAttribute<DescriptionAttribute>()?.Description,
                Index = _thens.Count,
                IsolationLevel = isolationLevel,
                Stage = TccUnitStage.Try,
                State = state == null ? null : Newtonsoft.Json.JsonConvert.SerializeObject(state),
                StateTypeName = state?.GetType().AssemblyQualifiedName,
                Tid = _tid,
                TypeName = tccType.AssemblyQualifiedName,
            });
            return this;
        }

        async public Task<bool> ExecuteAsync()
        {
            if (string.IsNullOrEmpty(_cloud._masterName)) throw new ArgumentException($"必须注册可用的服务");
            var tccs = _thenTccs.ToArray();
            var tccOrms = _thens.Select(a => _cloud._ib.Get(a.CloudName)).ToArray();

            var tccMaster = new TccMasterInfo
            {
                Tid = _tid,
                Title = _title,
                Total = _thens.Count,
                Units = Newtonsoft.Json.JsonConvert.SerializeObject(_thens.Select(a => new { a.CloudName, a.TypeName })),
                Status = TccMasterStatus.Pending,
                RetryCount = 0,
                MaxRetryCount = _options.MaxRetryCount,
                RetryInterval = (int)_options.RetryInterval.TotalSeconds,
            };
            await _cloud._master.Insert(tccMaster).ExecuteAffrowsAsync();
            if (_cloud.TccTraceEnable) _cloud.OnTccTrace($"TCC({tccMaster.Tid}, {tccMaster.Title}) Created successful, retry count: {_options.MaxRetryCount}, interval: {_options.RetryInterval.TotalSeconds}S");
            var tccUnit = new List<TccUnitInfo>();

            Exception tccTryException = null;
            var tccTryIndex = 0;
            for (var idx = 0; idx < _thens.Count; idx++)
            {
                try
                {
                    using (var conn = await tccOrms[idx].Ado.MasterPool.GetAsync())
                    {
                        var tran = _thens[idx].IsolationLevel == null ? conn.Value.BeginTransaction() : conn.Value.BeginTransaction(_thens[idx].IsolationLevel.Value);
                        var tranIsCommited = false;
                        try
                        {
                            var fsql = TccOrm.Create(tccOrms[idx], () => tran);
                            fsql.Insert(_thens[idx]).ExecuteAffrows();
                            (tccs[idx] as ITccUnitSetter)?.SetOrm(fsql).SetUnit(_thens[idx]);

                            tccs[idx].Try();
                            tccUnit.Add(_thens[idx]);
                            tran.Commit();
                            tranIsCommited = true;
                        }
                        finally
                        {
                            if (tranIsCommited == false)
                                tran.Rollback();
                        }
                    }
                    if (_cloud.TccTraceEnable) _cloud.OnTccTrace($"TCC({tccMaster.Tid}, {tccMaster.Title}) Unit{_thens[idx].Index}{(string.IsNullOrWhiteSpace(_thens[idx].Description) ? "" : $"({_thens[idx].Description})")} TRY successful\r\n    State: {_thens[idx].State}\r\n    Type:  {_thens[idx].TypeName}");
                }
                catch (Exception ex)
                {
                    tccTryException = ex.InnerException?.InnerException ?? ex.InnerException ?? ex;
                    if (_cloud.TccTraceEnable) _cloud.OnTccTrace($"TCC({tccMaster.Tid}, {tccMaster.Title}) Unit{_thens[idx].Index}{(string.IsNullOrWhiteSpace(_thens[idx].Description) ? "" : $"({_thens[idx].Description})")} TRY failed, ready to CANCEL, -ERR {tccTryException.Message}\r\n    State: {_thens[idx].State}\r\n    Type:  {_thens[idx].TypeName}");
                    break;
                }
                tccTryIndex++;
            }
            return await ConfimCancelAsync(_cloud, tccMaster, tccUnit, tccs, tccOrms, true);
        }


        static void SetTccState(ITccUnit tcc, TccUnitInfo unit)
        {
            if (string.IsNullOrWhiteSpace(unit.StateTypeName)) return;
            if (unit.State == null) return;
            var stateType = Type.GetType(unit.StateTypeName);
            if (stateType == null) return;
            (tcc as ITccUnitSetter)?.SetState(Newtonsoft.Json.JsonConvert.DeserializeObject(unit.State, stateType));
        }
        //public static Task ConfimCancelAsync(FreeSqlCloud cloud, string tid) => ConfimCancelAsync(cloud, tid, false);
        async static Task ConfimCancelAsync(FreeSqlCloud cloud, string tid, bool retry)
        {
            var tccMaster = await cloud._master.Select<TccMasterInfo>().Where(a => a.Tid == tid && a.Status == TccMasterStatus.Pending && a.RetryCount <= a.MaxRetryCount).FirstAsync();
            if (tccMaster == null) return;
            var tccLites = Newtonsoft.Json.JsonConvert.DeserializeObject<TccUnitLiteInfo[]>(tccMaster.Units);
            if (tccLites?.Length != tccMaster.Total)
            {
                if (cloud.TccTraceEnable) cloud.OnTccTrace($"TCC({tccMaster.Tid}, {tccMaster.Title}) Data error, cannot deserialize Units");
                throw new ArgumentException($"TCC({tccMaster.Tid}, {tccMaster.Title}) Data error, cannot deserialize Units");
            }
            var tccs = tccLites.Select(tl =>
            {
                try
                {
                    var tccTypeDefault = Type.GetType(tl.TypeName).CreateInstanceGetDefaultValue() as ITccUnit;
                    if (tccTypeDefault == null)
                    {
                        if (cloud.TccTraceEnable) cloud.OnTccTrace($"TCC({tccMaster.Tid}, {tccMaster.Title}) Data error, cannot create as ITccUnit, {tl.TypeName}");
                        throw new ArgumentException($"TCC({tccMaster.Tid}, {tccMaster.Title}) Data error, cannot create as ITccUnit, {tl.TypeName}");
                    }
                    return tccTypeDefault;
                }
                catch
                {
                    if (cloud.TccTraceEnable) cloud.OnTccTrace($"TCC({tccMaster.Tid}, {tccMaster.Title}) Data error, cannot create as ITccUnit, {tl.TypeName}");
                    throw new ArgumentException($"TCC({tccMaster.Tid}, {tccMaster.Title}) Data error, cannot create as ITccUnit, {tl.TypeName}");
                }
            })
            .ToArray();
            var tccOrms = tccLites.Select(a => cloud._ib.Get(a.CloudName)).ToArray();
            var tccUnits = tccOrms.Distinct().SelectMany(z => z.Select<TccUnitInfo>().Where(a => a.Tid == tid).ToList()).OrderBy(a => a.Index).ToList();
            await ConfimCancelAsync(cloud, tccMaster, tccUnits, tccs, tccOrms, retry);
        }
        async static Task<bool> ConfimCancelAsync(FreeSqlCloud cloud, TccMasterInfo tccMaster, List<TccUnitInfo> tccUnits, ITccUnit[] tccs, IFreeSql[] tccOrms, bool retry)
        {
            var isConfirm = tccUnits.Count == tccMaster.Total;
            var successCount = 0;
            for (var idx = tccs.Length - 1; idx >= 0; idx--)
            {
                var tccUnit = tccUnits.Where(tt => tt.Index == idx && tt.Stage == TccUnitStage.Try).FirstOrDefault();
                try
                {
                    if (tccUnit != null)
                    {
                        if ((tccs[idx] as ITccUnitSetter)?.StateIsValued != true)
                            SetTccState(tccs[idx], tccUnit);
                        using (var conn = await tccOrms[idx].Ado.MasterPool.GetAsync())
                        {
                            var tran = tccUnit.IsolationLevel == null ? conn.Value.BeginTransaction() : conn.Value.BeginTransaction(tccUnit.IsolationLevel.Value);
                            var tranIsCommited = false;
                            try
                            {
                                var fsql = TccOrm.Create(tccOrms[idx], () => tran);
                                (tccs[idx] as ITccUnitSetter)?.SetOrm(fsql).SetUnit(tccUnit);

                                var affrows = await fsql.Update<TccUnitInfo>()
                                    .Where(a => a.Tid == tccMaster.Tid && a.Index == idx && a.Stage == TccUnitStage.Try)
                                    .Set(a => a.Stage, isConfirm ? TccUnitStage.Confirm : TccUnitStage.Cancel)
                                    .ExecuteAffrowsAsync();
                                if (affrows == 1)
                                {
                                    if (isConfirm) tccs[idx].Confirm();
                                    else tccs[idx].Cancel();
                                }
                                tran.Commit();
                                tranIsCommited = true;
                            }
                            finally
                            {
                                if (tranIsCommited == false)
                                    tran.Rollback();
                            }
                        }
                        if (cloud.TccTraceEnable) cloud.OnTccTrace($"TCC({tccMaster.Tid}, {tccMaster.Title}) Unit{tccUnit.Index}{(string.IsNullOrWhiteSpace(tccUnit.Description) ? "" : $"({tccUnit.Description})")}{(tccMaster.RetryCount > 0 ? $" retry again {tccMaster.RetryCount} times" : "")} {(isConfirm ? "CONFIRM" : "CANCEL")} successful\r\n    State: {tccUnit.State}\r\n    Type:  {tccUnit.TypeName}");
                    }
                    successCount++;
                }
                catch(Exception ex)
                {
                    if (tccUnit != null)
                        if (cloud.TccTraceEnable) cloud.OnTccTrace($"TCC({tccMaster.Tid}, {tccMaster.Title}) Unit{tccUnit.Index}{(string.IsNullOrWhiteSpace(tccUnit.Description) ? "" : $"({tccUnit.Description})")}{(tccMaster.RetryCount > 0 ? $" retry again {tccMaster.RetryCount} times" : "")} {(isConfirm ? "CONFIRM" : "CANCEL")} failed, -ERR {ex.Message}\r\n    State: {tccUnit.State}\r\n    Type:  {tccUnit.TypeName}");
                }
            }
            if (successCount == tccs.Length)
            {
                await cloud._master.Update<TccMasterInfo>()
                    .Where(a => a.Tid == tccMaster.Tid && a.Status == TccMasterStatus.Pending)
                    .Set(a => a.RetryCount + 1)
                    .Set(a => a.RetryTime == DateTime.UtcNow)
                    .Set(a => a.Status, isConfirm ? TccMasterStatus.Confirmed : TccMasterStatus.Canceled)
                    .Set(a => a.FinishTime == DateTime.UtcNow)
                    .ExecuteAffrowsAsync();
                if (cloud.TccTraceEnable) cloud.OnTccTrace($"TCC({tccMaster.Tid}, {tccMaster.Title}) End {(isConfirm ? "confirmed" : "canceld")},{(tccMaster.RetryCount > 0 ? $" retry again {tccMaster.RetryCount} times" : "")} {(isConfirm ? "CONFIRM" : "CANCEL")} successful");
                return true;
            }
            else
            {
                var affrows = await cloud._master.Update<TccMasterInfo>()
                    .Where(a => a.Tid == tccMaster.Tid && a.Status == TccMasterStatus.Pending && a.RetryCount < a.MaxRetryCount)
                    .Set(a => a.RetryCount + 1)
                    .Set(a => a.RetryTime == DateTime.UtcNow)
                    .ExecuteAffrowsAsync();
                if (affrows == 1)
                {
                    if (retry)
                    {
                        //if (cloud.TccTraceEnable) cloud.OnTccTrace($"TCC({tccMaster.Tid}, {tccMaster.Title}) Not completed, waiting to try again, current tasks {cloud._tccScheduler.QuantityTempTask}");
                        cloud._tccScheduler.AddTempTask(TimeSpan.FromSeconds(tccMaster.RetryInterval), GetTempTask(cloud, tccMaster.Tid, tccMaster.Title, tccMaster.RetryInterval));
                    }
                }
                else
                {
                    if (cloud.TccTraceEnable) cloud.OnTccTrace($"TCC({tccMaster.Tid}, {tccMaster.Title}) Not completed, waiting for manual operation 【人工干预】");
                }
                return false;
            }
        }
        internal static Action GetTempTask(FreeSqlCloud cloud, string tid, string title, int retryInterval)
        {
            return () =>
            {
                try
                {
                    ConfimCancelAsync(cloud, tid, true).Wait();
                }
                catch
                {
                    try
                    {
                        cloud._master.Update<TccMasterInfo>()
                            .Where(a => a.Tid == tid && a.Status == TccMasterStatus.Pending)
                            .Set(a => a.RetryCount + 1)
                            .Set(a => a.RetryTime == DateTime.UtcNow)
                            .ExecuteAffrows();
                    }
                    catch { }
                    //if (cloud.TccTraceEnable) cloud.OnTccTrace($"TCC({tid}, {title}) Not completed, waiting to try again, current tasks {cloud._tccScheduler.QuantityTempTask}");
                    cloud._tccScheduler.AddTempTask(TimeSpan.FromSeconds(retryInterval), GetTempTask(cloud, tid, title, retryInterval));
                }
            };
        }
    }
}
