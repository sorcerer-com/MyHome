using MyHome.Models;
using MyHome.Utils;

using NLog;

namespace MyHome.Systems.Devices
{
    public abstract class Device
    {
        private static readonly ILogger logger = LogManager.GetCurrentClassLogger();


        [UiProperty(true)]
        public string Name { get; set; } // unique per room

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
