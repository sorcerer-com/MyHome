using System.Collections.Generic;
using System.Linq;

using MyHome.Systems.Devices;
using MyHome.Utils;

using Newtonsoft.Json;

namespace MyHome.Models
{
    public class Room
    {
        public MyHome Owner { get; set; }

        [UiProperty]
        public string Name { get; set; }


        [JsonIgnore]
        [UiProperty]
        public IEnumerable<Device> Devices => this.Owner.DevicesSystem.Devices.Where(d => d.Room == this);

        [JsonIgnore]
        public IEnumerable<BaseSensor> Sensors => this.Devices.OfType<BaseSensor>();

        [JsonIgnore]
        public IEnumerable<Camera> Cameras => this.Devices.OfType<Camera>();


        [JsonIgnore]
        [UiProperty]
        public bool IsSecuritySystemEnabled
        {
            get => this.Owner.SecuritySystem.ActivatedRooms.ContainsKey(this);
            set => this.Owner.SecuritySystem.SetEnable(this, value);
        }

        [JsonIgnore]
        [UiProperty]
        public bool IsSecuritySystemActivated
        {
            get => this.Owner.SecuritySystem.ActivatedRooms.GetValueOrDefault(this);
            set => this.Owner.SecuritySystem.Activate(this);
        }


        [UiProperty]
        public Dictionary<string, double> SensorsValues => this.GetSensorsValues();

        [UiProperty]
        public Dictionary<string, Dictionary<string, string>> SensorsMetadata => this.GetSensorsMetadata();


        private Room() : this(null, null) { } // for json deserialization

        public Room(MyHome owner, string name)
        {
            this.Owner = owner;
            this.Name = name;
        }


        private Dictionary<string, double> GetSensorsValues()
        {
            return this.Sensors.Select(s => s.LastValues)
                 .SelectMany(dict => dict)
                 .GroupBy(kvp => kvp.Key)
                 .ToDictionary(g => g.Key, g => g.Average(kvp => kvp.Value));
        }

        private Dictionary<string, Dictionary<string, string>> GetSensorsMetadata()
        {
            return this.Sensors.Select(s => s.Metadata)
                 .SelectMany(dict => dict)
                 .GroupBy(kvp => kvp.Key, kvp => kvp.Value)
                 .ToDictionary(g => g.Key, g => g.SelectMany(x => x)
                     .GroupBy(kvp => kvp.Key, kvp => kvp.Value)
                     .ToDictionary(gg => gg.Key, gg => gg.Aggregate((a, b) => a + "\n" + b).ToString()));
        }
    }
}
