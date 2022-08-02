using System;
using System.Collections.Generic;

using MyHome.Utils;

using Newtonsoft.Json;

using NLog;

namespace MyHome.Systems.Devices.Drivers
{
    public abstract class BaseDriver : Device
    {
        private static readonly ILogger logger = LogManager.GetCurrentClassLogger();

        [JsonIgnore]
        [UiProperty]
        public virtual DateTime LastOnline => DateTime.Now;


        protected Dictionary<string, object> States { get; }


        protected BaseDriver()
        {
            this.States = new Dictionary<string, object>();
        }


        protected bool SetState(string name, object value, Action call = null)
        {
            if (this.States[name] == value || this.States[name]?.Equals(value) == true)
                return false;

            if (this.Room != null)
                logger.Info($"Set {name} state of {this.Name} ({this.Room.Name}): {value}");
            this.States[name] = value;
            MyHome.Instance.Events.Fire(this, GlobalEventTypes.DriverStateChanged, this.States);
            call?.Invoke();
            return true;
        }
    }
}
