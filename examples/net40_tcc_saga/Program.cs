using FreeSql;
using System;
using System.Threading.Tasks;

namespace net60_tcc_saga
{
    class Program
    {
        static void Main(string[] args)
        {
            DB.Cloud.Insert(new User { Id = 1, Name = "testuser01", Point = 10 }).ExecuteAffrows();
            DB.Cloud.Insert(new Goods { Id = 1, Title = "testgoods01", Stock = 0 }).ExecuteAffrows();

            TestTcc();
            TestSaga();

            Console.ReadKey();
            DB.Cloud.Dispose();
        }

        static void TestTcc()
        {
            var orderId = Guid.NewGuid();
            DB.Cloud.StartTcc(orderId.ToString(), "支付购买TCC事务",
                new TccOptions
                {
                    MaxRetryCount = 10,
                    RetryInterval = TimeSpan.FromSeconds(10)
                })
                .Then<Tcc1>(DbEnum.db1, new TccUnit1State { UserId = 1, Point = 10, GoodsId = 1, OrderId = orderId })
                .Then<Tcc2>(DbEnum.db2, new TccUnit2State { UserId = 1, Point = 10, GoodsId = 1, OrderId = orderId })
                .Then<Tcc3>(DbEnum.db3, new TccUnit3State { UserId = 1, Point = 10, GoodsId = 1, OrderId = orderId })
                .Execute();
        }

        static void TestSaga()
        {
            var orderId = Guid.NewGuid();
            DB.Cloud.StartSaga(orderId.ToString(), "支付购买SAGA事务",
                new SagaOptions
                {
                    MaxRetryCount = 10,
                    RetryInterval = TimeSpan.FromSeconds(10)
                })
                .Then<Saga1>(DbEnum.db1, new SagaUnit1State { UserId = 1, Point = 10, GoodsId = 1, OrderId = orderId })
                .Then<Saga2>(DbEnum.db2, new SagaUnit2State { UserId = 1, Point = 10, GoodsId = 1, OrderId = orderId })
                .Then<Saga3>(DbEnum.db3, new SagaUnit3State { UserId = 1, Point = 10, GoodsId = 1, OrderId = orderId })
                .Execute();
        }
    }

}