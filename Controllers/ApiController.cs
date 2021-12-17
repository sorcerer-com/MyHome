using System;
using System.Linq;

using Microsoft.AspNetCore.Mvc;

using MyHome.Utils;

using NLog;
using NLog.Targets;

namespace MyHome.Controllers
{
    [Route("[controller]")]
    [ApiController]
    public class ApiController : ControllerBase
    {
        private static readonly ILogger logger = LogManager.GetCurrentClassLogger();

        private readonly MyHome myHome;


        public ApiController(MyHome myHome)
        {
            this.myHome = myHome;
        }


        [HttpGet("config")]
        public ActionResult GetConfig()
        {
            return this.Ok(this.myHome.Config.ToUiObject(true));
        }

        [HttpPost("config")]
        public ActionResult SetConfig()
        {
            try
            {
                this.Request.Form.SetObject(this.myHome.Config);
                this.myHome.SystemChanged = true;
                return this.Ok();
            }
            catch (Exception e)
            {
                logger.Error("Failed to set config");
                logger.Debug(e);
                return this.BadRequest(e.Message);
            }
        }


        [HttpGet("logs")]
        public ActionResult GetLogs()
        {
            var memoryTarget = LogManager.Configuration.FindTargetByName<MemoryTarget>("memory");
            return this.Ok(memoryTarget.Logs);
        }

        [HttpGet("upgrade")]
        public ActionResult GetUpgradeAvailable()
        {
            return this.Ok(this.myHome.UpgradeAvailable);
        }

        [HttpPost("upgrade")]
        public ActionResult Upgrade()
        {
            if (this.myHome.Upgrade())
                return this.Restart();

            return this.Conflict();
        }

        [HttpPost("restart")]
        public ActionResult Restart()
        {
            System.Threading.Tasks.Task.Delay(100).ContinueWith(_ => Environment.Exit(0));
            return this.Ok();
        }


        [HttpGet("types/{typeName}")]
        public ActionResult GetSubTypes(string typeName)
        {
            var type = Utils.Utils.GetType(typeName);
            if (type == null)
                return this.NotFound("Invalid type name: " + typeName);

            return this.Ok(type.GetSubClasses().Select(t => t.Name));
        }
    }
}
