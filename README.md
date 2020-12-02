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

class Tcc1 : TccUnit<LocalState>
{
    public override void Cancel()
    {
        throw new Exception("dkdkdk");
    }
    public override void Confirm() { }
    public override void Try() { }
}
class Tcc2 : TccUnit<LocalState>
{
    public override void Cancel() { }
    public override void Confirm() { }
    public override void Try() { }
}
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
2020-12-02 14:03:34 【app001】db1 注册成功, 并存储 TCC/SAGA 事务相关数据
2020-12-02 14:07:31 【app001】成功加载历史未完成 TCC 事务 0 个
2020-12-02 14:07:31 【app001】成功加载历史未完成 SAGA 事务 0 个
2020-12-02 14:03:35 【app001】TCC(5fec6379-d43e-4d5f-95a2-42ea8710f176, 支付购买) Created successful, retry count: 10, interval: 10S
2020-12-02 14:03:35 【app001】TCC(5fec6379-d43e-4d5f-95a2-42ea8710f176, 支付购买) Unit0 TRY successful
2020-12-02 14:03:35 【app001】TCC(5fec6379-d43e-4d5f-95a2-42ea8710f176, 支付购买) Unit1 TRY successful
2020-12-02 14:03:35 【app001】TCC(5fec6379-d43e-4d5f-95a2-42ea8710f176, 支付购买) Unit2 TRY failed, ready to CANCEL, -ERR xxx
2020-12-02 14:03:35 【app001】TCC(5fec6379-d43e-4d5f-95a2-42ea8710f176, 支付购买) Unit1 CANCEL successful
2020-12-02 14:03:35 【app001】TCC(5fec6379-d43e-4d5f-95a2-42ea8710f176, 支付购买) Unit0 CANCEL failed, -ERR dkdkdk
2020-12-02 14:03:45 【app001】TCC(5fec6379-d43e-4d5f-95a2-42ea8710f176, 支付购买) Unit0 retry again 1 times CANCEL failed, -ERR dkdkdk
2020-12-02 14:03:55 【app001】TCC(5fec6379-d43e-4d5f-95a2-42ea8710f176, 支付购买) Unit0 retry again 2 times CANCEL failed, -ERR dkdkdk
2020-12-02 14:04:06 【app001】TCC(5fec6379-d43e-4d5f-95a2-42ea8710f176, 支付购买) Unit0 retry again 3 times CANCEL failed, -ERR dkdkdk
2020-12-02 14:04:16 【app001】TCC(5fec6379-d43e-4d5f-95a2-42ea8710f176, 支付购买) Unit0 retry again 4 times CANCEL failed, -ERR dkdkdk
2020-12-02 14:04:26 【app001】TCC(5fec6379-d43e-4d5f-95a2-42ea8710f176, 支付购买) Unit0 retry again 5 times CANCEL failed, -ERR dkdkdk
2020-12-02 14:04:36 【app001】TCC(5fec6379-d43e-4d5f-95a2-42ea8710f176, 支付购买) Unit0 retry again 6 times CANCEL failed, -ERR dkdkdk
2020-12-02 14:04:46 【app001】TCC(5fec6379-d43e-4d5f-95a2-42ea8710f176, 支付购买) Unit0 retry again 7 times CANCEL failed, -ERR dkdkdk
2020-12-02 14:04:57 【app001】TCC(5fec6379-d43e-4d5f-95a2-42ea8710f176, 支付购买) Unit0 retry again 8 times CANCEL failed, -ERR dkdkdk
2020-12-02 14:05:07 【app001】TCC(5fec6379-d43e-4d5f-95a2-42ea8710f176, 支付购买) Unit0 retry again 9 times CANCEL failed, -ERR dkdkdk
2020-12-02 14:05:17 【app001】TCC(5fec6379-d43e-4d5f-95a2-42ea8710f176, 支付购买) Unit0 retry again 10 times CANCEL failed, -ERR dkdkdk
2020-12-02 14:05:17 【app001】TCC(5fec6379-d43e-4d5f-95a2-42ea8710f176, 支付购买) Not completed, waiting for manual operation 【人工干预】
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

class Saga1 : SagaUnit<LocalState>
{
    public override void Cancel()
    {
        throw new Exception("dkdkdk");
    }
    public override void Commit() { }
}
class Saga2 : SagaUnit<LocalState>
{
    public override void Cancel() { }
    public override void Commit() { }
}
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
2020-12-02 14:07:30 【app001】db1 注册成功, 并存储 TCC/SAGA 事务相关数据
2020-12-02 14:07:31 【app001】成功加载历史未完成 TCC 事务 0 个
2020-12-02 14:07:31 【app001】成功加载历史未完成 SAGA 事务 0 个
2020-12-02 14:07:31 【app001】SAGA(e5469b8f-c27f-498a-a0f8-6dd128967dca, 发表评论) Created successful, retry count: 5, interval: 5S
2020-12-02 14:07:31 【app001】SAGA(e5469b8f-c27f-498a-a0f8-6dd128967dca, 发表评论) Unit0 COMMIT successful
2020-12-02 14:07:31 【app001】SAGA(e5469b8f-c27f-498a-a0f8-6dd128967dca, 发表评论) Unit1 COMMIT successful
2020-12-02 14:07:31 【app001】SAGA(e5469b8f-c27f-498a-a0f8-6dd128967dca, 发表评论) Unit2 COMMIT failed, ready to CANCEL, -ERR xxx
2020-12-02 14:07:31 【app001】SAGA(e5469b8f-c27f-498a-a0f8-6dd128967dca, 发表评论) Unit1 CANCEL successful
2020-12-02 14:07:31 【app001】SAGA(e5469b8f-c27f-498a-a0f8-6dd128967dca, 发表评论) Unit0 CANCEL failed, -ERR dkdkdk
2020-12-02 14:07:36 【app001】SAGA(e5469b8f-c27f-498a-a0f8-6dd128967dca, 发表评论) Unit0 retry again 1 times CANCEL failed, -ERR dkdkdk
2020-12-02 14:07:41 【app001】SAGA(e5469b8f-c27f-498a-a0f8-6dd128967dca, 发表评论) Unit0 retry again 2 times CANCEL failed, -ERR dkdkdk
2020-12-02 14:07:47 【app001】SAGA(e5469b8f-c27f-498a-a0f8-6dd128967dca, 发表评论) Unit0 retry again 3 times CANCEL failed, -ERR dkdkdk
2020-12-02 14:07:52 【app001】SAGA(e5469b8f-c27f-498a-a0f8-6dd128967dca, 发表评论) Unit0 retry again 4 times CANCEL failed, -ERR dkdkdk
2020-12-02 14:07:57 【app001】SAGA(e5469b8f-c27f-498a-a0f8-6dd128967dca, 发表评论) Unit0 retry again 5 times CANCEL failed, -ERR dkdkdk
2020-12-02 14:07:57 【app001】SAGA(e5469b8f-c27f-498a-a0f8-6dd128967dca, 发表评论) Not completed, waiting for manual operation 【人工干预】
```