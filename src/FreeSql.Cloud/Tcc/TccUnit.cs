using FreeSql.Cloud.Tcc;
using System;
using System.Data.Common;
using System.Threading.Tasks;

namespace FreeSql
{
    public class TccOptions
    {
        public int MaxRetryCount { get; set; } = 10;
        public TimeSpan RetryInterval { get; set; } = TimeSpan.FromSeconds(60);
    }

    public abstract class TccUnit<TState> : ITccUnit, ITccUnitSetter
    {
        protected TccUnitInfo Unit { get; private set; }
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
        ITccUnitSetter SetState(object value);
        bool StateIsValued { get; }
    }
}
