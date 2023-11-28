using FreeSql;
using System;

namespace net60_webapi
{
    public enum DbEnum { db1, db2, db3 }

    public class FreeSqlCloud : FreeSqlCloud<DbEnum>
    {
        public FreeSqlCloud() : base(null) { }
        public FreeSqlCloud(string distributeKey) : base(distributeKey) { }
    }

    public static class DB
    {
        public static FreeSqlCloud Cloud => cloudLazy.Value;

        readonly static Lazy<FreeSqlCloud> cloudLazy = new Lazy<FreeSqlCloud>(() =>
        {
            var fsql = new FreeSqlCloud("app001");
            fsql.DistributeTrace += log => Console.WriteLine(log.Split('\n')[0].Trim());

            fsql.Register(DbEnum.db1, () => new FreeSqlBuilder()
                .UseConnectionString(DataType.Sqlite, @"Data Source=:memory:;max pool size=1")
                .UseAutoSyncStructure(true)
                .Build());

            fsql.Register(DbEnum.db2, () => new FreeSqlBuilder()
                .UseConnectionString(DataType.Sqlite, @"Data Source=:memory:;max pool size=2")
                .UseAutoSyncStructure(true)
                .Build());

            fsql.Register(DbEnum.db3, () => new FreeSqlBuilder()
                .UseConnectionString(DataType.Sqlite, @"Data Source=:memory:;max pool size=3")
                .UseAutoSyncStructure(true)
                .Build());

            Console.WriteLine(fsql.Ado.ConnectionString);
            using (fsql.Change(DbEnum.db3))
            {
				Console.WriteLine(fsql.Ado.ConnectionString);
			}
			Console.WriteLine(fsql.Ado.ConnectionString);

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
