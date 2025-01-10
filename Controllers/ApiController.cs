using System;
using System.IO;
using System.Linq;
using System.Net.Mime;
using System.Reflection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MyHome.Systems;
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
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();

        private readonly MyHome myHome;


        public ApiController(MyHome myHome)
        {
            this.myHome = myHome;
        }


        [HttpGet("status")]
        public ActionResult GetStatus()
        {
            return this.Ok();
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
                var body = Newtonsoft.Json.Linq.JToken.Parse(new StreamReader(this.Request.Body).ReadToEndAsync().Result);
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

        [HttpGet("logFile")]
        public ActionResult GetLogFile()
        {
            var filePath = Path.Join(Models.Config.BinPath, "log.log");
            var file = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            return this.File(file, MediaTypeNames.Text.Plain);
        }

        [HttpGet("map")]
        public ActionResult GetMap()
        {
            var filePath = Path.Join(Models.Config.BinPath, "map.png");
            if (!System.IO.File.Exists(filePath))
                return this.NotFound();
            var file = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            return this.File(file, MediaTypeNames.Image.Jpeg);
        }

        [HttpPost("map")]
        public ActionResult SetMap([FromForm] IFormFile file)
        {
            using var stream = System.IO.File.Create(Path.Join(Models.Config.BinPath, "map.png"));
            file.CopyTo(stream);
            return this.Ok(file);
        }

        [HttpGet("notifications")]
        public ActionResult GetNotifications()
        {
            return this.Ok(this.myHome.Notifications.Select(n => n.ToUiObject()));
        }

        [HttpPost("notifications/{message}/delete")]
        public ActionResult RemoveNotification(string message)
        {
            this.myHome.RemoveNotification(message);
            return this.Ok();
        }

        [HttpPost("notifications/{message}/snooze")]
        public ActionResult SnoozeAlert(string message, int interval)
        {
            var notification = this.myHome.Notifications.FirstOrDefault(n => n.Message() == message);
            if (notification == null)
                return this.NotFound();
            notification.Snooze(TimeSpan.FromHours(interval));
            return this.Ok();
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


        [HttpGet("songs/{fileName}")]
        public ActionResult GetSong(string fileName)
        {
            var filePath = Path.Join(MyHome.Instance.Config.SongsPath, fileName);
            if (!System.IO.File.Exists(filePath))
                filePath = Path.Join(Models.Config.SoundsPath, fileName);

            if (!System.IO.File.Exists(filePath))
            {
                var assistant = this.myHome.Systems[nameof(AssistantSystem)] as AssistantSystem;
                if (assistant.SpeakResponses.TryGetValue(fileName, out var response))
                {
                    var stream = new MemoryStream(response.data);
                    return this.File(stream, "audio/wav");
                }
                return this.NotFound($"Song '{fileName}' not found");
            }

            // match buffer with the one in Tasmota
            var file = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 64 * 1024);
            return this.File(file, "audio/mpeg");
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
