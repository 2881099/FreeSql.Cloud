using FreeSql;
using FreeSql.Cloud.Abstract;
using FreeSql.Internal;
using net60_webapi;
using Rougamo.Context;
using System.Data;
using static System.Net.Mime.MediaTypeNames;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton(DB.Cloud); //注入 FreeSqlCloud<DbEnum>
builder.Services.AddSingleton(provider => DB.Cloud.Use(DbEnum.db1)); //注入 db1 IFreeSql
builder.Services.AddScoped<UnitOfWorkManagerCloud>();

//批量注入 Repository
builder.Services.AddScoped(typeof(IBaseRepository<>), typeof(RepositoryCloud<>)); //db1
builder.Services.AddScoped(typeof(BaseRepository<>), typeof(RepositoryCloud<>)); //db1
builder.Services.AddScoped(typeof(IBaseRepository<,>), typeof(RepositoryCloud<,>)); //db1
builder.Services.AddScoped(typeof(BaseRepository<,>), typeof(RepositoryCloud<,>)); //db1
foreach (var repositoryType in typeof(User).Assembly.GetTypes().Where(a => a.IsAbstract == false && typeof(IBaseRepository).IsAssignableFrom(a)))
    builder.Services.AddScoped(repositoryType);

var repo1 = DB.Cloud.GetCloudRepository<User>();
Console.WriteLine(repo1.Orm.Ado.ConnectionString);
DB.Cloud.Change(DbEnum.db2);
Console.WriteLine(repo1.Orm.Ado.ConnectionString);
DB.Cloud.Change(DbEnum.db3);
Console.WriteLine(repo1.Orm.Ado.ConnectionString);
DB.Cloud.Change(DbEnum.db1);
Console.WriteLine(repo1.Orm.Ado.ConnectionString);

builder.Services.AddScoped<UserService>();

var app = builder.Build();

app.Use(async (context, next) =>
{
    TransactionalAttribute.SetServiceProvider(context.RequestServices);
    await next();
});

app.MapGet("/", async context =>
{
    var _userService = context.RequestServices.GetService<UserService>();
    _userService.Test01();

    await context.Response.WriteAsync("hello word");
});

AsyncLocalAccessor<int> access = new AsyncLocalAccessor<int>(() => 100);
await access.SetValue();
Console.WriteLine($"ValueAccessor before await FooAsync in Main: {access.Value}");

test();
app.Run();


void test()
{
	var fsql = new FreeSqlCloud2();
	fsql.Register(DbEnum2.ojDb3D, () =>
		new FreeSqlBuilder()
			.UseConnectionFactory(DataType.PostgreSQL, () => null)
			.UseNameConvert(NameConvertType.ToLower).Build());
	fsql.Register(DbEnum2.ChDb, () =>
		new FreeSqlBuilder()
			.UseConnectionFactory(DataType.Oracle, () => null)
			.UseNameConvert(NameConvertType.ToUpper).Build());

	Console.WriteLine(fsql.Ado.DataType);
	using (fsql.Change(DbEnum2.ChDb))
	{
		Console.WriteLine(fsql.Ado.DataType);
		using (fsql.Change(DbEnum2.ojDb3D))
		{
			Console.WriteLine(fsql.Ado.DataType);
		}
		Console.WriteLine(fsql.Ado.DataType);
	}
	Console.WriteLine(fsql.Ado.DataType);
}
public class FreeSqlCloud2 : FreeSqlCloud<DbEnum2>
{
	public FreeSqlCloud2() : base(null) { }
	public FreeSqlCloud2(string distributekey) : base(distributekey) { }
}
public enum DbEnum2
{
	ChDb = 0,
	ojDb3D = 1
}

class UserService
{
    readonly IBaseRepository<User> m_repo1;
    readonly BaseRepository<User> m_repo2;
    readonly UserRepository m_repo3;
    public UserService(IBaseRepository<User> repo1, BaseRepository<User> repo2, UserRepository repo3)
    {
        m_repo1 = repo1;
        m_repo2 = repo2;
        m_repo3 = repo3;
    }

    public void Test01()
    {
        Console.WriteLine("aaa");

        Test02().Wait();

        Console.WriteLine("bbb");
    }

    [Transactional(DbEnum.db1, Propagation = Propagation.Required)] //db1
    [Transactional(DbEnum.db3)] //db3
    async public Task Test02()
    {
        Console.WriteLine("xxx");

        Test03();

        Console.WriteLine("yyy");

        await Task.CompletedTask;
    }

    [Transactional(DbEnum.db2, Propagation = Propagation.Never)] //db1
    public void Test03()
    {
        Console.WriteLine("zzz");
    }
}

class UserRepository : RepositoryCloud<User>, IBaseRepository<User>
{
    public UserRepository(UnitOfWorkManagerCloud uowm) : base(DbEnum.db3, uowm) { }

    //todo..
}

class UnitOfWorkManagerCloud
{
    readonly Dictionary<string, UnitOfWorkManager> m_managers = new Dictionary<string, UnitOfWorkManager>();
    readonly FreeSqlCloud m_cloud;
    public UnitOfWorkManagerCloud(IServiceProvider serviceProvider)
    {
        m_cloud = serviceProvider.GetService<FreeSqlCloud>();
    }

    public void Dispose()
    {
        foreach(var uowm in m_managers.Values)
        {
            uowm.Dispose();
        }
        m_managers.Clear();
    }
    public IUnitOfWork Begin(string db, Propagation propagation = Propagation.Required, IsolationLevel? isolationLevel = null)
    {
        return GetUnitOfWorkManager(db).Begin(propagation, isolationLevel);
    }
    public UnitOfWorkManager GetUnitOfWorkManager(string db)
    {
        if (m_managers.TryGetValue(db, out var uowm) == false)
        {
            uowm = new UnitOfWorkManager(m_cloud.Use(db));
            m_managers.Add(db, uowm);
        }
        return uowm;
    }
}

class RepositoryCloud<T> : DefaultRepository<T, int> where T : class
{
    public RepositoryCloud(UnitOfWorkManagerCloud uomw) : this(DbEnum.db1, uomw) { } //DI
    public RepositoryCloud(DbEnum db, UnitOfWorkManagerCloud uomw) : this(uomw.GetUnitOfWorkManager(db.ToString())) { }
    RepositoryCloud(UnitOfWorkManager uomw) : base(uomw.Orm, uomw)
    {
        uomw.Binding(this);
    }
}
class RepositoryCloud<T, TKey> : DefaultRepository<T, TKey> where T : class
{
    public RepositoryCloud(UnitOfWorkManagerCloud uomw) : this(DbEnum.db1, uomw) { } //DI
    public RepositoryCloud(DbEnum db, UnitOfWorkManagerCloud uomw) : this(uomw.GetUnitOfWorkManager(db.ToString())) { }
    RepositoryCloud(UnitOfWorkManager uomw) : base(uomw.Orm, uomw)
    {
        uomw.Binding(this);
    }
}

[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public class TransactionalAttribute : Rougamo.MoAttribute
{
    public Propagation Propagation { get; set; } = Propagation.Required;
    public IsolationLevel IsolationLevel { get => m_IsolationLevel.Value; set => m_IsolationLevel = value; }
    IsolationLevel? m_IsolationLevel;
    readonly DbEnum m_db;

    public TransactionalAttribute(DbEnum db)
    {
        m_db = db;
    }

    static AsyncLocal<IServiceProvider> m_ServiceProvider = new AsyncLocal<IServiceProvider>();
    public static void SetServiceProvider(IServiceProvider serviceProvider) => m_ServiceProvider.Value = serviceProvider;

    IUnitOfWork _uow;
    public override void OnEntry(MethodContext context)
    {
        var uowManager = m_ServiceProvider.Value.GetService<UnitOfWorkManagerCloud>();
        _uow = uowManager.Begin(m_db.ToString(), this.Propagation, this.m_IsolationLevel);
    }
    public override void OnExit(MethodContext context)
    {
        if (typeof(Task).IsAssignableFrom(context.RealReturnType))
            ((Task)context.ReturnValue).ContinueWith(t => _OnExit());
        else _OnExit();

        void _OnExit()
        {
            try
            {
                if (context.Exception == null) _uow.Commit();
                else _uow.Rollback();
            }
            finally
            {
                _uow.Dispose();
            }
        }
    }
}

public static class Extension
{
    public static async Task SetValue(this AsyncLocalAccessor<int> face)
    {
        face.Value = 200;
        await Task.Delay(100);
    }
}