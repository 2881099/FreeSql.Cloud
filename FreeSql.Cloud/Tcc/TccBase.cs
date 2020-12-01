namespace FreeSql.Cloud
{
    public abstract class TccBase<TState> : ITcc, ITccSetter
    {
        protected IFreeSql Fsql { get; private set; }
        protected TccTask TccTask { get; private set; }
        protected TState State { get; private set; }

        public abstract void Try();
        public abstract void Confirm();
        public abstract void Cancel();

        public ITccSetter SetOrm(IFreeSql fsql)
        {
            Fsql = fsql;
            return this;
        }
        public ITccSetter SetTccTask(TccTask task)
        {
            TccTask = task;
            return this;
        }
        public ITccSetter SetState(object state)
        {
            State = (TState)state;
            StateIsValued = true;
            return this;
        }
        public bool StateIsValued { get; internal set; }
    }

    public interface ITcc
    {
        void Try();
        void Confirm();
        void Cancel();
    }

    public interface ITccSetter
    {
        ITccSetter SetOrm(IFreeSql fsql);
        ITccSetter SetTccTask(TccTask task);

        ITccSetter SetState(object state);
        bool StateIsValued { get; }
    }
}
