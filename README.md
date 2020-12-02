<h1 align="center"> 🦄 FreeSql.Cloud </h1>

为 FreeSql 提供跨数据库访问，分布式事务TCC、SAGA解决方案。

## 快速开始

```c#
public enum DbEnum { db1, db2, db3 }

var fsql = new FreeSqlCloud<DbEnum>("myapp"); //提示：泛型可以传入 string
fsql.DistributeTrace += (_, log) => Console.WriteLine(log.Split('\n')[0].Trim());

fsql.Register(DbEnum.db1, () => new FreeSqlBuilder()
    .UseConnectionString(DataType.Sqlite, @"Data Source=db1.db")
    .Build());

fsql.Register(DbEnum.db2, () => new FreeSqlBuilder()
    .UseConnectionString(DataType.Sqlite, @"Data Source=db2.db")
    .Build());

fsql.Register(DbEnum.db3, () => new FreeSqlBuilder()
    .UseConnectionString(DataType.Sqlite, @"Data Source=db3.db")
    .Build());
```

> FreeSqlCloud 必须定义成单例模式

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

## 关于分布式事务

1、唯一标识

FreeSqlCloud 使用唯一标识区分，从而达到事务管理互不冲突的目的，举例：

```c#
var fsql = new FreeSqlCloud<DbEnum>("myapp");
var fsql2 = new FreeSqlCloud<DbEnum>("myapp2");
```

fsql2 访问不到 fsql 产生的事务，如果我们的 webapi 程序发布多实例，需要设置多个实例对应的 name，以作区分。

2、主库

fsql.Register 第一个注册的称之为【主库】，存储 TCC/SAGA 相关数据，当程序重新启动的时候，会将未处理完的事务载入内存重新调度。

主库会自动创建表 tcc_myapp、saga_myapp：

> 提示：fsql2 会创建表 tcc_myapp2、saga_myapp2

| 字段名 | 描述 |
| --- | --- |
| tid | 事务ID |
| title | 事务描述，查看日志更直观 |
| total | 所有单元数量 |
| units | 所有单元重要信息 |
| create_time | 创建时间 |
| finish_time | 完成时间 |
| status | Pending, Confirmed, Canceled, ManualOperation |
| max_retry_count | 最大重试次数，如果仍然失败将转为【人工干预】 |
| retry_interval | 重试间隔(秒) |
| retry_count | 已重试次数 |
| retry_time | 重试时间 |

3、单元库

fsql.Register 第一个和第N个注册的称之为【单元库】，请注意【主库】也是【单元库】，实现该单元内的强一致性、及相关数据存储。

每个单元库会自动创建表 tcc_myapp_unit、saga_myapp_unit：

| 字段名 | 描述 |
| --- | --- |
| tid | 事务ID |
| index | 单元下标，1到N |
| description | 单元描述，使用 [Description("xx")] 特性设置，查看日志更直观 |
| stage | Try, Confirm, Cancel |
| db_key | fsql.Register 对应的 key |
| type_name | 对应 c# TccUnit/SagaUnit 反射类型信息，用于创建 TccUnit/SagaUnit 对象 |
| isolation_level | 单元事务隔离级别 |
| state | 状态数据 |
| state_type_name | 状态数据对应的 c# 反射类型信息 |
| create_time | 创建时间 |

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
    .Then<Tcc1>(DbEnum.db1, new LocalState { Id = 1, Name = "tcc1" })
    .Then<Tcc2>(DbEnum.db2)
    .Then<Tcc3>(DbEnum.db3, new LocalState { Id = 3, Name = "tcc3" })
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
    public override void Cancel()
    {
        throw new Exception("dkdkdk");
    }
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
    public override void Try()
    {
        throw new Exception("xxx");
    }
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
    .Then<Saga1>(DbEnum.db1, new LocalState { Id = 1, Name = "tcc1" })
    .Then<Saga2>(DbEnum.db2)
    .Then<Saga3>(DbEnum.db3, new LocalState { Id = 3, Name = "tcc3" })
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
    public override void Cancel()
    {
        throw new Exception("dkdkdk");
    }
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
    public override void Commit()
    {
        throw new Exception("xxx");
    }
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