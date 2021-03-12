using System;
using System.IO;
using System.Linq;

using Microsoft.AspNetCore.Mvc;

using MyHome.Utils;

using Newtonsoft.Json.Linq;

using NLog;

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
                logger.Error(e, "Failed to set room");
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
                logger.Error(e, "Failed to call room's function");
                return this.BadRequest(e.Message);
            }
        }


        [HttpPost("sensor/data")]
        public ActionResult ProcessSensorData()
        {
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
    }
}
