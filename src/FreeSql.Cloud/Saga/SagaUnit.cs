using FreeSql.Cloud.Saga;
using System;
using System.Data.Common;
using System.Threading.Tasks;

namespace FreeSql
{
    public class SagaOptions
    {
        /// <summary>
        /// 重试次数
        /// </summary>
        public int MaxRetryCount { get; set; } = 10;
        /// <summary>
        /// 重试间隔
        /// </summary>
        public TimeSpan RetryInterval { get; set; } = TimeSpan.FromSeconds(60);
    }

    public abstract class SagaUnit<TState> : ISagaUnit, ISagaUnitSetter
    {
        /// <summary>
        /// SagaUnit 持久化数据
        /// </summary>
        protected SagaUnitInfo Unit { get; private set; }
        /// <summary>
        /// 要求 StartSaga Then 方法设置 DBKey<para></para>
        /// Commit/Cancel 将使用 DBKey 对应的事务<para></para>
        /// 使用属性 Orm 可保持事务一致，它是被重新实现的 IFreeSql
        /// </summary>
        protected IFreeSql Orm { get; private set; }
        /// <summary>
        /// 要求 StartSaga Then 方法设置 State<para></para>
        /// Commit/Cancel 将使用 State 进行无状态工作<para></para>
        /// 因为最终会脱离执行上下文
        /// </summary>
        protected TState State { get; private set; }

#if net40
        public abstract void Commit();
        public abstract void Cancel();
#else
        public abstract Task Commit();
        public abstract Task Cancel();
#endif

        ISagaUnitSetter ISagaUnitSetter.SetUnit(SagaUnitInfo value)
        {
            Unit = value;
            return this;
        }
        ISagaUnitSetter ISagaUnitSetter.SetOrm(IFreeSql value)
        {
            Orm = value;
            return this;
        }
        ISagaUnitSetter ISagaUnitSetter.SetState(object value)
        {
            State = (TState)value;
            _StateIsValued = true;
            return this;
        }
        bool _StateIsValued;
        bool ISagaUnitSetter.StateIsValued => _StateIsValued;
    }
}

namespace FreeSql.Cloud.Saga
{
    public interface ISagaUnit
    {
#if net40
        void Commit();
        void Cancel();
#else
        Task Commit();
        Task Cancel();
#endif
    }

    public interface ISagaUnitSetter
    {
        ISagaUnitSetter SetUnit(SagaUnitInfo value);
        ISagaUnitSetter SetOrm(IFreeSql value);
        ISagaUnitSetter SetState(object value);
        bool StateIsValued { get; }
    }
}
