using FreeSql;
using System;

namespace net60_tcc_saga
{
    public enum DbEnum { db1, db2, db3 }

    public static class DB
    {
        public static FreeSqlCloud<DbEnum> Cloud => cloudLazy.Value;

        readonly static Lazy<FreeSqlCloud<DbEnum>> cloudLazy = new Lazy<FreeSqlCloud<DbEnum>>(() =>
        {
            var fsql = new FreeSqlCloud<DbEnum>("app001");
            fsql.DistributeTrace += log => Console.WriteLine(log.Split('\n')[0].Trim());

            fsql.Register(DbEnum.db1, () => new FreeSqlBuilder()
                .UseConnectionString(DataType.Sqlite, @"Data Source=:memory:")
                .UseAutoSyncStructure(true)
                .Build());

            fsql.Register(DbEnum.db2, () => new FreeSqlBuilder()
                .UseConnectionString(DataType.Sqlite, @"Data Source=:memory:")
                .UseAutoSyncStructure(true)
                .Build());

            fsql.Register(DbEnum.db3, () => new FreeSqlBuilder()
                .UseConnectionString(DataType.Sqlite, @"Data Source=:memory:")
                .UseAutoSyncStructure(true)
                .Build());

            fsql.EntitySteering = (_, e) =>
            {
                if (e.EntityType == typeof(User)) e.DBKey = DbEnum.db1;
                else if (e.EntityType == typeof(Goods)) e.DBKey = DbEnum.db2;
                else if (e.EntityType == typeof(Order)) e.DBKey = DbEnum.db3;
                #region 另一种读写分离
                //switch (e.MethodName)
                //{
                //    case "Select":
                //        if (e.EntityType == typeof(Program)) ; //判断某一个实体类型
                //        if (e.DBKey == DbEnum.db1) //判断主库时
                //        {
                //            var dbkeyIndex = new Random().Next(0, e.AvailableDBKeys.Length);
                //            e.DBKey = e.AvailableDBKeys[dbkeyIndex]; //重新定向到其他 db
                //        }
                //        break;
                //    case "Insert":
                //    case "Update":
                //    case "Delete":
                //    case "InsertOrUpdate":
                //        break;
                //}
                #endregion
            };
            return fsql;
        });
    }
}
