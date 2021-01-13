using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace net50_webapi_idlebus.Controllers
{
    [ApiController]
    [Route("")]
    public class HomeController : ControllerBase
    {

        [HttpGet]
        public object Get([FromServices] IFreeSql fsql)
        {
            fsql.Change("db2").Ado.ExecuteConnectTest();
            fsql.Change("db3").Ado.ExecuteConnectTest();
            return "";
        }
    }
}
