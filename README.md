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

## 关于并发

FreeSqlCloud 内部使用 IdleBus + AsyncLocal\<string\> 方式实现。

1、AsyncLocal 存储执行上下文 DBKey 值，它在异步或同步并发场景是安全的，请百度了解。

> 注意：异步不使用 await 会脱离执行上下文

2、fsql.Change(DbEnum.db3) 会改变 AsyncLocal 值。

> 说明：fsql.Change 比 IdleBus.Get 更聪明的返回 IFreeSql 特殊实现，不会出现 IdleBus 被释放的错误，IdleBus.Get 不允许被外部变量长期引用。

3、fsql.Select\<T\>() 会调用 IdleBus.Get(AsyncLocal).Select\<T\>()。

> 你还会顾及并发问题吗？

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
//以后所有 fsql.Select/Insert/Update/Delete 操作是 db3
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

## 关于分布式事务

1、简介

FreeSqlCloud 提供 TCC/SAGA 分布式事务调度，遇错重试、程序重启不影响的事务单元管理功能。

2、唯一标识

FreeSqlCloud 使用唯一标识区分，达到事务管理互不冲突的目的，举例：

```c#
var fsql = new FreeSqlCloud<DbEnum>("myapp");
var fsql2 = new FreeSqlCloud<DbEnum>("myapp2");
```

fsql2 访问不到 fsql 产生的事务，如果我们的 webapi 程序发布多实例，需要设置多个实例对应的 name，以作区分。

3、主库

fsql.Register 第一个注册的称之为【主库】，存储 TCC/SAGA 相关数据，当程序重新启动的时候，会将未处理完的事务载入内存重新调度。

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
| retry_time | 重试时间 |

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

4、单元

TccUnit、SagaUnit 内部支持调用 webapi/grpc，当调用异常触发重试调度。

由于网络不确定因素，较坏的情况比如单元调用 webapi/grpc 成功，但是 tcc_unit 表保存状态失败，单元又会进入重试调用，最终导致多次调用 webapi/grpc，所以 web/grpc 提供方应该保证幂等操作，无论多少次调用结果都一致。

## TCC 事务

```c#
var tid = Guid.NewGuid().ToString();
await fsql
    .StartTcc(tid, "支付购买", 
        new TccOptions
        {
            MaxRetryCount = 10,
            RetryInterval = TimeSpan.FromSeconds(10)
        })
    .Then<Tcc1>(new LocalState { Id = 1, Name = "tcc1" })
    .Then<Tcc2>()
    .Then<Tcc3>(new LocalState { Id = 3, Name = "tcc3" })
    .ExecuteAsync();

// 状态数据
class LocalState
{
    public int Id { get; set; }
    public string Name { get; set; }
}
[Description("第1步")]
class Tcc1 : TccUnit<LocalState>
{
    public override Task Cancel() => throw new Exception("dkdkdk");
    public override Task Confirm() => Task.CompletedTask;
    public override Task Try() => Task.CompletedTask;
}
[Description("第2步")]
class Tcc2 : TccUnit<LocalState>
{
    public override Task Cancel() => Task.CompletedTask;
    public override Task Confirm() => Task.CompletedTask;
    public override Task Try() => Task.CompletedTask;
}
[Description("第3步")]
class Tcc3 : TccUnit<LocalState>
{
    public override Task Cancel() => Task.CompletedTask;
    public override Task Confirm() => Task.CompletedTask;
    public override Task Try() => throw new Exception("xxx");
}
```

执行结果：

```bash
2020-12-02 14:03:34 【myapp】db1 注册成功, 并存储 TCC/SAGA 事务相关数据
2020-12-02 14:07:31 【myapp】成功加载历史未完成 TCC 事务 0 个
2020-12-02 14:07:31 【myapp】成功加载历史未完成 SAGA 事务 0 个
2020-12-02 14:03:35 【myapp】TCC (5fec6379-d43e-4d5f-95a2-42ea8710f176, 支付购买) Created successful, retry count: 10, interval: 10S
2020-12-02 14:03:35 【myapp】TCC (5fec6379-d43e-4d5f-95a2-42ea8710f176, 支付购买) Unit1(第1步) TRY successful
2020-12-02 14:03:35 【myapp】TCC (5fec6379-d43e-4d5f-95a2-42ea8710f176, 支付购买) Unit2(第2步) TRY successful
2020-12-02 14:03:35 【myapp】TCC (5fec6379-d43e-4d5f-95a2-42ea8710f176, 支付购买) Unit3(第3步) TRY failed, ready to CANCEL, -ERR xxx
2020-12-02 14:03:35 【myapp】TCC (5fec6379-d43e-4d5f-95a2-42ea8710f176, 支付购买) Unit2(第2步) CANCEL successful
2020-12-02 14:03:35 【myapp】TCC (5fec6379-d43e-4d5f-95a2-42ea8710f176, 支付购买) Unit1(第1步) CANCEL failed , -ERR dkdkdk
2020-12-02 14:03:45 【myapp】TCC (5fec6379-d43e-4d5f-95a2-42ea8710f176, 支付购买) Unit1(第1步) CANCEL failed retry again 1 times , -ERR dkdkdk
2020-12-02 14:03:55 【myapp】TCC (5fec6379-d43e-4d5f-95a2-42ea8710f176, 支付购买) Unit1(第1步) CANCEL failed retry again 2 times , -ERR dkdkdk
2020-12-02 14:04:06 【myapp】TCC (5fec6379-d43e-4d5f-95a2-42ea8710f176, 支付购买) Unit1(第1步) CANCEL failed retry again 3 times , -ERR dkdkdk
2020-12-02 14:04:16 【myapp】TCC (5fec6379-d43e-4d5f-95a2-42ea8710f176, 支付购买) Unit1(第1步) CANCEL failed retry again 4 times , -ERR dkdkdk
2020-12-02 14:04:26 【myapp】TCC (5fec6379-d43e-4d5f-95a2-42ea8710f176, 支付购买) Unit1(第1步) CANCEL failed retry again 5 times , -ERR dkdkdk
2020-12-02 14:04:36 【myapp】TCC (5fec6379-d43e-4d5f-95a2-42ea8710f176, 支付购买) Unit1(第1步) CANCEL failed retry again 6 times , -ERR dkdkdk
2020-12-02 14:04:46 【myapp】TCC (5fec6379-d43e-4d5f-95a2-42ea8710f176, 支付购买) Unit1(第1步) CANCEL failed retry again 7 times , -ERR dkdkdk
2020-12-02 14:04:57 【myapp】TCC (5fec6379-d43e-4d5f-95a2-42ea8710f176, 支付购买) Unit1(第1步) CANCEL failed retry again 8 times , -ERR dkdkdk
2020-12-02 14:05:07 【myapp】TCC (5fec6379-d43e-4d5f-95a2-42ea8710f176, 支付购买) Unit1(第1步) CANCEL failed retry again 9 times , -ERR dkdkdk
2020-12-02 14:05:17 【myapp】TCC (5fec6379-d43e-4d5f-95a2-42ea8710f176, 支付购买) Unit1(第1步) CANCEL failed retry again 10 times , -ERR dkdkdk
2020-12-02 14:05:17 【myapp】TCC (5fec6379-d43e-4d5f-95a2-42ea8710f176, 支付购买) Not completed, waiting for manual operation 【人工干预】
```

## Saga 事务

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