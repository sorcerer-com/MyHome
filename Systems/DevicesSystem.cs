using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using MyHome.Models;
using MyHome.Systems.Devices;
using MyHome.Systems.Devices.Drivers;
using MyHome.Systems.Devices.Sensors;
using MyHome.Utils;

using Newtonsoft.Json;

using NLog;

namespace MyHome.Systems
{
    public class DevicesSystem : BaseSystem
    {
        private static readonly ILogger logger = LogManager.GetCurrentClassLogger();

        [UiProperty(true, "minutes")]
        public int SensorsCheckInterval { get; set; } // minutes

        public List<Device> Devices { get; }

        [JsonIgnore]
        public IEnumerable<BaseSensor> Sensors => this.Devices.OfType<BaseSensor>();

        [JsonIgnore]
        public IEnumerable<Camera> Cameras => this.Devices.OfType<Camera>();

        [JsonIgnore]
        public IEnumerable<BaseDriver> Drivers => this.Devices.OfType<BaseDriver>();


        private DateTime nextSensorCheckTime;


        public DevicesSystem()
        {
            this.SensorsCheckInterval = 15;
            this.Devices = new List<Device>();

            Directory.CreateDirectory(MyHome.Instance.Config.CameraRecordsPath);
        }


        public override void Setup()
        {
            base.Setup();

            // set after loading the GetSensorDataInterval
            this.nextSensorCheckTime = this.GetNextSensorCheckTime();

            foreach (var device in this.Devices)
                device.Setup();
        }

        public override void Stop()
        {
            base.Stop();

            foreach (var device in this.Devices)
                device.Stop();
        }

        protected override void Update()
        {
            base.Update();

            this.Devices.RunForEach(device => device.Update());

            if (DateTime.Now < this.nextSensorCheckTime)
                return;

            // if SensorsCheckInterval is changed
            if (this.nextSensorCheckTime.Minute % this.SensorsCheckInterval != 0)
                this.nextSensorCheckTime = this.GetNextSensorCheckTime();

            var alertMsg = "";
            this.Devices.RunForEach(device =>
            {
                // check for inactive device
                if (device.LastOnline <= this.nextSensorCheckTime.AddMinutes(-this.SensorsCheckInterval * 4) &&
                    device.LastOnline > this.nextSensorCheckTime.AddMinutes(-this.SensorsCheckInterval * 5))
                {
                    var type = device is BaseSensor ? "Sensor" : "Driver";
                    logger.Warn($"{type} {device.Name} ({device.Room.Name}) is not active from {device.LastOnline}");
                    alertMsg += $"{device.Name} ({device.Room.Name}) inactive ";
                }

                // TODO: maybe move to sensor update
                if (device is BaseSensor sensor)
                {
                    sensor.GenerateTimeseries();

                    // aggregate one value per SensorsCheckInterval
                    var now = DateTime.Now;
                    var times = sensor.Data.Keys.Where(t => t < now.AddHours(-1) && t >= now.Date.AddHours(now.Hour).AddDays(-1)); // last 24 hours
                    var groupedDates = times.GroupBy(t => new DateTime(t.Year, t.Month, t.Day, t.Hour, t.Minute / this.SensorsCheckInterval * this.SensorsCheckInterval, 0));
                    sensor.AggregateData(groupedDates);
                }
            });

            if (!string.IsNullOrEmpty(alertMsg))
                Alert.Create($"{alertMsg.Trim()} alarm activated!").Send();

            this.nextSensorCheckTime += TimeSpan.FromMinutes(this.SensorsCheckInterval);
            MyHome.Instance.SystemChanged = true;
        }


        private DateTime GetNextSensorCheckTime()
        {
            var now = DateTime.Now;
            var time = new DateTime(now.Year, now.Month, now.Day, now.Hour, 0, 0, 0);
            while (time < now)
                time += TimeSpan.FromMinutes(this.SensorsCheckInterval);
            return time;
        }
    }
}
