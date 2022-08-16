using FreeSql;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace net60_tcc_saga
{
    class SagaUnit1State
    {
        public int UserId { get; set; }
        public int Point { get; set; }
        public Guid BuyLogId { get; set; }
        public int GoodsId { get; set; }
        public Guid OrderId { get; set; }
    }
    [Description("第1步：数据库db1 扣除用户积分")]
    class Saga1 : SagaUnit<SagaUnit1State>
    {
        public override async Task Commit()
        {
            var affrows = await Orm.Update<User>()
                .Set(a => a.Point - State.Point)
                .Where(a => a.Id == State.UserId && a.Point >= State.Point)
                .ExecuteAffrowsAsync();
            if (affrows <= 0) throw new Exception("扣除积分失败");

            //记录积分变动日志？
        }
        public override async Task Cancel()
        {
            await Orm.Update<User>()
                .Set(a => a.Point + State.Point)
                .Where(a => a.Id == State.UserId)
                .ExecuteAffrowsAsync(); //退还积分

            //记录积分变动日志？
        }
    }

    class SagaUnit2State
    {
        public int UserId { get; set; }
        public int Point { get; set; }
        public Guid BuyLogId { get; set; }
        public int GoodsId { get; set; }
        public Guid OrderId { get; set; }
    }
    [Description("第2步：数据库db2 扣除库存")]
    class Saga2 : SagaUnit<SagaUnit2State>
    {
        public override async Task Commit()
        {
            var affrows = await Orm.Update<Goods>()
                .Set(a => a.Stock - 1)
                .Where(a => a.Id == State.GoodsId && a.Stock >= 1)
                .ExecuteAffrowsAsync();
            if (affrows <= 0) throw new Exception("扣除库存失败");
        }
        public override async Task Cancel()
        {
            await Orm.Update<Goods>()
                .Set(a => a.Stock + 1)
                .Where(a => a.Id == State.GoodsId)
                .ExecuteAffrowsAsync(); //退还库存
        }
    }


    class SagaUnit3State
    {
        public int UserId { get; set; }
        public int Point { get; set; }
        public Guid BuyLogId { get; set; }
        public int GoodsId { get; set; }
        public Guid OrderId { get; set; }
    }
    [Description("第3步：数据库db3 创建订单")]
    class Saga3 : SagaUnit<SagaUnit3State>
    {
        public override async Task Commit()
        {
            await Orm.Insert(new Order { Id = State.OrderId, Status = Order.OrderStatus.Success, CreateTime = DateTime.Now })
                .ExecuteAffrowsAsync();
        }
        public override Task Cancel()
        {
            return Task.CompletedTask;
        }
    }
}
