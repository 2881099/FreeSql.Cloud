using FreeSql;
using System;
using System.ComponentModel;
using System.Threading.Tasks;

namespace netcore31_tcc_saga
{
    class Program
    {
        public enum DbEnum { db1, db2, db3 }
        async static Task Main(string[] args)
        {
            using (var fsql = new FreeSqlCloud<DbEnum>("app001"))
            {
                fsql.DistributeTrace += log => Console.WriteLine(log.Split('\n')[0].Trim());

                fsql.Register(DbEnum.db1, () => new FreeSqlBuilder()
                    .UseConnectionString(DataType.Sqlite, @"Data Source=db1.db")
                    .Build());

                fsql.Register(DbEnum.db2, () => new FreeSqlBuilder()
                    .UseConnectionString(DataType.Sqlite, @"Data Source=db2.db")
                    .Build());

                fsql.Register(DbEnum.db3, () => new FreeSqlBuilder()
                    .UseConnectionString(DataType.Sqlite, @"Data Source=db3.db")
                    .Build());

                //for (var a = 0; a < 1000; a++)
                //{

                //TCC
                var tid = Guid.NewGuid().ToString();
                await fsql
                    .StartTcc(tid, "创建订单")
                    .Then<Tcc1>(DbEnum.db1)
                    .Then<Tcc2>(DbEnum.db2)
                    .Then<Tcc3>(DbEnum.db3)
                    .ExecuteAsync();

                tid = Guid.NewGuid().ToString();
                await fsql.StartTcc(tid, "支付购买", 
                    new TccOptions
                    {
                        MaxRetryCount = 10,
                        RetryInterval = TimeSpan.FromSeconds(10)
                    })
                .Then<Tcc1>(DbEnum.db1, new LocalState { Id = 1, Name = "tcc1" })
                .Then<Tcc2>(DbEnum.db2)
                .Then<Tcc3>(DbEnum.db3, new LocalState { Id = 3, Name = "tcc3" })
                .ExecuteAsync();

                //Saga
                tid = Guid.NewGuid().ToString();
                await fsql
                    .StartSaga(tid, "注册用户")
                    .Then<Saga1>(DbEnum.db1)
                    .Then<Saga2>(DbEnum.db2)
                    .Then<Saga3>(DbEnum.db3)
                    .ExecuteAsync();

                tid = Guid.NewGuid().ToString();
                await fsql.StartSaga(tid, "发表评论",
                    new SagaOptions
                    {
                        MaxRetryCount = 5,
                        RetryInterval = TimeSpan.FromSeconds(5)
                    })
                    .Then<Saga1>(DbEnum.db1, new LocalState { Id = 1, Name = "tcc1" })
                    .Then<Saga2>(DbEnum.db2)
                    .Then<Saga3>(DbEnum.db3, new LocalState { Id = 3, Name = "tcc3" })
                    .ExecuteAsync();

                Console.ReadKey();
            }
        }
    }

    class LocalState
    {
        public int Id { get; set; }
        public string Name { get; set; }
    }

    [Description("第1步")]
    class Tcc1 : TccUnit<LocalState>
    {
        public override void Cancel() => throw new Exception("dkdkdk");
        public override void Confirm() { }
        public override void Try() { }
    }
    [Description("第2步")]
    class Tcc2 : TccUnit<LocalState>
    {
        public override void Cancel() { }
        public override void Confirm() { }
        public override void Try() { }
    }
    [Description("第3步")]
    class Tcc3 : TccUnit<LocalState>
    {
        public override void Cancel() { }
        public override void Confirm() { }
        public override void Try() => throw new Exception("xxx");
    }

    [Description("第1步")]
    class Saga1 : SagaUnit<LocalState>
    {
        public override void Cancel() => throw new Exception("dkdkdk");
        public override void Commit() { }
    }
    [Description("第2步")]
    class Saga2 : SagaUnit<LocalState>
    {
        public override void Cancel() { }
        public override void Commit() { }
    }
    [Description("第3步")]
    class Saga3 : SagaUnit<LocalState>
    {
        public override void Cancel() { }
        public override void Commit() => throw new Exception("xxx");
    }
}