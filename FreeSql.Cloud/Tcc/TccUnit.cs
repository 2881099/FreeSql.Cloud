namespace FreeSql.Cloud
{
    public abstract class TccUnit<TState> : ITccUnit, ITccUnitSetter
    {
        protected IFreeSql Fsql { get; private set; }
        protected TccUnitInfo Unit { get; private set; }
        protected TState State { get; private set; }

        public abstract void Try();
        public abstract void Confirm();
        public abstract void Cancel();

        public ITccUnitSetter SetOrm(IFreeSql value)
        {
            Fsql = value;
            return this;
        }
        public ITccUnitSetter SetUnit(TccUnitInfo value)
        {
            Unit = value;
            return this;
        }
        public ITccUnitSetter SetState(object value)
        {
            State = (TState)value;
            StateIsValued = true;
            return this;
        }
        public bool StateIsValued { get; internal set; }
    }

    public interface ITccUnit
    {
        void Try();
        void Confirm();
        void Cancel();
    }

    public interface ITccUnitSetter
    {
        ITccUnitSetter SetOrm(IFreeSql value);
        ITccUnitSetter SetUnit(TccUnitInfo value);

        ITccUnitSetter SetState(object value);
        bool StateIsValued { get; }
    }
}
