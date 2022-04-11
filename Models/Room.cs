using System;
using System.Collections.Generic;
using System.Linq;

using MyHome.Systems.Devices;
using MyHome.Systems.Devices.Sensors;
using MyHome.Utils;

using Newtonsoft.Json;

using NLog;

namespace MyHome.Models
{
    public class Room
    {
        private static readonly ILogger logger = LogManager.GetCurrentClassLogger();

        [UiProperty(true)]
        public string Name { get; set; }


        [JsonIgnore]
        [UiProperty(true)]
        public IEnumerable<Device> Devices => MyHome.Instance.DevicesSystem.Devices.Where(d => d.Room == this);

        [JsonIgnore]
        public IEnumerable<BaseSensor> Sensors => this.Devices.OfType<BaseSensor>();

        [JsonIgnore]
        public IEnumerable<Camera> Cameras => this.Devices.OfType<Camera>();


        [JsonIgnore]
        [UiProperty]
        public bool IsSecuritySystemEnabled
        {
            get => MyHome.Instance.SecuritySystem.ActivatedRooms.ContainsKey(this);
            set => MyHome.Instance.SecuritySystem.SetEnable(this, value);
        }

        [JsonIgnore]
        [UiProperty]
        public bool IsSecuritySystemActivated
        {
            get => MyHome.Instance.SecuritySystem.ActivatedRooms.GetValueOrDefault(this);
            set
            {
                if (value)
                    MyHome.Instance.SecuritySystem.Activate(this);
                else
                    logger.Warn($"Try to deactivate security system on room: {this.Name}");
            }
        }

        [JsonIgnore]
        [UiProperty]
        public Dictionary<DateTime, string> SecurityHistory =>
            MyHome.Instance.SecuritySystem.History.ContainsKey(this.Name)
                    ? MyHome.Instance.SecuritySystem.History[this.Name]
                    : new Dictionary<DateTime, string>();

        [JsonIgnore]
        [UiProperty]
        public Dictionary<string, double> SensorsValues => this.GetSensorsValues();

        [JsonIgnore]
        [UiProperty]
        public Dictionary<string, Dictionary<string, string>> SensorsMetadata => this.GetSensorsMetadata();


        private Room() : this(null) { } // for json deserialization

        public Room(string name)
        {
            this.Name = name;
        }


        public Alert Alert(string message)
        {
            return Models.Alert.Create($"'{this.Name}' room alert: {message}");
        }

        private Dictionary<string, double> GetSensorsValues()
        {
            return this.Sensors.Select(s => s.Values)
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
