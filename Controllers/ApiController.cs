using System;
using System.IO;
using System.Linq;

using Microsoft.AspNetCore.Mvc;

using MyHome.Utils;

using Newtonsoft.Json.Linq;

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


        [HttpPost("sensor/data")]
        public ActionResult ProcessSensorData()
        {
            // curl "http://127.0.0.1:5000/api/sensor/data" -i -X POST -H "token: c9dd348f9b48020e5d0a7204d5ce6eb8" -H "Content-Type: application/json" -d '[{"name": "test", "value": '$((1 + RANDOM % 100))'}]'
            try
            {
                if (this.Request.Headers.ContainsKey("token"))
                {
                    using var reader = new StreamReader(this.Request.Body);
                    var body = reader.ReadToEndAsync().Result;
                    var json = JArray.Parse(body);
                    if (this.myHome.DevicesSystem.ProcessSensorData(this.Request.Headers["token"], json))
                        return this.Ok();
                }
            }
            catch (Exception e)
            {
                logger.Error("Failed to process external sensor data");
                logger.Debug(e);
            }
            return this.NotFound();
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
        public ActionResult GetDeviceTypes(string typeName)
        {
            var type = Utils.Utils.GetType(typeName);
            if (type == null)
                return this.NotFound("Invalid type name: " + typeName);

            return this.Ok(type.GetSubClasses().Select(t => t.Name));
        }
    }
}
