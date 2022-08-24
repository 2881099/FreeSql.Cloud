using FreeSql;
using FreeSql.Cloud.Abstract;

public static class FreesqlCloudGlobalExtensions
{
    /// <summary>
    /// 创建特殊仓储对象，实现随时跟随 FreeSqlCloud Change 方法切换到对应的数据库<para></para>
    /// _<para></para>
    /// 区别说明：其他方式创建的仓储对象，初始化已经固定 IFreeSql（无法跟随切换）
    /// </summary>
    /// <typeparam name="TEntity"></typeparam>
    /// <param name="that"></param>
    /// <returns></returns>
    public static IBaseRepository<TEntity> GetCloudRepository<TEntity>(this FreeSqlCloudBase that) where TEntity : class
    {
        return new RepositoryCloud<TEntity>(that);
    }
}
