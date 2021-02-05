using System;
using System.Collections.Generic;
using System.Linq;

using MyHome.Systems.Devices;

using Newtonsoft.Json;

using NLog;

namespace MyHome.Systems
{
    public class DevicesSystem : BaseSystem
    {
        private static readonly ILogger logger = LogManager.GetCurrentClassLogger();

        public int GetSensorDataInterval { get; set; } // minutes

        public List<Device> Devices { get; }

        [JsonIgnore]
        public IEnumerable<BaseSensor> Sensors => this.Devices.OfType<BaseSensor>();


        private DateTime nextGetDataTime;


        public DevicesSystem(MyHome owner) : base(owner)
        {
            this.GetSensorDataInterval = 1;// TODO: 15;
            this.Devices = new List<Device>();
        }


        public override void Setup()
        {
            base.Setup();

            // set after loading the GetSensorDataInterval
            this.nextGetDataTime = this.GetNextGetDataTime();
        }

        public override void Update()
        {
            base.Update();

            foreach (var device in this.Devices)
                device.Update();

            if (DateTime.Now < this.nextGetDataTime) // TODO: seconds = 59 for end of the minute?
                return;

            // if GetSensorDataInterval is changed
            if (this.nextGetDataTime.Minute % this.GetSensorDataInterval != 0)
                this.nextGetDataTime = this.GetNextGetDataTime();

            var alertMsg = "";
            foreach (var sensor in this.Sensors)
            {
                logger.Debug($"Requesting data from {sensor.Name}({sensor.Room}, {sensor.Address}) sensor");

                if (sensor.ReadData(this.nextGetDataTime))
                {
                    this.Owner.SystemChanged = true;
                }
                else
                {
                    logger.Warn($"No data from {sensor.Name} sensor");
                    if (sensor.LastTime.HasValue &&
                        sensor.LastTime.Value <= this.nextGetDataTime.AddMinutes(this.GetSensorDataInterval * 4) &&
                        sensor.LastTime.Value <= this.nextGetDataTime.AddMinutes(this.GetSensorDataInterval * 5))
                    {
                        alertMsg += $"{sensor.Name}(inactive) ";
                    }
                }
            }

            if (!string.IsNullOrEmpty(alertMsg))
                this.Owner.SendAlert($"{alertMsg.Trim()} Alarm Activated!");

            this.nextGetDataTime += TimeSpan.FromMinutes(this.GetSensorDataInterval);
        }


        private DateTime GetNextGetDataTime()
        {
            var now = DateTime.Now;
            var time = new DateTime(now.Year, now.Month, now.Day, now.Hour, 0, 0, 0);
            while (time < now)
                time += TimeSpan.FromMinutes(this.GetSensorDataInterval);
            return time;
        }
    }
}
