using System;
using System.Collections.Generic;
using System.Linq;

using MyHome.Models;
using MyHome.Utils;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using NLog;

namespace MyHome.Systems.Devices.Sensors
{
    public abstract class BaseSensor : Device
    {
        private static readonly ILogger logger = LogManager.GetCurrentClassLogger();

        public class SensorValue : Dictionary<string, double> { }; // subName / value


        [UiProperty(true)]
        public string Address { get; set; }

        [UiProperty(true)]
        public string Token { get; set; }

        public Dictionary<DateTime, SensorValue> Data { get; }

        [UiProperty]
        public Dictionary<string, Dictionary<string, object>> Metadata { get; }

        [UiProperty(true, "real name / custom name")]
        public Dictionary<string, string> SubNamesMap { get; } // map sensor subname to custom subname

        [UiProperty(true, "name / addition, multiplier")]
        public Dictionary<string, (double addition, double multiplier)> Calibration { get; } // calibration values per subname - newValue = (realValue + addition) * multiplier

        [UiProperty(true, "name / unit")]
        public Dictionary<string, string> Units { get; } // subname unit name (if not provide by metadata)


        [JsonProperty]
        private Dictionary<string, double> LastReadings { get; } // subname / value


        [JsonIgnore]
        [UiProperty]
        public Dictionary<string, double> LastValues => this.GetLastValues();


        private BaseSensor() : this(null, null, null, null) { } // for json deserialization

        protected BaseSensor(DevicesSystem owner, string name, Room room, string address) : base(owner, name, room)
        {
            this.Address = address;
            this.Token = Utils.Utils.GenerateRandomToken(16);
            this.Data = new Dictionary<DateTime, SensorValue>();
            this.Metadata = new Dictionary<string, Dictionary<string, object>>();
            this.SubNamesMap = new Dictionary<string, string>();
            this.Calibration = new Dictionary<string, (double addition, double multiplier)>();
            this.Units = new Dictionary<string, string>();

            this.LastReadings = new Dictionary<string, double>();
        }


        public bool ReadData(DateTime time)
        {
            var data = this.ReadDataInternal(); // read data from sensor
            logger.Trace($"Sensor '{this.Name}' ({this.Room.Name}) read data: '{data}'");
            if (data != null)
            {
                this.AddData(time, data);
                return true;
            }
            return false;
        }

        protected abstract JToken ReadDataInternal();

        public void AddData(DateTime time, JToken data)
        {
            logger.Trace($"Sensor '{this.Name}' ({this.Room.Name}) add data at {time:dd/MM/yyyy HH:mm:ss}: {data}");
            if (!data.HasValues)
                return;

            if (!this.Data.ContainsKey(time))
                this.Data.Add(time, new SensorValue());

            var addedData = new Dictionary<string, double>();
            foreach (var item in data.OfType<JObject>())
            {
                if (!item.ContainsKey("name") && !item.ContainsKey("value"))
                {
                    logger.Warn($"Try to add invalid data item({item}) in sensor '{this.Name}' ({this.Room.Name})");
                    continue;
                }

                var name = (string)item["name"];
                if (this.SubNamesMap.ContainsKey(name))
                    name = this.SubNamesMap[name];

                var value = (item["value"].Type == JTokenType.Boolean) ? ((bool)item["value"] ? 1 : 0) : (double)item["value"];
                if (this.Calibration.ContainsKey(name)) // mapped name
                    value = (value + this.Calibration[name].addition) * this.Calibration[name].multiplier;

                var aggrType = item.ContainsKey("aggrType") ? (string)item["aggrType"] : "avg";
                if (aggrType == "avg")
                {
                    this.Data[time][name] = value;
                }
                else // sum type - differentiate
                {
                    this.LastReadings.TryGetValue(name, out var prevValue);
                    if (prevValue - 1 > value) // if the new value is smaller (with epsilon - 1) than previous - it's reset
                        prevValue = 0;
                    this.Data[time][name] = Math.Round(value - prevValue, 2); // round to 2 decimals after the point
                    this.LastReadings[name] = value;
                }

                addedData[name] = this.Data[time][name];
                item.Remove("name");
                item.Remove("value");
                if (this.Units.ContainsKey(name))
                    item.Add("unit", this.Units[name]);
                this.Metadata[name] = item.ToObject<Dictionary<string, object>>();
            }
            this.Owner.Owner.Events.Fire(this, GlobalEventTypes.SensorDataAdded, addedData);
            this.ArchiveData();
        }

        public void AggregateData(IEnumerable<IGrouping<DateTime, DateTime>> groupedDates)
        {
            foreach (var group in groupedDates) // new time / list of times to be grouped
            {
                if (group.Count() == 1) // already grouped
                    continue;

                var items = group.Select(t => this.Data[t]).ToList();
                // delete old records
                foreach (var t in group)
                    this.Data.Remove(t);
                // add one new
                this.Data[group.Key] = new SensorValue();
                var subNames = items.Select(i => i.Keys).SelectMany(x => x).Distinct();
                foreach (var subName in subNames)
                {
                    var values = items.Where(i => i.ContainsKey(subName)).Select(i => i[subName]);
                    if (!values.Any())
                        continue;

                    var aggrType = "avg";
                    if (this.Metadata.ContainsKey(subName) && this.Metadata[subName].ContainsKey("aggrType"))
                        aggrType = (string)this.Metadata[subName]["aggrType"];

                    double newValue = 0.0;
                    if (aggrType == "avg")
                        newValue = Math.Round(values.Average(), 2); // round to 2 decimals after the point
                    else if (aggrType == "sum")
                        newValue = Math.Round(values.Sum(), 2);
                    this.Data[group.Key][subName] = newValue;
                }
            }
        }

        private void ArchiveData()
        {
            var now = DateTime.Now;
            // delete entries older then 1 year
            var times = this.Data.Keys.Where(t => t < now.AddYears(-1));
            foreach (var time in times)
                this.Data.Remove(time);

            // for older then 24 hour, save only 1 per day
            times = this.Data.Keys.Where(t => t < now.Date.AddDays(-1));
            var groupedDates = times.GroupBy(t => t.Date);
            this.AggregateData(groupedDates);
        }

        private Dictionary<string, double> GetLastValues()
        {
            return this.Data.OrderBy(kvp => kvp.Key)
                .SelectMany(x => x.Value)
                .GroupBy(x => x.Key)
                .ToDictionary(g => g.Key, g => g.Last().Value);
        }
    }
}
