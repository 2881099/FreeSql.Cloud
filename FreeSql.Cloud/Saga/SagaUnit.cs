using FreeSql.Cloud.Saga;
using System;
using System.Data.Common;

namespace FreeSql
{
    public class SagaOptions
    {
        public int MaxRetryCount { get; set; } = 30;
        public TimeSpan RetryInterval { get; set; } = TimeSpan.FromSeconds(60);
    }

    public abstract class SagaUnit<TState> : ISagaUnit, ISagaUnitSetter
    {
        protected DbTransaction Transaction { get; private set; }
        protected IFreeSql Fsql { get; private set; }
        protected SagaUnitInfo Unit { get; private set; }
        protected TState State { get; private set; }

        public abstract void Commit();
        public abstract void Cancel();

        ISagaUnitSetter ISagaUnitSetter.SetTransaction(DbTransaction value)
        {
            Transaction = value;
            return this;
        }
        ISagaUnitSetter ISagaUnitSetter.SetOrm(IFreeSql value)
        {
            Fsql = value;
            return this;
        }
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
        void Commit();
        void Cancel();
    }

    public interface ISagaUnitSetter
    {
        ISagaUnitSetter SetTransaction(DbTransaction value);
        ISagaUnitSetter SetOrm(IFreeSql value);
        ISagaUnitSetter SetUnit(SagaUnitInfo value);

        ISagaUnitSetter SetState(object value);
        bool StateIsValued { get; }
    }
}
