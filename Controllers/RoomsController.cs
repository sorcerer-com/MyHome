using System;
using System.Linq;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

using MyHome.Systems.Devices;
using MyHome.Systems.Devices.Sensors;
using MyHome.Utils;

using NLog;

namespace MyHome.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class RoomsController : ControllerBase
    {
        private static readonly ILogger logger = LogManager.GetCurrentClassLogger();

        private readonly MyHome myHome;


        public RoomsController(MyHome myHome)
        {
            this.myHome = myHome;
        }


        [HttpGet]
        public ActionResult GetRooms()
        {
            return this.Ok(this.myHome.Rooms.Select(r => r.ToUiObject()));
        }

        [HttpPost("create")]
        public ActionResult CreateRoom()
        {
            return this.Ok(new Models.Room("New Room").ToUiObject());
        }

        [HttpPost("{roomName}")]
        public ActionResult SetRoom(string roomName)
        {
            try
            {
                var body = Newtonsoft.Json.Linq.JToken.Parse(new System.IO.StreamReader(this.Request.Body).ReadToEndAsync().Result);

                var room = this.myHome.Rooms.FirstOrDefault(r => r.Name == roomName);
                if (room == null)
                {
                    logger.Info($"Add room '{roomName}'");
                    room = new Models.Room(roomName);
                    this.myHome.Rooms.Add(room);
                }

                body.SetObject(room);
                this.myHome.SystemChanged = true;
                return this.Ok();
            }
            catch (Exception e)
            {
                logger.Error($"Failed to set room '{roomName}'");
                logger.Debug(e);
                return this.BadRequest(e.Message);
            }
        }

        [HttpPost("{roomName}/delete")]
        public ActionResult DeleteRoom(string roomName)
        {
            try
            {
                var room = this.myHome.Rooms.FirstOrDefault(r => r.Name == roomName);
                if (room == null)
                    return this.NotFound($"Room '{roomName}' not found");

                foreach (var device in room.Devices.ToList()) // copy the list of devices
                    this.myHome.DevicesSystem.Devices.Remove(device);
                this.myHome.Rooms.Remove(room);
                this.myHome.SystemChanged = true;
                return this.Ok();
            }
            catch (Exception e)
            {
                logger.Error($"Failed to delete room '{roomName}'");
                logger.Debug(e);
                return this.BadRequest(e.Message);
            }
        }



        [HttpPost("{roomName}/devices/create/{deviceType}")]
        public ActionResult CreateDevice(string roomName, string deviceType)
        {
            var room = this.myHome.Rooms.FirstOrDefault(r => r.Name == roomName);
            if (room == null)
                return this.NotFound($"Room '{roomName}' not found");

            var type = typeof(Device).GetSubClasses().FirstOrDefault(t => t.Name == deviceType);
            if (type == null)
                return this.NotFound("No such device type: " + deviceType);

            var device = (Device)Activator.CreateInstance(type, true);
            device.Name = "New " + deviceType;
            device.Room = room;
            return this.Ok(device.ToUiObject());
        }

        [HttpPost("{roomName}/devices/{deviceName}")]
        public ActionResult SetDevice(string roomName, string deviceName)
        {
            try
            {
                var body = Newtonsoft.Json.Linq.JToken.Parse(new System.IO.StreamReader(this.Request.Body).ReadToEndAsync().Result);

                var room = this.myHome.Rooms.FirstOrDefault(r => r.Name == roomName);
                if (room == null)
                    return this.NotFound($"Room '{roomName}' not found");

                var device = room.Devices.FirstOrDefault(d => d.Name == deviceName);
                if (device == null)
                {
                    logger.Info($"Add device '{deviceName}' to room '{roomName}'");
                    var type = System.Reflection.Assembly.GetExecutingAssembly().GetType((string)body["$type"]);
                    if (type == null)
                        return this.NotFound("No such device type: " + type);

                    device = (Device)Activator.CreateInstance(type, true);
                    device.Room = room;
                    device.Setup();
                    this.myHome.DevicesSystem.Devices.Add(device);
                }

                body.SetObject(device);
                this.myHome.SystemChanged = true;
                return this.Ok();
            }
            catch (Exception e)
            {
                logger.Error($"Failed to set device '{deviceName}' ({roomName})");
                logger.Debug(e);
                return this.BadRequest(e.InnerException?.Message ?? e.Message);
            }
        }

        [HttpPost("{roomName}/devices/{deviceName}/delete")]
        public ActionResult DeleteDevice(string roomName, string deviceName)
        {
            try
            {
                var room = this.myHome.Rooms.FirstOrDefault(r => r.Name == roomName);
                if (room == null)
                    return this.NotFound($"Room '{roomName}' not found");

                var device = room.Devices.FirstOrDefault(d => d.Name == deviceName);
                if (device == null)
                    return this.NotFound($"Device '{deviceName}' not found");

                device.Stop();
                this.myHome.DevicesSystem.Devices.Remove(device);
                this.myHome.SystemChanged = true;
                return this.Ok();
            }
            catch (Exception e)
            {
                logger.Error($"Failed to delete device '{deviceName}' ({roomName})");
                logger.Debug(e);
                return this.BadRequest(e.Message);
            }
        }


        [HttpGet("{roomName}/sensors/{sensorName}/data/{valueType}")]
        public ActionResult GetSensorData(string roomName, string sensorName, string valueType)
        {
            var room = this.myHome.Rooms.FirstOrDefault(r => r.Name == roomName);
            if (room == null)
                return this.NotFound($"Room '{roomName}' not found");

            var sensor = room.Sensors.FirstOrDefault(s => s.Name == sensorName);
            if (sensor == null)
                return this.NotFound($"Sensor '{sensorName}' not found");

            var result = sensor.Data.Where(kvp => kvp.Value.ContainsKey(valueType)).ToDictionary(kvp => kvp.Key.ToString("o"), kvp => kvp.Value[valueType]);
            return this.Ok(result);
        }

        [HttpPost("{roomName}/sensors/{sensorName}/data/{valueType}")]
        public ActionResult SetSensorData(string roomName, string sensorName, string valueType)
        {
            var room = this.myHome.Rooms.FirstOrDefault(r => r.Name == roomName);
            if (room == null)
                return this.NotFound($"Room '{roomName}' not found");

            var sensor = room.Sensors.FirstOrDefault(s => s.Name == sensorName);
            if (sensor == null)
                return this.NotFound($"Sensor '{sensorName}' not found");

            var body = Newtonsoft.Json.Linq.JToken.Parse(new System.IO.StreamReader(this.Request.Body).ReadToEndAsync().Result);
            foreach(var item in body.OfType<Newtonsoft.Json.Linq.JProperty>())
            {
                var key = DateTime.Parse(item.Name);
                if (sensor.Data.ContainsKey(key) && sensor.Data[key].ContainsKey(valueType))
                    sensor.Data[key][valueType] = (double)item;
            }
            this.myHome.SystemChanged = true;

            return this.Ok();
        }


        [HttpGet("{roomName}/cameras/{cameraName}/image")]
        public void GetCameraImage(string roomName, string cameraName)
        {
            var room = this.myHome.Rooms.FirstOrDefault(r => r.Name == roomName);
            if (room == null)
            {
                this.Response.StatusCode = StatusCodes.Status404NotFound;
                this.Response.Body.WriteAsync(System.Text.Encoding.UTF8.GetBytes($"Room '{roomName}' not found")).AsTask();
                return;
            }

            var camera = room.Cameras.FirstOrDefault(c => c.Name == cameraName);
            if (camera == null)
            {
                this.Response.StatusCode = StatusCodes.Status404NotFound;
                this.Response.Body.WriteAsync(System.Text.Encoding.UTF8.GetBytes($"Camera '{cameraName}' not found")).AsTask();
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
        }

        [HttpPost("{roomName}/cameras/{cameraName}/move")]
        public ActionResult MoveCamera(string roomName, string cameraName, string movementType)
        {
            try
            {
                var room = this.myHome.Rooms.FirstOrDefault(r => r.Name == roomName);
                if (room == null)
                    return this.NotFound($"Room '{roomName}' not found");

                var camera = room.Cameras.FirstOrDefault(c => c.Name == cameraName);
                if (camera == null)
                    return this.NotFound($"Camera '{cameraName}' not found");

                if (Enum.TryParse(movementType.ToUpper(), out Camera.Movement movement))
                {
                    camera.Move(movement);
                    return this.Ok();
                }
                return this.BadRequest("Invalid movement type: " + movementType);
            }
            catch (Exception e)
            {
                logger.Error($"Failed to move '{cameraName}' camera to '{movementType}'");
                logger.Debug(e);
                return this.BadRequest(e.Message);
            }
        }

        [HttpPost("{roomName}/cameras/{cameraName}/restart")]
        public ActionResult RestartCamera(string roomName, string cameraName)
        {

            try
            {
                var room = this.myHome.Rooms.FirstOrDefault(r => r.Name == roomName);
                if (room == null)
                    return this.NotFound($"Room '{roomName}' not found");

                var camera = room.Cameras.FirstOrDefault(c => c.Name == cameraName);
                if (camera == null)
                    return this.NotFound($"Camera '{cameraName}' not found");

                if (camera.Restart())
                    return this.Ok();
                return this.BadRequest($"Cannot restart '{cameraName}' camera");
            }
            catch (Exception e)
            {
                logger.Error($"Failed to restart '{cameraName}' camera");
                logger.Debug(e);
                return this.BadRequest(e.Message);
            }
        }
    }
}
