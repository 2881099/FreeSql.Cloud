using FreeSql;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace net60_tcc_saga
{
    class TccUnit1State
    {
        public int UserId { get; set; }
        public int Point { get; set; }
        public Guid BuyLogId { get; set; }
        public int GoodsId { get; set; }
        public Guid OrderId { get; set; }
    }
    [Description("第1步：数据库db1 扣除用户积分")]
    class Tcc1 : TccUnit<TccUnit1State>
    {
        public override async Task Try()
        {
            var affrows = await Orm.Update<User>()
                .Set(a => a.Point - State.Point)
                .Where(a => a.Id == State.UserId && a.Point >= State.Point)
                .ExecuteAffrowsAsync();
            if (affrows <= 0) throw new Exception("扣除积分失败");

            //记录积分变动日志？
        }
        public override Task Confirm()
        {
            return Task.CompletedTask;
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

    class TccUnit2State
    {
        public int UserId { get; set; }
        public int Point { get; set; }
        public Guid BuyLogId { get; set; }
        public int GoodsId { get; set; }
        public Guid OrderId { get; set; }
    }
    [Description("第2步：数据库db2 扣除库存")]
    class Tcc2 : TccUnit<TccUnit2State>
    {
        public override async Task Try()
        {
            var affrows = await Orm.Update<Goods>()
                .Set(a => a.Stock - 1)
                .Where(a => a.Id == State.GoodsId && a.Stock >= 1)
                .ExecuteAffrowsAsync();
            if (affrows <= 0) throw new Exception("扣除库存失败");
        }
        public override Task Confirm()
        {
            return Task.CompletedTask;
        }
        public override async Task Cancel()
        {
            await Orm.Update<Goods>()
                .Set(a => a.Stock + 1)
                .Where(a => a.Id == State.GoodsId)
                .ExecuteAffrowsAsync(); //退还库存
        }
    }


    class TccUnit3State
    {
        public int UserId { get; set; }
        public int Point { get; set; }
        public Guid BuyLogId { get; set; }
        public int GoodsId { get; set; }
        public Guid OrderId { get; set; }
    }
    [Description("第3步：数据库db3 创建订单")]
    class Tcc3 : TccUnit<TccUnit3State>
    {
        public override async Task Try()
        {
            await Orm.Insert(new Order { Id = State.OrderId, Status = Order.OrderStatus.Pending, CreateTime = DateTime.Now })
                .ExecuteAffrowsAsync();
        }
        public override async Task Confirm()
        {
            //幂等交付
            await Orm.Update<Order>()
                   .Set(a => a.Status == Order.OrderStatus.Success)
                   .Where(a => a.Id == State.OrderId && a.Status == Order.OrderStatus.Pending)
                   .ExecuteAffrowsAsync();
        }
        public override Task Cancel()
        {
            return Task.CompletedTask;
        }
    }
}
