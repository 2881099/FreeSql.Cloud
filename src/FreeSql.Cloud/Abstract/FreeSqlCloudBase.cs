using System;
using System.Collections.Generic;
using System.Text;

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
}
