using FreeSql;
using FreeSql.Cloud;
using System;
using System.Threading.Tasks;

namespace ConsoleApp1
{
    class Program
    {
        async static Task Main(string[] args)
        {
            using (var fsql = new FreeSqlCloud("app001"))
            {
                fsql.TccTrace += (_, log) => Console.WriteLine(log.Split('\n')[0].Trim());
                fsql.Register("db1", () => new FreeSqlBuilder().UseConnectionString(FreeSql.DataType.Sqlite, @"Data Source=db1.db").Build());
                fsql.Register("db2", () => new FreeSqlBuilder().UseConnectionString(FreeSql.DataType.Sqlite, @"Data Source=db2.db").Build());
                fsql.Register("db3", () => new FreeSqlBuilder().UseConnectionString(FreeSql.DataType.Sqlite, @"Data Source=db3.db").Build());

                //for (var a = 0; a < 1000; a++)
                //{
                    var tid = Guid.NewGuid().ToString();
                    await fsql
                        .StartTcc(tid, "创建订单")
                        .Then(typeof(Tcc1), "db1")
                        .Then(typeof(Tcc2), "db2")
                        .Then(typeof(Tcc3), "db3")
                        .ExecuteAsync();

                    tid = Guid.NewGuid().ToString();
                    await fsql
                        .StartTcc(tid, "支付购买", new TccOptions
                        {
                            MaxRetryCount = 10,
                            RetryInterval = TimeSpan.FromSeconds(10)
                        })
                        .Then(typeof(Tcc1), "db1", new TccState { Id = 1, Name = "tcc1" })
                        .Then(typeof(Tcc2), "db2")
                        .Then(typeof(Tcc3), "db3", new TccState { Id = 3, Name = "tcc3" })
                        .ExecuteAsync();
                //}

                Console.ReadKey();
            }
        }
    }
    class TccState
    {
        public int Id { get; set; }
        public string Name { get; set; }
    }

    class Tcc1 : TccUnit<TccState>
    {
        public override void Cancel()
        {
            throw new Exception("dkdkdk");
        }
        public override void Confirm()
        {
        }
        public override void Try()
        {
        }
    }

    class Tcc2 : TccUnit<TccState>
    {
        public override void Cancel()
        {
        }
        public override void Confirm()
        {
        }
        public override void Try()
        {
        }
    }

    class Tcc3 : TccUnit<TccState>
    {
        public override void Cancel()
        {
        }
        public override void Confirm()
        {
        }
        public override void Try()
        {
            throw new Exception("xxx");
        }
    }
}
