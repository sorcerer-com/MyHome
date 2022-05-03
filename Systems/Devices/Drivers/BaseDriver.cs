using System;
using System.Collections.Generic;

namespace MyHome.Systems.Devices.Drivers
{
    public abstract class BaseDriver : Device
    {
        protected Dictionary<string, object> State { get; }


        protected BaseDriver()
        {
            this.State = new Dictionary<string, object>();
        }


        protected void SetState(string name, object value, Action call)
        {
            if (this.State[name] == value)
                return;

            this.State[name] = value;
            call?.Invoke();
        }
    }
}
