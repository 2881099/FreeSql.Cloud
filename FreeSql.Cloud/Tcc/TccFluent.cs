using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace FreeSql.Cloud
{
    public class TccFluent
    {
        FreeSqlCloud _cloud;
        string _tid;
        List<TccTask> _thens = new List<TccTask>();
        List<ITcc> _thenTccs = new List<ITcc>();

        IFreeSql _master => _cloud._master;
        string _masterName => _cloud._masterName;
        IdleBus<IFreeSql> _ib => _cloud._ib;
        IdleScheduler.Scheduler _scheduer => _cloud._scheduer;

        internal TccFluent(FreeSqlCloud cloud, string tid)
        {
            if (string.IsNullOrWhiteSpace(tid)) throw new ArgumentNullException(nameof(tid));
            _cloud = cloud;
            _tid = tid;
        }

        public TccFluent Then(Type tccType, string cloudName) => Then<string>(tccType, cloudName, null);
        public TccFluent Then<TState>(Type tccType, string cloudName, TState state, IsolationLevel? isolationLevel = null)
        {
            if (tccType == null) throw new ArgumentNullException(nameof(tccType));
            var tccBaseType = typeof(TccBase<>);
            if (state == null && tccType.BaseType.GetGenericTypeDefinition() == typeof(TccBase<>)) tccBaseType = tccBaseType.MakeGenericType(tccType.BaseType.GetGenericArguments()[0]);
            else tccBaseType = tccBaseType.MakeGenericType(typeof(TState));
            if (tccBaseType.IsAssignableFrom(tccType) == false) throw new ArgumentException($"{tccType.FullName} 必须继承 TccBase<{typeof(TState).Name}>");
            var tccTypeCtors = tccType.GetConstructors();
            if (tccTypeCtors.Length != 1 && tccTypeCtors[0].GetParameters().Length > 0) throw new ArgumentException($"{tccType.FullName} 不能使用构造函数");
            if (string.IsNullOrWhiteSpace(cloudName)) throw new ArgumentNullException(nameof(cloudName));
            if (_ib.Exists(cloudName) == false) throw new KeyNotFoundException($"{cloudName} 不存在");

            var tccTypeConved = Type.GetType(tccType.AssemblyQualifiedName);
            if (tccTypeConved == null) throw new ArgumentException($"{tccType.FullName} 无效");
            var tccTask = tccTypeConved.CreateInstanceGetDefaultValue() as ITcc;
            (tccTask as ITccSetter)?.SetState(state);
            _thenTccs.Add(tccTask);
            _thens.Add(new TccTask
            {
                CloudName = cloudName,
                Description = tccTypeConved.GetCustomAttribute<DescriptionAttribute>()?.Description,
                Index = _thens.Count,
                IsolationLevel = isolationLevel,
                Stage = TccTaskStage.Try,
                State = state == null ? null : Newtonsoft.Json.JsonConvert.SerializeObject(state),
                StateTypeName = state?.GetType().AssemblyQualifiedName,
                Tid = _tid,
                TypeName = tccType.AssemblyQualifiedName,
            });
            return this;
        }

        void SetTccState(ITcc tcc, TccTask tccTask)
        {
            if (string.IsNullOrWhiteSpace(tccTask.StateTypeName)) return;
            if (tccTask.State == null) return;
            var stateType = Type.GetType(tccTask.StateTypeName);
            if (stateType == null) return;
            (tcc as ITccSetter)?.SetState(Newtonsoft.Json.JsonConvert.DeserializeObject(tccTask.State, stateType));
        }

        async public Task<bool> ExecuteAsync()
        {
            if (string.IsNullOrEmpty(_masterName)) throw new ArgumentException($"必须注册可用的服务");
            var tccs = _thenTccs.ToArray();
            var tccOrms = _thens.Select(a => _ib.Get(a.CloudName)).ToArray();

            var tccMaster = new TccMaster
            {
                Tid = _tid,
                Total = _thens.Count,
                Tasks = Newtonsoft.Json.JsonConvert.SerializeObject(_thens.Select(a => new { a.CloudName, a.TypeName })),
                Status = TccMasterStatus.Pending,
                RetryCount = 0,
            };
            await _master.Insert(tccMaster).ExecuteAffrowsAsync();
            var tccTasks = new List<TccTask>();

            Exception tccTryException = null;
            var tccTryIndex = 0;
            for (var idx = 0; idx < _thens.Count; idx++)
            {
                try
                {
                    using (var uow = tccOrms[idx].CreateUnitOfWork())
                    {
                        uow.IsolationLevel = _thens[idx].IsolationLevel;
                        var fsql = TccOrm.Create(tccOrms[idx], () => uow.GetOrBeginTransaction());
                        fsql.Insert(_thens[idx]).ExecuteAffrows();
                        (tccs[idx] as ITccSetter)?.SetOrm(fsql).SetTccTask(_thens[idx]);

                        tccs[idx].Try();
                        tccTasks.Add(_thens[idx]);
                        uow.Commit();
                    }
                }
                catch (Exception ex)
                {
                    tccTryException = ex.InnerException?.InnerException ?? ex.InnerException ?? ex;
                    break;
                }
                tccTryIndex++;
            }
            return await ConfimCancelAsync(tccMaster, tccTasks, tccs, tccOrms, true);
        }

        public Task ConfimCancelAsync(string tid) => ConfimCancelAsync(tid, false);
        async Task ConfimCancelAsync(string tid, bool retry)
        {
            var tccMaster = await _master.Select<TccMaster>().Where(a => a.Tid == tid && a.Status == TccMasterStatus.Pending && a.RetryCount <= 30).FirstAsync();
            if (tccMaster == null) return;
            var tccLites = Newtonsoft.Json.JsonConvert.DeserializeObject<TccTaskLite[]>(tccMaster.Tasks);
            if (tccLites?.Length != tccMaster.Total) throw new ArgumentException($"tid = {tid} 数据有误，无法反序列化 Tasks");
            var tccs = tccLites.Select(tl =>
            {
                try
                {
                    var tccTypeDefault = Type.GetType(tl.TypeName).CreateInstanceGetDefaultValue() as ITcc;
                    if (tccTypeDefault == null) throw new ArgumentException($"{tl.TypeName} 无法创建为 ITcc");
                    return tccTypeDefault;
                }
                catch
                {
                    throw new ArgumentException($"{tl.TypeName} 无法创建为 ITcc");
                }
            })
            .ToArray();
            var tccOrms = tccLites.Select(a => _ib.Get(a.CloudName)).ToArray();
            var tccTasks = tccOrms.SelectMany(z => z.Select<TccTask>().Where(a => a.Tid == tid).ToList()).ToList();
            await ConfimCancelAsync(tccMaster, tccTasks, tccs, tccOrms, retry);
        }
        async Task<bool> ConfimCancelAsync(TccMaster tccMaster, List<TccTask> tccTasks, ITcc[] tccs, IFreeSql[] tccOrms, bool retry)
        {
            var isConfirm = tccTasks.Count == tccMaster.Total;
            var successCount = 0;
            for (var idx = tccs.Length - 1; idx >= 0; idx--)
            {
                try
                {
                    var tccTask = tccTasks.Where(tt => tt.Index == idx && tt.Stage == TccTaskStage.Try).FirstOrDefault();
                    if (tccTask != null)
                    {
                        if ((tccs[idx] as ITccSetter)?.StateIsValued != true)
                            SetTccState(tccs[idx], tccTask);
                        using (var uow = tccOrms[idx].CreateUnitOfWork())
                        {
                            uow.IsolationLevel = tccTask.IsolationLevel;
                            var fsql = TccOrm.Create(tccOrms[idx], () => uow.GetOrBeginTransaction());
                            (tccs[idx] as ITccSetter)?.SetOrm(fsql).SetTccTask(tccTask);

                            var affrows = await fsql.Update<TccTask>()
                                .Where(a => a.Tid == tccMaster.Tid && a.Index == idx && a.Stage == TccTaskStage.Try)
                                .Set(a => a.Stage, isConfirm ? TccTaskStage.Confirm : TccTaskStage.Cancel)
                                .ExecuteAffrowsAsync();
                            if (affrows == 1)
                            {
                                if (isConfirm) tccs[idx].Confirm();
                                else tccs[idx].Cancel();
                            }
                            uow.Commit();
                        }
                    }
                    successCount++;
                }
                catch
                {
                }
            }
            if (successCount == tccs.Length)
            {
                await _master.Update<TccMaster>()
                    .Where(a => a.Tid == tccMaster.Tid && a.Status == TccMasterStatus.Pending)
                    .Set(a => a.Status, isConfirm ? TccMasterStatus.Confirmed : TccMasterStatus.Canceled)
                    .Set(a => a.FinishTime == DateTime.UtcNow)
                    .ExecuteAffrowsAsync();
                return true;
            }
            else
            {
                var affrows = await _master.Update<TccMaster>()
                    .Where(a => a.Tid == tccMaster.Tid && a.Status == TccMasterStatus.Pending)
                    .Set(a => a.RetryCount + 1)
                    .Set(a => a.RetryTime == DateTime.UtcNow)
                    .ExecuteAffrowsAsync();
                if (retry && affrows == 1)
                {
                    //lazy exec
                    _scheduer.AddTempTask(TimeSpan.FromSeconds(60), GetTempTask(tccMaster.Tid));
                }
                return false;
            }
        }
        Action GetTempTask(string tid)
        {
            return () =>
            {
                try
                {
                    ConfimCancelAsync(tid, true).Wait();
                }
                catch
                {
                    try
                    {
                        _master.Update<TccMaster>()
                           .Where(a => a.Tid == tid && a.Status == TccMasterStatus.Pending)
                           .Set(a => a.RetryCount + 1)
                           .ExecuteAffrows();
                    }
                    catch { }
                    _scheduer.AddTempTask(TimeSpan.FromSeconds(60), GetTempTask(tid));
                }
            };
        }
    }
}
