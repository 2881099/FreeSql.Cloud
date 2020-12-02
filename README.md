<h1 align="center"> 🦄 FreeSql.Cloud </h1>

为 FreeSql 提供跨数据库、TCC事务解决方案。

## 快速开始

```c#
var fsql = new FreeSqlCloud("myapp");
fsql.TccTrace += (_, log) => Console.WriteLine(log.Split('\n')[0].Trim());

fsql.Register("db1", () => new FreeSqlBuilder()
    .UseConnectionString(FreeSql.DataType.Sqlite, @"Data Source=db1.db")
    .Build());

fsql.Register("db2", () => new FreeSqlBuilder()
    .UseConnectionString(FreeSql.DataType.Sqlite, @"Data Source=db2.db")
    .Build());

fsql.Register("db3", () => new FreeSqlBuilder()
    .UseConnectionString(FreeSql.DataType.Sqlite, @"Data Source=db3.db")
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
fsql.Change("db3").Select<T>();
//以后所有 fsql.Select/Insert/Update/Delete 操作是 db3
```

## TCC 事务

```c#
var tid = Guid.NewGuid().ToString();
await fsql.StartTcc(tid, "创建订单")
    .Then(typeof(Tcc1), "db1", new TccState { Id = 1, Name = "tcc1" })
    .Then(typeof(Tcc2), "db2")
    .Then(typeof(Tcc3), "db3", new TccState { Id = 3, Name = "tcc3" })
    .ExecuteAsync();

// 状态数据
class TccState
{
    public int Id { get; set; }
    public string Name { get; set; }
}

class Tcc1 : TccUnit<TccState>
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
```