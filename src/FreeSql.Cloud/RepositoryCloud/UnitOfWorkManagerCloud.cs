using FreeSql.Cloud.Abstract;
using System;
using System.Collections.Generic;
using System.Data;
using System.Threading;

namespace FreeSql
{
    class UnitOfWorkManagerCloud
    {
        public FreeSqlCloudBase Cloud { get; }
        internal readonly Dictionary<string, UnitOfWorkManager> _uowManagers = new Dictionary<string, UnitOfWorkManager>();
        public UnitOfWorkManagerCloud(FreeSqlCloudBase cloud)
        {
            Cloud = cloud;
            _dbkeyCurrent = new AsyncLocalAccessor<string>(Cloud.GetDBKey);
		}

        public void Dispose()
        {
            ForEachUowManagers(uowm => uowm.Dispose());
            _uowManagers.Clear();
        }
        protected void ForEachUowManagers(Action<UnitOfWorkManager> action)
        {
            foreach (var uowm in _uowManagers.Values) action(uowm);
        }

        internal AsyncLocalAccessor<string> _dbkeyCurrent;
        internal string GetDBKey()
        {
            if (string.IsNullOrWhiteSpace(_dbkeyCurrent.Value) || GetUnitOfWorkManager(_dbkeyCurrent.Value).Current == null) return Cloud.GetDBKey();
            return _dbkeyCurrent.Value;
        }
        public IUnitOfWork Begin(string dbkey, Propagation propagation = Propagation.Required, IsolationLevel? isolationLevel = null)
        {
            _dbkeyCurrent.Value = dbkey;
            return GetUnitOfWorkManager(dbkey).Begin(propagation, isolationLevel);
        }
        public UnitOfWorkManager GetUnitOfWorkManager(string dbkey)
        {
            if (_uowManagers.TryGetValue(dbkey, out var uowm) == false)
                _uowManagers.Add(dbkey, uowm = new UnitOfWorkManager(Cloud.Use(dbkey)));
            return uowm;
        }
    }
}
