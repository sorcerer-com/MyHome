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
                if (value >= 0 && MyHome.Instance.DevicesSystem.Devices.IndexOf(this) != value)
                {
                    MyHome.Instance.DevicesSystem.Devices.Remove(this);
                    MyHome.Instance.DevicesSystem.Devices.Insert(value, this);
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
