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
            using (var fsqlc = new FreeSqlCloud())
            {
                fsqlc.Register("db1", () => new FreeSqlBuilder().UseConnectionString(FreeSql.DataType.Sqlite, @"Data Source=db1.db").Build());
                fsqlc.Register("db2", () => new FreeSqlBuilder().UseConnectionString(FreeSql.DataType.Sqlite, @"Data Source=db2.db").Build());
                fsqlc.Register("db3", () => new FreeSqlBuilder().UseConnectionString(FreeSql.DataType.Sqlite, @"Data Source=db3.db").Build());

                var tid = Guid.NewGuid().ToString();
                await fsqlc
                    .StartTcc(tid)
                    .Then(typeof(Tcc1), "db1")
                    .Then(typeof(Tcc2), "db2")
                    .Then(typeof(Tcc3), "db3")
                    .ExecuteAsync();

                tid = Guid.NewGuid().ToString();
                await fsqlc
                    .StartTcc(tid)
                    .Then(typeof(Tcc1), "db1", new TccState { Id = 1, Name = "tcc1" })
                    .Then(typeof(Tcc2), "db2")
                    .Then(typeof(Tcc3), "db3", new TccState { Id = 3, Name = "tcc3" })
                    .ExecuteAsync();
            }
        }
    }
    class TccState
    {
        public int Id { get; set; }
        public string Name { get; set; }
    }

    class Tcc1 : TccBase<TccState>
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

    class Tcc2 : TccBase<TccState>
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

    class Tcc3 : TccBase<TccState>
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
