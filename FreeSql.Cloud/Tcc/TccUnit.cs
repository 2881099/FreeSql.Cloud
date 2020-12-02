using FreeSql.Cloud.Tcc;
using System;
using System.Data.Common;

namespace FreeSql
{
    public class TccOptions
    {
        public int MaxRetryCount { get; set; } = 30;
        public TimeSpan RetryInterval { get; set; } = TimeSpan.FromSeconds(60);
    }

    public abstract class TccUnit<TState> : ITccUnit, ITccUnitSetter
    {
        protected DbTransaction Transaction { get; private set; }
        protected IFreeSql Fsql { get; private set; }
        protected TccUnitInfo Unit { get; private set; }
        protected TState State { get; private set; }

        public abstract void Try();
        public abstract void Confirm();
        public abstract void Cancel();

        ITccUnitSetter ITccUnitSetter.SetTransaction(DbTransaction value)
        {
            Transaction = value;
            return this;
        }
        ITccUnitSetter ITccUnitSetter.SetOrm(IFreeSql value)
        {
            Fsql = value;
            return this;
        }
        ITccUnitSetter ITccUnitSetter.SetUnit(TccUnitInfo value)
        {
            Unit = value;
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
        void Try();
        void Confirm();
        void Cancel();
    }

    public interface ITccUnitSetter
    {
        ITccUnitSetter SetTransaction(DbTransaction value);
        ITccUnitSetter SetOrm(IFreeSql value);
        ITccUnitSetter SetUnit(TccUnitInfo value);

        ITccUnitSetter SetState(object value);
        bool StateIsValued { get; }
    }
}
