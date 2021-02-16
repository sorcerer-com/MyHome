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


        [JsonIgnore]
        public bool IsSecuritySystemEnabled
        {
            get => this.Owner.SecuritySystem.ActivatedRooms.ContainsKey(this);
            set => this.Owner.SecuritySystem.SetEnable(this, value);
        }

        [JsonIgnore]
        public bool IsSecuritySystemActivated
        {
            get => this.Owner.SecuritySystem.ActivatedRooms.GetValueOrDefault(this);
            set => this.Owner.SecuritySystem.Activate(this);
        }

        // TODO: add other references too: maybe subtype devices (motion, multi, light, switch), security status, trigger/actions, sounds


        private Room() : this(null, null) { } // for json deserialization

        public Room(MyHome owner, string name)
        {
            this.Owner = owner;
            this.Name = name;
        }
    }
}
