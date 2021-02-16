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
            this.ReadSensorDataInterval = 1;// TODO: 15;
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

            foreach (var device in this.Devices)
                device.Update();

            if (DateTime.Now < this.nextGetDataTime) // TODO: seconds = 59 for end of the minute?
                return;

            // if GetSensorDataInterval is changed
            if (this.nextGetDataTime.Minute % this.ReadSensorDataInterval != 0)
                this.nextGetDataTime = this.GetNextReadDataTime();

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
                    logger.Warn($"No data from {sensor.Name} ({sensor.Room.Name}) sensor");
                    if (sensor.LastTime.HasValue &&
                        sensor.LastTime.Value <= this.nextGetDataTime.AddMinutes(this.ReadSensorDataInterval * 4) &&
                        sensor.LastTime.Value <= this.nextGetDataTime.AddMinutes(this.ReadSensorDataInterval * 5))
                    {
                        alertMsg += $"{sensor.Name}({sensor.Room.Name}) inactive ";
                    }
                }
            }

            if (!string.IsNullOrEmpty(alertMsg))
                this.Owner.SendAlert($"{alertMsg.Trim()} Alarm Activated!");

            this.nextGetDataTime += TimeSpan.FromMinutes(this.ReadSensorDataInterval);
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
