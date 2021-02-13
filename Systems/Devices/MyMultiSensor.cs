﻿using MyHome.Models;
using MyHome.Utils;

using Newtonsoft.Json.Linq;

namespace MyHome.Systems.Devices
{
    public class MyMultiSensor : BaseSensor
    {
        public MyMultiSensor(DevicesSystem owner, string name, Room room, string address) : base(owner, name, room, address)
        {
        }

        protected override JToken ReadDataInternal()
        {
            return Services.GetJsonContent(this.Address);
        }
    }
}
