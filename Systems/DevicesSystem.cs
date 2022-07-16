using System;
using System.Collections.Generic;
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
            this.Sensors.RunForEach(sensor =>
            {
                sensor.GenerateTimeseries();

                // check for inactive sensor
                var lastSensorTime = sensor.Data.Keys.OrderBy(t => t).LastOrDefault();
                if (lastSensorTime <= this.nextSensorCheckTime.AddMinutes(-this.SensorsCheckInterval * 4) &&
                    lastSensorTime > this.nextSensorCheckTime.AddMinutes(-this.SensorsCheckInterval * 5))
                {
                    logger.Warn($"Sensor {sensor.Name} ({sensor.Room.Name}) is not active from {lastSensorTime}");
                    alertMsg += $"{sensor.Name} ({sensor.Room.Name}) inactive ";
                }

                // aggregate one value per SensorsCheckInterval
                var now = DateTime.Now;
                var times = sensor.Data.Keys.Where(t => t < now.AddHours(-1) && t >= now.Date.AddHours(now.Hour).AddDays(-1)); // last 24 hours
                var groupedDates = times.GroupBy(t => new DateTime(t.Year, t.Month, t.Day, t.Hour, t.Minute / this.SensorsCheckInterval * this.SensorsCheckInterval, 0));
                sensor.AggregateData(groupedDates);
            });

            this.Drivers.RunForEach(driver =>
            {
                // check for inactive driver
                if (driver.LastOnline <= this.nextSensorCheckTime.AddMinutes(-this.SensorsCheckInterval * 4) &&
                    driver.LastOnline > this.nextSensorCheckTime.AddMinutes(-this.SensorsCheckInterval * 5))
                {
                    logger.Warn($"Driver {driver.Name} ({driver.Room.Name}) is not online from {driver.LastOnline}");
                    alertMsg += $"{driver.Name} ({driver.Room.Name}) inactive ";
                }
            });

            if (!string.IsNullOrEmpty(alertMsg))
                Alert.Create($"{alertMsg.Trim()} alarm activated!").Validity(TimeSpan.FromHours(1)).Send();

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
