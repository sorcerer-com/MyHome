﻿using System;
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
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();

        [UiProperty(true)]
        public string Name { get; set; }

        [JsonIgnore]
        [UiProperty(true)]
        public int Index
        {
            get => MyHome.Instance.Rooms.IndexOf(this);
            set
            {
                if (value >= 0 && MyHome.Instance.Rooms.IndexOf(this) != value)
                {
                    MyHome.Instance.Rooms.Remove(this);
                    MyHome.Instance.Rooms.Insert(value, this);
                }
            }
        }


        [JsonIgnore]
        [UiProperty]
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
            MyHome.Instance.SecuritySystem.History.TryGetValue(this.Name, out var value) ? value : [];

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


        public Notification AddNotification(string message)
        {
            return MyHome.Instance.AddNotification($"'{this.Name}' room: {message}");
        }

        // allow enable/disable Security System with specific level
        public void SetSecuritySystemEnable(bool enable, int level)
        {
            MyHome.Instance.SecuritySystem.SetEnable(this, enable, level);
        }

        public Device GetDevice(string type)
        {
            return this.Devices.FirstOrDefault(
                d => d.GetType().Name == type ||
                d.GetType().BaseType.Name == type ||
                d.GetType().GetInterface(type) != null);
        }


        private Dictionary<string, double> GetSensorsValues()
        {
            return this.Sensors.Where(s => !s.Grouped)
                .Select(s => s.Values)
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
