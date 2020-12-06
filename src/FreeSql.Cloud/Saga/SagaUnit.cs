using FreeSql.Cloud.Saga;
using System;
using System.Data.Common;
using System.Threading.Tasks;

namespace FreeSql
{
    public class SagaOptions
    {
        public int MaxRetryCount { get; set; } = 10;
        public TimeSpan RetryInterval { get; set; } = TimeSpan.FromSeconds(60);
    }

    public abstract class SagaUnit<TState> : ISagaUnit, ISagaUnitSetter
    {
        protected SagaUnitInfo Unit { get; private set; }
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
        ISagaUnitSetter SetState(object value);
        bool StateIsValued { get; }
    }
}
