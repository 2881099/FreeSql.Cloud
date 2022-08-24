using System;
using System.Collections.Generic;
using System.Text;

namespace FreeSql.Cloud.Abstract
{
    public abstract class FreeSqlCloudBase
    {
        internal abstract string GetDBKey();
        internal abstract IFreeSql UseDBKey(string dbkey);
        internal abstract IFreeSql ChangeDBKey(string dbkey);
    }
}
