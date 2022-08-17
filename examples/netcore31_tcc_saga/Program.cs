using FreeSql;
using System;
using System.Threading.Tasks;

namespace net60_tcc_saga
{
    class Program
    {
        async static Task Main(string[] args)
        {
            DB.Cloud.Insert(new User { Id = 1, Name = "testuser01", Point = 10 }).ExecuteAffrows();
            DB.Cloud.Insert(new Goods { Id = 1, Title = "testgoods01", Stock = 0 }).ExecuteAffrows();

            await TestTcc();
            await TestSaga();
            //await TestHttpTcc();
            //await TestHttpSaga();

            Console.ReadKey();
            DB.Cloud.Dispose();
        }

        async static Task TestTcc()
        {
            var orderId = Guid.NewGuid();
            await DB.Cloud.StartTcc(orderId.ToString(), "支付购买TCC事务",
                new TccOptions
                {
                    MaxRetryCount = 10,
                    RetryInterval = TimeSpan.FromSeconds(10)
                })
                .Then<Tcc1>(DbEnum.db1, new TccUnit1State { UserId = 1, Point = 10, GoodsId = 1, OrderId = orderId })
                .Then<Tcc2>(DbEnum.db2, new TccUnit2State { UserId = 1, Point = 10, GoodsId = 1, OrderId = orderId })
                .Then<Tcc3>(DbEnum.db3, new TccUnit3State { UserId = 1, Point = 10, GoodsId = 1, OrderId = orderId })
                .ExecuteAsync();
        }

        async static Task TestSaga()
        {
            var orderId = Guid.NewGuid();
            await DB.Cloud.StartSaga(orderId.ToString(), "支付购买SAGA事务",
                new SagaOptions
                {
                    MaxRetryCount = 10,
                    RetryInterval = TimeSpan.FromSeconds(10)
                })
                .Then<Saga1>(DbEnum.db1, new SagaUnit1State { UserId = 1, Point = 10, GoodsId = 1, OrderId = orderId })
                .Then<Saga2>(DbEnum.db2, new SagaUnit2State { UserId = 1, Point = 10, GoodsId = 1, OrderId = orderId })
                .Then<Saga3>(DbEnum.db3, new SagaUnit3State { UserId = 1, Point = 10, GoodsId = 1, OrderId = orderId })
                .ExecuteAsync();
        }


        async static Task TestHttpSaga()
        {
            var orderId = Guid.NewGuid();
            await DB.Cloud.StartSaga(orderId.ToString(), "支付购买webapi(saga)",
                new SagaOptions
                {
                    MaxRetryCount = 10,
                    RetryInterval = TimeSpan.FromSeconds(10)
                })
                .Then<HttpSaga>(default, new HttpUnitState
                {
                    Url = "https://192.168.1.100/saga/UserPoint",
                    Data = "UserId=1&Point=10&GoodsId=1&OrderId=" + orderId
                })
                .Then<HttpSaga>(default, new HttpUnitState
                {
                    Url = "https://192.168.1.100/saga/GoodsStock",
                    Data = "UserId=1&Point=10&GoodsId=1&OrderId=" + orderId
                })
                .Then<HttpSaga>(default, new HttpUnitState
                {
                    Url = "https://192.168.1.100/saga/OrderNew",
                    Data = "UserId=1&Point=10&GoodsId=1&OrderId=" + orderId
                })
                .ExecuteAsync();
        }
        class HttpSaga : SagaUnit<HttpUnitState>
        {
            public override Task Commit()
            {
                //Console.WriteLine("请求 webapi：" + State.Url + "/Commit" + State.Data);
                return Task.CompletedTask;
            }
            public override Task Cancel()
            {
                //Console.WriteLine("请求 webapi：" + State.Url + "/Cancel" + State.Data);
                return Task.CompletedTask;
            }
        }

        async static Task TestHttpTcc()
        {
            var orderId = Guid.NewGuid();
            await DB.Cloud.StartTcc(orderId.ToString(), "支付购买webapi",
                new TccOptions
                {
                    MaxRetryCount = 10,
                    RetryInterval = TimeSpan.FromSeconds(10)
                })
                .Then<HttpTcc>(default, new HttpUnitState
                {
                    Url = "https://192.168.1.100/tcc/UserPoint",
                    Data = "UserId=1&Point=10&GoodsId=1&OrderId=" + orderId
                })
                .Then<HttpTcc>(default, new HttpUnitState
                {
                    Url = "https://192.168.1.100/tcc/GoodsStock",
                    Data = "UserId=1&Point=10&GoodsId=1&OrderId=" + orderId
                })
                .Then<HttpTcc>(default, new HttpUnitState
                {
                    Url = "https://192.168.1.100/tcc/OrderNew",
                    Data = "UserId=1&Point=10&GoodsId=1&OrderId=" + orderId
                })
                .ExecuteAsync();
        }
        class HttpTcc : TccUnit<HttpUnitState>
        {
            public override Task Try()
            {
                //Console.WriteLine("请求 webapi：" + State.Url + "/Try" + State.Data);
                return Task.CompletedTask;
            }
            public override Task Confirm()
            {
                //Console.WriteLine("请求 webapi：" + State.Url + "/Confirm" + State.Data);
                return Task.CompletedTask;
            }
            public override Task Cancel()
            {
                //Console.WriteLine("请求 webapi：" + State.Url + "/Cancel" + State.Data);
                return Task.CompletedTask;
            }
        }
        class HttpUnitState
        {
            public string Url { get; set; }
            public string Data { get; set; }
        }
    }

}