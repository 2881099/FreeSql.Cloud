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
        public IFreeSql fsql { get; private set; }

        public void ConfigureServices(IServiceCollection services)
        {
            fsql = new MultiFreeSql();
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
                db.Aop.CommandAfter += (_, e) =>
                {
                    var logger = _serviceProvider.GetService<ILogger<Startup>>();
                    logger.LogDebug(e.Command.CommandText);
                };
                return db;
            });

            services.AddSingleton<IFreeSql>(fsql);
            services.AddControllers();
        }

        IServiceProvider _serviceProvider;

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            _serviceProvider = app.ApplicationServices;

            if (env.IsDevelopment())
                app.UseDeveloperExceptionPage();

            app.Use(async (context, next) =>
            {
                fsql.Change("db2");

                try
                {
                    await next();
                }
                finally
                {
                    fsql.Change("db1");
                }
            });

            app.UseRouting();
            app.UseAuthorization();
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
        }
    }
}
