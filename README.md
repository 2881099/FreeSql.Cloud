<h1 align="center"> 🦄 FreeSql.Cloud </h1>

为 FreeSql 提供跨数据库访问，分布式事务TCC、SAGA解决方案，支持 .NET Core 2.1+, .NET Framework 4.0+.

## 快速开始

> dotnet add package FreeSql.Cloud

or

> Install-Package FreeSql.Cloud

```c#
public enum DbEnum { db1, db2, db3 }

var fsql = new FreeSqlCloud<DbEnum>("myapp"); //提示：泛型可以传入 string
fsql.DistributeTrace = log => Console.WriteLine(log.Split('\n')[0].Trim());

fsql.Register(DbEnum.db1, () => new FreeSqlBuilder()
    .UseConnectionString(DataType.Sqlite, @"Data Source=db1.db")
    .Build());

fsql.Register(DbEnum.db2, () => new FreeSqlBuilder()
    .UseConnectionString(DataType.Sqlite, @"Data Source=db2.db")
    .Build());

fsql.Register(DbEnum.db3, () => new FreeSqlBuilder()
    .UseConnectionString(DataType.Sqlite, @"Data Source=db3.db")
    .Build());

services.AddSingleton<IFreeSql>(fsql);
services.AddSingleton(fsql);
//注入两个类型，稳
```

> FreeSqlCloud 必须定义成单例模式

> new FreeSqlCloud\<DbEnum\>() 多连接管理

> new FreeSqlCloud\<DbEnum\>("myapp") 开启 TCC/SAGA 事务生效

## 如何使用？

FreeSqlCloud 的访问方式和 IFreeSql 一样：

```c#
fsql.Select<T>();
fsql.Insert<T>();
fsql.Update<T>();
fsql.Delete<T>();

//...
```

切换数据库：

```c#
fsql.Change(DbEnum.db3).Select<T>();
//同一线程，或异步await 后续 fsql.Select/Insert/Update/Delete 操作是 db3

fsql.Use(DbEnum.db3).Select<T>();
//单次有效
```
自动定向数据库配置：

```c#
//对 fsql.CRUD 方法名 + 实体类型 进行拦截，自动定向到对应的数据库，达到自动 Change 切换数据库目的
fsql.EntitySteering = (_, e) =>
{
    switch (e.MethodName)
    {
        case "Select":
            if (e.EntityType == typeof(T))
            {
                //查询 T 自动定向 db3
                e.DBKey = DbEnum.db3;
            }
            else if (e.DBKey == DbEnum.db1)
            {
                //此处像不像读写分离？
                var dbkeyIndex = new Random().Next(0, e.AvailableDBKeys.Length);
                e.DBKey = e.AvailableDBKeys[dbkeyIndex]; //重新定向到其他 db
            }
            break;
        case "Insert":
        case "Update":
        case "Delete":
        case "InsertOrUpdate":
            break;
    }
};
```

## 关于并发

FreeSqlCloud 内部使用 IdleBus + AsyncLocal\<string\> 方式实现。

1、FreeSqlCloud 实现了接口 IFreeSql，但它不负责直接交互数据库，只是个代理层。

```c#
public class FreeSqlCloud<TDBKey> : IFreeSql
{
    AsyncLocal<TDBKey> _dbkeyCurrent = new AsyncLocal<TDBKey>();
    IFreeSql _ormCurrent => _idlebus.Get(_dbkeyCurrent.Value);
    IdleBus<TDBKey, IFreeSql> _idlebus;
    ...
    public IAdo Ado => _ormCurrent.Ado;
    public IAop Aop => _ormCurrent.Aop;
    public ICodeFirst CodeFirst => _ormCurrent.CodeFirst;
    public IDbFirst DbFirst => _ormCurrent.DbFirst;
    public GlobalFilter GlobalFilter => _ormCurrent.GlobalFilter;

    public void Transaction(Action handler) => _ormCurrent.Transaction(handler);
    public void Transaction(IsolationLevel isolationLevel, Action handler) => _ormCurrent.Transaction(isolationLevel, handler);
    ...
}
```

2、AsyncLocal 存储执行上下文 DBKey 值，在异步或同步并发场景是安全的，请百度了解。

> 注意：异步不使用 await 会脱离执行上下文

3、fsql.Change(DbEnum.db3) 会改变 AsyncLocal DBKey 值。

> 比 IdleBus.Get 更聪明的返回 IFreeSql 特殊实现，大大降低 IdleBus 误用被释放的异常（原因：IdleBus.Get 返回值不允许被外部变量长期引用，应每次 Get 获取对象）

4、fsql.Select\<T\>() 会调用 IdleBus.Get(AsyncLocal).Select\<T\>()。

> 你还会顾及并发问题吗？

## 关于分布式事务

1、简介

FreeSqlCloud 提供 TCC/SAGA 分布式事务调度，遇错重试、程序重启不影响的事务单元管理功能。

TCC 事务特点：

- Try 用于资源冻结/预扣；
- Try 全部环节通过，代表业务一定能完成，进入 Confirm 环节；
- Try 任何环节失败，代表业务失败，进入 Cancel 环节；
- Confirm 失败会进行重试N次，直到交付成功，或者人工干预；
- Cancel 失败会进行重试N次，直到取消成功，或者人工干预；

> 请看下方示例代码，或者[支付购买商品演示项目](https://github.com/2881099/FreeSql.Cloud/blob/master/examples/net60_tcc_saga/Program.cs)

SAGA 事务特点：

- Commit 用于业务提交；
- Commit 全部环节通过，代表业务交付成功；
- Commit 任何环节失败，代表业务失败，进入 Cancel 环节；
- Cancel 失败会进行重试N次，直到取消成功，或者人工干预；

> 请查看[支付购买商品演示项目](https://github.com/2881099/FreeSql.Cloud/blob/master/examples/net60_tcc_saga/Program.cs)

2、唯一标识

FreeSqlCloud 使用唯一标识区分，达到事务管理互不冲突的目的，举例：

```c#
var fsql = new FreeSqlCloud<DbEnum>("myapp");
var fsql2 = new FreeSqlCloud<DbEnum>("myapp2");
```

fsql2 访问不到 fsql 产生的分布式事务，如果 webapi 程序部署多实例，需要设置多个实例对应的 name，以作区分。

3、持久化

fsql.Register 第一个注册的称之为【主库】，存储 TCC/SAGA 持久数据，当程序重新启动的时候，会将未处理完的事务载入内存重新调度。

自动创建表 tcc_myapp、saga_myapp：

> 提示：fsql2 会创建表 tcc_myapp2、saga_myapp2

| 字段名 | 描述 |
| --- | --- |
| tid | 事务ID |
| title | 事务描述，查看日志更直观 |
| total | 所有单元数量 |
| create_time | 创建时间 |
| finish_time | 完成时间 |
| status | Pending, Confirmed, Canceled, ManualOperation |
| max_retry_count | 最大重试次数，如果仍然失败将转为【人工干预】 |
| retry_interval | 重试间隔(秒) |
| retry_count | 已重试次数 |
| retry_time | 最后重试时间 |

自动创建表 tcc_myapp_unit、saga_myapp_unit：

> 提示：fsql2 会创建表 tcc_myapp2_unit、saga_myapp2_unit

| 字段名 | 描述 |
| --- | --- |
| tid | 事务ID |
| index | 单元下标，1到N |
| description | 单元描述，使用 [Description("xx")] 特性设置，查看日志更直观 |
| stage | Try, Confirm, Cancel |
| type_name | 对应 c# TccUnit/SagaUnit 反射类型信息，用于创建 TccUnit/SagaUnit 对象 |
| state | 状态数据 |
| state_type_name | 状态数据对应的 c# 反射类型信息 |
| create_time | 创建时间 |
| db_key | 用于唤醒时使用 fsql.Use(db_key) 对应的事务或开启事务 |

其他库会创建表 tcc_myapp_unit_invoked、saga_myapp_unit_invoked 判断重复执行

4、单元

TccUnit、SagaUnit 内部支持调用 webapi/grpc，调用异常时会触发重试调度。

由于网络不确定因素，较坏的情况比如单元调用 webapi/grpc 成功，但是 tcc_unit 表保存状态失败，单元又会进入重试调用，最终导致多次调用 webapi/grpc，所以 web/grpc 提供方应该保证幂等操作，无论多少次调用结果都一致。

## TCC 事务

```c#
// 测试数据
fsql.Use(DbEnum.db1).Insert(new User { Id = 1, Name = "testuser01", Point = 10 }).ExecuteAffrows();
fsql.Use(DbEnum.db2).Insert(new Goods { Id = 1, Title = "testgoods01", Stock = 0 }).ExecuteAffrows();

var orderId = Guid.NewGuid();
await fsql.StartTcc(orderId.ToString(), "支付购买",
    new TccOptions
    {
        MaxRetryCount = 10,
        RetryInterval = TimeSpan.FromSeconds(10)
    })
    .Then<Tcc1>(DbEnum.db1, new BuyUnitState { UserId = 1, Point = 10, GoodsId = 1, OrderId = orderId })
    .Then<Tcc2>(DbEnum.db2, new BuyUnitState { UserId = 1, Point = 10, GoodsId = 1, OrderId = orderId })
    .Then<Tcc3>(DbEnum.db3, new BuyUnitState { UserId = 1, Point = 10, GoodsId = 1, OrderId = orderId })
     // 每个单元所需 State 都可以不同
    .ExecuteAsync();
```

```shell
2022-08-16 10:47:53 【app001】db1 注册成功, 并存储 TCC/SAGA 事务相关数据
2022-08-16 10:47:53 【app001】成功加载历史未完成 TCC 事务 0 个
2022-08-16 10:47:53 【app001】成功加载历史未完成 SAGA 事务 0 个
2022-08-16 10:47:53 【app001】TCC (3a9c548f-95b1-43b4-b918-9c3817d4c316, 支付购买) Created successful, retry count: 10, interval: 10S
2022-08-16 10:47:53 【app001】TCC (3a9c548f-95b1-43b4-b918-9c3817d4c316, 支付购买) Unit1(第1步：数据库db1 扣除用户积分) TRY successful
2022-08-16 10:47:53 【app001】数据库使用[Use] db2
2022-08-16 10:47:53 【app001】TCC (3a9c548f-95b1-43b4-b918-9c3817d4c316, 支付购买) Unit2(第2步：数据库db2 扣除库存) TRY failed, ready to CANCEL, -ERR 扣除库存失败
2022-08-16 10:47:53 【app001】TCC (3a9c548f-95b1-43b4-b918-9c3817d4c316, 支付购买) Unit1(第1步：数据库db1 扣除用户积分) CANCEL successful
2022-08-16 10:47:53 【app001】TCC (3a9c548f-95b1-43b4-b918-9c3817d4c316, 支付购买) Completed, all units CANCEL successfully
```

```c#
// 状态数据
class BuyUnitState
{
    public int UserId { get; set; }
    public int Point { get; set; }
    public int GoodsId { get; set; }
    public Guid OrderId { get; set; }
}

[Description("第1步：数据库db1 扣除用户积分")]
class Tcc1 : TccUnit<BuyUnitState>
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
[Description("第2步：数据库db2 扣除库存")]
class Tcc2 : TccUnit<BuyUnitState>
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
[Description("第3步：数据库db3 创建订单")]
class Tcc3 : TccUnit<BuyUnitState>
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

#region db1 实体类
public class User
{
    public int Id { get; set; }
    public string Name { get; set; }
    public int Point { get; set; }
}
#endregion

#region db2 实体类
public class Goods
{
    public int Id { get; set; }
    public string Title { get; set; }
    public int Stock { get; set; }
}
#endregion

#region db3 实体类
public class Order
{
    public Guid Id { get; set; }
    public OrderStatus Status { get; set; }
    public enum OrderStatus { Pending, Success, Canceled }
    public DateTime CreateTime { get; set; }
}
#endregion
```

## Saga 事务

> 请查看 [支付购买商品演示项目](https://github.com/2881099/FreeSql.Cloud/blob/master/examples/net60_tcc_saga/Program.cs)

```c#
var tid = Guid.NewGuid().ToString();
await fsql
    .StartSaga(tid, "发表评论", 
        new SagaOptions
        {
            MaxRetryCount = 5,
            RetryInterval = TimeSpan.FromSeconds(5)
        })
    .Then<Saga1>(new LocalState { Id = 1, Name = "tcc1" })
    .Then<Saga2>()
    .Then<Saga3>(new LocalState { Id = 3, Name = "tcc3" })
    .ExecuteAsync();

// 状态数据
class LocalState
{
    public int Id { get; set; }
    public string Name { get; set; }
}
[Description("第1步")]
class Saga1 : SagaUnit<LocalState>
{
    public override Task Cancel() => throw new Exception("dkdkdk");
    public override Task Commit() => Task.CompletedTask;
}
[Description("第2步")]
class Saga2 : SagaUnit<LocalState>
{
    public override Task Cancel() => Task.CompletedTask;
    public override Task Commit() => Task.CompletedTask;
}
[Description("第3步")]
class Saga3 : SagaUnit<LocalState>
{
    public override Task Cancel() => Task.CompletedTask;
    public override Task Commit() => throw new Exception("xxx");
}
```

执行结果：

```bash
2020-12-02 14:07:30 【myapp】db1 注册成功, 并存储 TCC/SAGA 事务相关数据
2020-12-02 14:07:31 【myapp】成功加载历史未完成 TCC 事务 0 个
2020-12-02 14:07:31 【myapp】成功加载历史未完成 SAGA 事务 0 个
2020-12-02 14:07:31 【myapp】SAGA(e5469b8f-c27f-498a-a0f8-6dd128967dca, 发表评论) Created successful, retry count: 5, interval: 5S
2020-12-02 14:07:31 【myapp】SAGA(e5469b8f-c27f-498a-a0f8-6dd128967dca, 发表评论) Unit1(第1步) COMMIT successful
2020-12-02 14:07:31 【myapp】SAGA(e5469b8f-c27f-498a-a0f8-6dd128967dca, 发表评论) Unit2(第2步) COMMIT successful
2020-12-02 14:07:31 【myapp】SAGA(e5469b8f-c27f-498a-a0f8-6dd128967dca, 发表评论) Unit3(第3步) COMMIT failed, ready to CANCEL, -ERR xxx
2020-12-02 14:07:31 【myapp】SAGA(e5469b8f-c27f-498a-a0f8-6dd128967dca, 发表评论) Unit2(第1步) CANCEL successful
2020-12-02 14:07:31 【myapp】SAGA(e5469b8f-c27f-498a-a0f8-6dd128967dca, 发表评论) Unit1(第1步) CANCEL failed, -ERR dkdkdk
2020-12-02 14:07:36 【myapp】SAGA(e5469b8f-c27f-498a-a0f8-6dd128967dca, 发表评论) Unit1(第1步) CANCEL failed after 1 retries, -ERR dkdkdk
2020-12-02 14:07:41 【myapp】SAGA(e5469b8f-c27f-498a-a0f8-6dd128967dca, 发表评论) Unit1(第1步) CANCEL failed after 2 retries, -ERR dkdkdk
2020-12-02 14:07:47 【myapp】SAGA(e5469b8f-c27f-498a-a0f8-6dd128967dca, 发表评论) Unit1(第1步) CANCEL failed after 3 retries, -ERR dkdkdk
2020-12-02 14:07:52 【myapp】SAGA(e5469b8f-c27f-498a-a0f8-6dd128967dca, 发表评论) Unit1(第1步) CANCEL failed after 4 retries, -ERR dkdkdk
2020-12-02 14:07:57 【myapp】SAGA(e5469b8f-c27f-498a-a0f8-6dd128967dca, 发表评论) Unit1(第1步) CANCEL failed after 5 retries, -ERR dkdkdk
2020-12-02 14:07:57 【myapp】SAGA(e5469b8f-c27f-498a-a0f8-6dd128967dca, 发表评论) Not completed, waiting for manual operation 【人工干预】
```