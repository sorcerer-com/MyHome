using System;

using MyHome.Models;
using MyHome.Utils;

using Newtonsoft.Json;

using NLog;

namespace MyHome.Systems.Devices
{
    public abstract class Device
    {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();


        [UiProperty(true)]
        public string Name { get; set; } // unique per room

        [JsonIgnore]
        [UiProperty(true)]
        public int Index
        {
            get => MyHome.Instance.DevicesSystem.Devices.IndexOf(this);
            set
            {
                var devices = MyHome.Instance.DevicesSystem.Devices;
                if (value >= 0 && devices.IndexOf(this) != value)
                {
                    devices.Remove(this);
                    devices.Insert(Math.Min(value, devices.Count), this);
                }
            }
        }

        [UiProperty]
        public Position Location { get; set; } = new Position(); // location in UI map

        [JsonIgnore]
        [UiProperty]
        public virtual DateTime LastOnline => DateTime.Now;

        public Room Room { get; set; }


        protected Device()
        {
        }


        public virtual void Setup()
        {
            logger.Debug($"Setup device: {this.Name}");
        }

        public virtual void Stop()
        {
            logger.Debug($"Stop device: {this.Name}");
        }

        public virtual void Update()
        {
        }
    }
}
