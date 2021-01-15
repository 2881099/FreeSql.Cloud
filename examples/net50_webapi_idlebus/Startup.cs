using FreeSql;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace net50_webapi_idlebus
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        public void ConfigureServices(IServiceCollection services)
        {
            var fsql = new MultiFreeSql();
            fsql.Register("db1", () =>
            {
                var db = new FreeSqlBuilder().UseConnectionString(DataType.Sqlite, "data source=db1.db").Build();
                //db.Aop.CommandAfter += ...
                return db;
            });
            fsql.Register("db2", () =>
            {
                var db = new FreeSqlBuilder().UseConnectionString(DataType.Sqlite, "data source=db2.db").Build();
                //db.Aop.CommandAfter += ...
                return db;
            });
            fsql.Register("db3", () =>
            {
                var db = new FreeSqlBuilder().UseConnectionString(DataType.Sqlite, "data source=db3.db").Build();
                //db.Aop.CommandAfter += ...
                return db;
            });

            services.AddSingleton<IFreeSql>(fsql);
            services.AddControllers();
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
                app.UseDeveloperExceptionPage();

            app.UseRouting();
            app.UseAuthorization();
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
        }
    }
}
