using System;
using System.IO;
using System.Linq;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

using MyHome.Systems.Devices;
using MyHome.Utils;

using Newtonsoft.Json.Linq;

using NLog;
using NLog.Targets;

namespace MyHome
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


        [HttpGet("rooms")]
        public ActionResult GetRooms()
        {
            return this.Ok(this.myHome.Rooms.Select(r => r.ToUiObject()));
        }

        [HttpPost("rooms/{roomName}")]
        public ActionResult SetRoom(string roomName)
        {
            try
            {
                var room = this.myHome.Rooms.FirstOrDefault(r => r.Name == roomName);
                if (room == null)
                    return this.NotFound("Room not found");

                var roomType = room.GetType();
                foreach (var item in this.Request.Form)
                {
                    var prop = roomType.GetProperty(item.Key);
                    if (prop == null || !prop.CanWrite)
                        continue;

                    var value = item.Value.ToString();
                    // TODO: extract to function (in Camera.cs used too, maybe more places)
                    if (value == "true" || value == "false")
                        prop.SetValue(room, value == "true");
                    else
                        prop.SetValue(room, value);
                }
                return this.Ok();
            }
            catch (Exception e)
            {
                logger.Error(e, $"Failed to set room '${roomName}'");
                return this.BadRequest(e.Message);
            }
        }


        [HttpGet("systems/{systemName}")]
        public ActionResult GetSystem(string systemName)
        {
            var system = this.myHome.Systems.Values.FirstOrDefault(s => s.Name.ToLower() == systemName.ToLower());
            if (system != null)
                return this.Ok(system.ToUiObject());
            return this.NotFound("System not found");
        }

        [HttpPost("systems/{systemName}/{funcName}")]
        public ActionResult CallSystem(string systemName, string funcName)
        {
            try
            {
                var system = this.myHome.Systems.Values.FirstOrDefault(s => s.Name.ToLower() == systemName.ToLower());
                if (system == null)
                    return this.NotFound("System not found");

                var method = system.GetType().GetMethod(funcName);
                if (method != null)
                {
                    var args = this.Request.HasFormContentType ? this.Request.Form.Select(kvp => kvp.Value.ToString()) : Array.Empty<string>(); // TODO: convert to real types (above TODO)
                    var result = method.Invoke(system, args.ToArray());
                    return this.Ok(result);
                }
                return this.NotFound("No such function");
            }
            catch (Exception e)
            {
                logger.Error(e, $"Failed to call '{systemName}' system's function '${funcName}'");
                return this.BadRequest(e.Message);
            }
        }


        [HttpGet("sensors/{sensorName}/data/{valueType}")]
        public ActionResult GetSensorData(string sensorName, string valueType)
        {
            var sensor = this.myHome.DevicesSystem.Sensors.FirstOrDefault(s => s.Name == sensorName);
            if (sensor == null)
                return this.NotFound("Sensor not found");

            var now = DateTime.Now;
            var prevDayTime = new DateTime(now.Year, now.Month, now.Day, now.Hour, 0, 0, 0) - TimeSpan.FromDays(1);
            var subData = sensor.Data.Where(kvp => kvp.Value.ContainsKey(valueType)).ToDictionary(kvp => kvp.Key, kvp => kvp.Value[valueType]);
            var result = new
            {
                lastDay = subData.Where(kvp => kvp.Key >= prevDayTime).ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
                lastYear = subData.Where(kvp => kvp.Key < prevDayTime).ToDictionary(kvp => kvp.Key, kvp => kvp.Value)
            };

            return this.Ok(result);
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
                logger.Error(e, "Failed to process external sensor data");
            }
            return this.NotFound();
        }

        [HttpGet("cameras/{cameraName}/image")]
        public void GetCameraImage(string cameraName)
        {
            var camera = this.myHome.DevicesSystem.Cameras.FirstOrDefault(c => c.Name == cameraName);
            if (camera == null)
            {
                this.Response.StatusCode = StatusCodes.Status404NotFound;
                this.Response.Body.WriteAsync(System.Text.Encoding.UTF8.GetBytes("Camera not found")).AsTask();
                return;
            }

            try
            {
                this.Response.ContentType = "multipart/x-mixed-replace;boundary=frame";

                while (true)
                {
                    var imageBytes = camera.GetImage().ToBytes(".jpg");

                    var header = $"--frame\r\nContent-Type: image/jpeg\r\nContent-Length: {imageBytes.Length}\r\n\r\n";
                    var headerData = System.Text.Encoding.UTF8.GetBytes(header);
                    var newLine = System.Text.Encoding.UTF8.GetBytes("\r\n");
                    this.Response.Body.WriteAsync(headerData, 0, headerData.Length, this.HttpContext.RequestAborted);
                    this.Response.Body.WriteAsync(imageBytes, 0, imageBytes.Length, this.HttpContext.RequestAborted);
                    this.Response.Body.WriteAsync(newLine, 0, newLine.Length, this.HttpContext.RequestAborted);

                    if (this.HttpContext.RequestAborted.IsCancellationRequested)
                        break;
                    System.Threading.Thread.Sleep(50); // sleep 50 ms for 20 FPS
                }
            }
            catch (OperationCanceledException)
            {
                // connection closed, no need to report this
            }
            return;
        }

        [HttpPost("cameras/{cameraName}/move")]
        public ActionResult MoveCamera(string cameraName, string movementType)
        {
            try
            {
                var camera = this.myHome.DevicesSystem.Cameras.FirstOrDefault(c => c.Name == cameraName);
                if (camera == null)
                    return this.NotFound("Camera not found");

                if (Enum.TryParse(movementType.ToUpper(), out Camera.Movement movement))
                {
                    camera.Move(movement);
                    return this.Ok();
                }
                return this.BadRequest("Invalid movement type: " + movementType);
            }
            catch (Exception e)
            {
                logger.Error(e, $"Failed to move '{cameraName}' camera to '${movementType}'");
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
            if (this.myHome.Upgrade(false))
            {
                System.Threading.Tasks.Task.Delay(100).ContinueWith(_ => Environment.Exit(0));
                return this.Ok();
            }
            return this.Conflict();
        }

        [HttpPost("restart")]
        public void Restart()
        {
            Environment.Exit(0);
        }
    }
}
