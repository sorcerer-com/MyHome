using System;
using System.Collections.Generic;
using System.Linq;

using MyHome.Models;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using NLog;

namespace MyHome.Systems.Devices
{
    public abstract class BaseSensor : Device
    {
        private static readonly ILogger logger = LogManager.GetCurrentClassLogger();

        public class SensorValue : Dictionary<string, double> { }; // subName / value


        public string Address { get; set; }

        public Dictionary<DateTime, SensorValue> Data { get; }

        public Dictionary<string, Dictionary<string, object>> Metadata { get; }

        public Dictionary<string, string> SubNamesMap { get; } // map sensor subname to custom subname

        public Dictionary<string, (double addition, double multiplier)> Calibration { get; } // calibration values per subname - newValue = (realValue + addition) * mutiplier


        [JsonIgnore]
        public DateTime? LastTime => this.Data.Count > 0 ? this.Data.Max(d => d.Key) : null;

        [JsonIgnore]
        public SensorValue LastValues => this.LastTime.HasValue ? this.Data[this.LastTime.Value] : null;

        [JsonIgnore]
        public List<string> SubNames => this.LastValues.Keys.ToList();


        private BaseSensor() : this(null, null, null, null) { } // for json deserialization

        public BaseSensor(DevicesSystem owner, string name, Room room, string address) : base(owner, name, room)
        {
            this.Address = address;
            this.Data = new Dictionary<DateTime, SensorValue>();
            this.Metadata = new Dictionary<string, Dictionary<string, object>>();
            this.SubNamesMap = new Dictionary<string, string>();
            this.Calibration = new Dictionary<string, (double addition, double multiplier)>();
        }


        public bool ReadData(DateTime time)
        {
            var data = this.ReadDataInternal(); // read data from sensor
            logger.Debug($"Sensor '{this.Name}' read data: '{data}'");
            if (data != null)
            {
                this.AddData(time, data);
                return true;
            }
            return false;
        }

        public void AddData(DateTime time, JToken data)
        {
            logger.Debug($"Sensor '{this.Name}' add data at {time:dd/MM/yyyy HH:mm:ss}: {data}");

            if (!this.Data.ContainsKey(time))
                this.Data.Add(time, new SensorValue());

            var addedData = new Dictionary<string, double>();
            foreach (var item in data.OfType<JObject>())
            {
                if (!item.ContainsKey("name") && !item.ContainsKey("value"))
                {
                    logger.Warn($"Try to add invalid data item({item}) in sensor({this.Name})");
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
                    var prevValue = this.LastValues[name];
                    if (prevValue > value) // if the new value is smaller than previous - it's reset
                        prevValue = 0;
                    this.Data[time][name] = value - prevValue;
                }

                addedData[name] = this.Data[time][name];
                item.Remove("name");
                item.Remove("value");
                this.Metadata[name] = item.ToObject<Dictionary<string, object>>();
            }
            this.Owner.Owner.Events.Fire(this, "SensorDataAdded", addedData);
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
                foreach (var subName in this.SubNames)
                {
                    var values = items.Where(i => i.ContainsKey(subName)).Select(i => i[subName]);
                    if (!values.Any())
                        continue;

                    var aggrType = "avg";
                    if (this.Metadata.ContainsKey(subName) && this.Metadata[subName].ContainsKey("aggrType"))
                        aggrType = (string)this.Metadata[subName]["aggrType"];

                    double newValue = 0.0;
                    if (aggrType == "avg")
                        newValue = values.Average();
                    else if (aggrType == "sum")
                        newValue = values.Sum();
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
            times = this.Data.Keys.Where(t => t < now.Date.AddHours(now.Hour).AddDays(-1));
            var groupedDates = times.GroupBy(t => t.Date);
            this.AggregateData(groupedDates);
        }

        protected abstract JToken ReadDataInternal();
    }
}
