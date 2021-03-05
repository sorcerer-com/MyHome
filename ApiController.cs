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
