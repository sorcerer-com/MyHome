using System;
using System.Linq;
using System.Reflection;

using Microsoft.AspNetCore.Mvc;

using MyHome.Utils;

using Newtonsoft.Json;

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
            return this.Ok(this.myHome.Config.ToUiObject());
        }

        [HttpPost("config")]
        public ActionResult SetConfig()
        {
            try
            {
                var body = Newtonsoft.Json.Linq.JToken.Parse(new System.IO.StreamReader(this.Request.Body).ReadToEndAsync().Result);
                body.SetObject(this.myHome.Config);
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
            this.myHome.Stop();
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

        [HttpGet("typescript-models")]
        public ActionResult GetTypescriptModels()
        {
            var models = Assembly.GetExecutingAssembly().ToTypescript() + "\n";
            // DateTime
            models += typeof(DateTime).ToTypescript() + "\n\n";
            // TimeSpan
            models += typeof(TimeSpan).ToTypescript() + "\n\n";
            // Task
            models += typeof(System.Threading.Tasks.Task).ToTypescript() + "\n\n";
            // Logger
            models += typeof(Logger).ToTypescript() + "\nlet logger: Logger;\n\n";
            // JSON
            models += "class JSON {\n  static parse(text: string): any { }\n  static stringify(value: any): string { }\n}\n\n\n";

            models += "var globals = {};\n\n";
            models += "let myHome: MyHome;\n\n";
            var rooms = this.myHome.Rooms.ToDictionary(r => r.Name.Replace(" ", ""), r => $"new {r.GetType().Name}()");
            models += $"let Rooms = {JsonConvert.SerializeObject(rooms, Formatting.Indented).Replace("\"", "")}\n\n";
            var devices = this.myHome.Rooms.ToDictionary(r => r.Name.Replace(" ", ""), r => r.Devices.ToDictionary(d => d.Name.Replace(" ", ""), d => $"new {d.GetType().Name}()"));
            models += $"let Devices = {JsonConvert.SerializeObject(devices, Formatting.Indented).Replace("\"", "")}\n\n";

            return this.Ok(models);
        }
    }
}
