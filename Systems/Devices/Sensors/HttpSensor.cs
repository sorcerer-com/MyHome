using System;
using System.Collections.Generic;
using System.Linq;

using MyHome.Utils;

using Newtonsoft.Json.Linq;

using NLog;

namespace MyHome.Systems.Devices.Sensors
{
    public class HttpSensor : BaseSensor
    {
        private static readonly ILogger logger = LogManager.GetCurrentClassLogger();

        [UiProperty(true)]
        public string Address { get; set; }

        [UiProperty(true, "minutes")]
        public int ReadDataInterval { get; set; } // minutes


        private DateTime nextDataRead;


        public HttpSensor()
        {
            this.Address = "";
            this.ReadDataInterval = 5;

            this.nextDataRead = DateTime.Now.AddMinutes(1); // start reading 1 minute after start
        }


        public override void Update()
        {
            base.Update();

            // read sensor data
            if (DateTime.Now > this.nextDataRead)
            {
                logger.Trace($"Requesting data from {this.Name} ({this.Room.Name}) sensor");
                var data = this.ReadData();
                if (data != null)
                    this.AddData(DateTime.Now, data);
                else
                    logger.Warn($"No data from {this.Name} ({this.Room.Name}) sensor");
                this.nextDataRead = DateTime.Now.AddMinutes(this.ReadDataInterval);
            }
        }


        private Dictionary<string, object> ReadData()
        {
            if (!string.IsNullOrEmpty(this.Address))
            {
                var content = Services.GetContent(this.Address);
                var json = content != null ? JToken.Parse(content) : content;
                return json?.OfType<JObject>()
                    .Where(item => item.ContainsKey("name") && item.ContainsKey("value") &&
                        this.SubNamesMap.ContainsKey((string)item["name"]))
                    .ToDictionary(item => (string)item["name"], item => (object)(double)item["value"]);
            }
            else
                return new Dictionary<string, object>();
        }
    }
}
