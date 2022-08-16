using FreeSql.Cloud.Tcc;
using System;
using System.Data.Common;
using System.Threading.Tasks;

namespace FreeSql
{
    public class TccOptions
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

    public abstract class TccUnit<TState> : ITccUnit, ITccUnitSetter
    {
        /// <summary>
        /// TccUnit 持久化数据
        /// </summary>
        protected TccUnitInfo Unit { get; private set; }
        /// <summary>
        /// 要求 StartTcc Then 方法设置 DBKey<para></para>
        /// Try/Confirm/Cancel 将使用 DBKey 对应的事务<para></para>
        /// 使用属性 Orm 可保持事务一致，它是被重新实现的 IFreeSql
        /// </summary>
        protected IFreeSql Orm { get; private set; }
        /// <summary>
        /// 要求 StartTcc Then 方法设置 State<para></para>
        /// Try/Confirm/Cancel 将使用 State 进行无状态工作<para></para>
        /// 因为最终会脱离执行上下文
        /// </summary>
        protected TState State { get; private set; }

#if net40
        public abstract void Try();
        public abstract void Confirm();
        public abstract void Cancel();
#else
        public abstract Task Try();
        public abstract Task Confirm();
        public abstract Task Cancel();
#endif

        ITccUnitSetter ITccUnitSetter.SetUnit(TccUnitInfo value)
        {
            Unit = value;
            return this;
        }
        ITccUnitSetter ITccUnitSetter.SetOrm(IFreeSql value)
        {
            Orm = value;
            return this;
        }
        ITccUnitSetter ITccUnitSetter.SetState(object value)
        {
            State = (TState)value;
            _StateIsValued = true;
            return this;
        }
        bool _StateIsValued;
        bool ITccUnitSetter.StateIsValued => _StateIsValued;
    }
}

namespace FreeSql.Cloud.Tcc
{
    public interface ITccUnit
    {
#if net40
        void Try();
        void Confirm();
        void Cancel();
#else
        Task Try();
        Task Confirm();
        Task Cancel();
#endif
    }

    public interface ITccUnitSetter
    {
        ITccUnitSetter SetUnit(TccUnitInfo value);
        ITccUnitSetter SetOrm(IFreeSql value);
        ITccUnitSetter SetState(object value);
        bool StateIsValued { get; }
    }
}
