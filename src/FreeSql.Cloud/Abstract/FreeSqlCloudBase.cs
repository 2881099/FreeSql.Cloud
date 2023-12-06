using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace FreeSql.Cloud.Abstract
{
    public abstract class FreeSqlCloudBase
    {
        internal abstract string GetDBKey();
        public abstract IFreeSql Use(DBKeyString dbkey);
        public abstract IFreeSql Change(DBKeyString dbkey);
    }

    public class DBKeyString
    {
        string _dbkey;
        public override string ToString() => _dbkey;

        public static implicit operator DBKeyString(string dbkey) => string.IsNullOrWhiteSpace(dbkey) ? null : new DBKeyString { _dbkey = dbkey };
        public static implicit operator string(DBKeyString dbkey) => dbkey?.ToString();
    }

    public class AsyncLocalAccessor<T>
    {
        Func<T> _defaultValue;
		public AsyncLocalAccessor(Func<T> defaultValue)
        {
            _defaultValue = defaultValue;
			if (_asyncLocal.Value == null) _asyncLocal.Value = new ValueHolder { DefaultValue = _defaultValue };
		}
		public T Value
        {
            get
            {
                if (_asyncLocal.Value != null) return _asyncLocal.Value.GetValue();
                return default;
            }
            set
            {
                if (_asyncLocal.Value == null) _asyncLocal.Value = new ValueHolder { DefaultValue = _defaultValue };
                _asyncLocal.Value.SetValue(value);
			}

		}

        class ValueHolder
        {
            T _rawValue;
            bool _rawValueChanged = false;
			public Func<T> DefaultValue { get; set; }

            public T GetValue() => _rawValueChanged ? _rawValue : DefaultValue();
            public void SetValue(T value)
            {
				_rawValueChanged = true;
				_rawValue = value;
			}
		}
#if net40
        ThreadLocal<ValueHolder> _asyncLocal = new ThreadLocal<ValueHolder>();
#else
        AsyncLocal<ValueHolder> _asyncLocal = new AsyncLocal<ValueHolder>();
#endif
    }
}
