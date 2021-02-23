using System;
using System.Collections.Generic;
using System.Linq;

using MyHome.Systems.Devices;
using MyHome.Utils;

using Newtonsoft.Json;

using NLog;

namespace MyHome.Systems
{
    public class DevicesSystem : BaseSystem
    {
        private static readonly ILogger logger = LogManager.GetCurrentClassLogger();

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
            this.Sensors.RunForEach(sensor =>
            {
                logger.Debug($"Requesting data from {sensor.Name}({sensor.Room.Name}, {sensor.Address}) sensor");

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
                    if (sensor.LastTime.HasValue &&
                        sensor.LastTime.Value <= this.nextGetDataTime.AddMinutes(-this.ReadSensorDataInterval * 4) &&
                        sensor.LastTime.Value > this.nextGetDataTime.AddMinutes(-this.ReadSensorDataInterval * 5))
                    {
                        alertMsg += $"{sensor.Name}({sensor.Room.Name}) inactive ";
                    }
                }
            });

            if (!string.IsNullOrEmpty(alertMsg))
                this.Owner.SendAlert($"{alertMsg.Trim()} Alarm Activated!");

            this.nextGetDataTime += TimeSpan.FromMinutes(this.ReadSensorDataInterval);
        }

        // TODO: processData


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
