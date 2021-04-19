using System;
using System.Collections.Generic;
using System.Linq;

using MyHome.Systems.Devices;
using MyHome.Utils;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using NLog;

namespace MyHome.Systems
{
    public class DevicesSystem : BaseSystem
    {
        private static readonly ILogger logger = LogManager.GetCurrentClassLogger();

        [UiProperty(true)]
        public int ReadSensorDataInterval { get; set; } // minutes

        public List<Device> Devices { get; }

        [JsonIgnore]
        public IEnumerable<BaseSensor> Sensors => this.Devices.OfType<BaseSensor>();

        [JsonIgnore]
        public IEnumerable<Camera> Cameras => this.Devices.OfType<Camera>();


        private DateTime nextGetDataTime;


        private DevicesSystem() : this(null) { }  // for json deserialization

        public DevicesSystem(MyHome owner) : base(owner)
        {
            this.ReadSensorDataInterval = 15;
            this.Devices = new List<Device>();
        }


        public override void Setup()
        {
            base.Setup();

            // set after loading the GetSensorDataInterval
            this.nextGetDataTime = this.GetNextReadDataTime();
        }

        public override void Update()
        {
            base.Update();

            this.Devices.RunForEach(device => device.Update());

            if (DateTime.Now < this.nextGetDataTime.AddSeconds(59)) // to be in the end of the minute
                return;

            // if GetSensorDataInterval is changed
            if (this.nextGetDataTime.Minute % this.ReadSensorDataInterval != 0)
                this.nextGetDataTime = this.GetNextReadDataTime();

            var alertMsg = "";
            this.Sensors.Where(s => !string.IsNullOrEmpty(s.Address)).RunForEach(sensor =>
            {
                logger.Debug($"Requesting data from {sensor.Name} ({sensor.Room.Name}, {sensor.Address}) sensor");

                if (sensor.ReadData(this.nextGetDataTime))
                {
                    // aggregate one value per ReadSensorDataInterval
                    var now = DateTime.Now;
                    var times = sensor.Data.Keys.Where(t => t < now.AddHours(-1) && t >= now.Date.AddHours(now.Hour).AddDays(-1)); // last 24 hours
                    var groupedDates = times.GroupBy(t => new DateTime(t.Year, t.Month, t.Day, t.Hour, t.Minute / this.ReadSensorDataInterval * this.ReadSensorDataInterval, 0));
                    sensor.AggregateData(groupedDates);

                    this.Owner.SystemChanged = true;
                }
                else
                {
                    logger.Warn($"No data from {sensor.Name} ({sensor.Room.Name}) sensor");
                    var lastSensorTime = sensor.Data.Keys.OrderBy(t => t).LastOrDefault();
                    if (lastSensorTime <= this.nextGetDataTime.AddMinutes(-this.ReadSensorDataInterval * 4) &&
                        lastSensorTime > this.nextGetDataTime.AddMinutes(-this.ReadSensorDataInterval * 5))
                    {
                        alertMsg += $"{sensor.Name} ({sensor.Room.Name}) inactive ";
                    }
                }
            });

            if (!string.IsNullOrEmpty(alertMsg))
                this.Owner.SendAlert($"{alertMsg.Trim()} Alarm Activated!");

            this.nextGetDataTime += TimeSpan.FromMinutes(this.ReadSensorDataInterval);
        }

        public bool ProcessSensorData(string token, JArray data)
        {
            var sensor = this.Sensors.FirstOrDefault(s => s.Token == token);
            if (sensor == null)
            {
                logger.Warn($"Try to process sensor data ({data}) with invalid token: {token}");
                return false;
            }

            logger.Debug($"Process sensor '{sensor.Name}' ({sensor.Room.Name}, {token}) data: {data}");

            sensor.AddData(DateTime.Now, data);
            this.Owner.SystemChanged = true;
            return true;
        }


        private DateTime GetNextReadDataTime()
        {
            var now = DateTime.Now;
            var time = new DateTime(now.Year, now.Month, now.Day, now.Hour, 0, 0, 0);
            while (time < now)
                time += TimeSpan.FromMinutes(this.ReadSensorDataInterval);
            return time;
        }
    }
}
