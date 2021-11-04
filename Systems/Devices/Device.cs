using MyHome.Models;
using MyHome.Utils;

using NLog;

namespace MyHome.Systems.Devices
{
    public abstract class Device
    {
        private static readonly ILogger logger = LogManager.GetCurrentClassLogger();

        public DevicesSystem Owner { get; set; }

        [UiProperty(true)]
        public string Name { get; set; } // unique per room

        public Room Room { get; set; }


        private Device() : this(null, null, null) { } // for json deserialization

        protected Device(DevicesSystem owner, string name, Room room)
        {
            this.Owner = owner;
            this.Name = name;
            this.Room = room;
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
