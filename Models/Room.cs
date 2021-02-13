using System.Collections.Generic;
using System.Linq;

using MyHome.Systems.Devices;

using Newtonsoft.Json;

namespace MyHome.Models
{
    public class Room
    {
        public MyHome Owner { get; set; }

        public string Name { get; set; }


        [JsonIgnore]
        public IEnumerable<Device> Devices => this.Owner.DevicesSystem.Devices.Where(d => d.Room == this);

        [JsonIgnore]
        public IEnumerable<BaseSensor> Sensors => this.Devices.OfType<BaseSensor>();

        [JsonIgnore]
        public IEnumerable<Camera> Cameras => this.Devices.OfType<Camera>();

        // TODO: add other references too: maybe subtype devices (motion, multi, light, switch), security status, trigger/actions, sounds


        public Room(MyHome owner, string name)
        {
            this.Owner = owner;
            this.Name = name;
        }
    }
}
