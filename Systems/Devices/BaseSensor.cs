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


        [JsonIgnore]
        public DateTime? LastTime => this.Data.Count > 0 ? this.Data.Max(d => d.Key) : null;

        [JsonIgnore]
        public SensorValue LastValues => this.LastTime.HasValue ? this.Data[this.LastTime.Value] : null;

        [JsonIgnore]
        public List<string> SubNames => this.LastValues.Keys.ToList();

        // TODO: add sensor calibration values - (realValue + const1) * const2
        // TODO: add subsensor name map - realName -> mappedName


        public BaseSensor(DevicesSystem owner, string name, Room room, string address) : base(owner, name, room)
        {
            this.Address = address;
            this.Data = new Dictionary<DateTime, SensorValue>();
            this.Metadata = new Dictionary<string, Dictionary<string, object>>();
        }


        public override void Update()
        {
        }

        public bool ReadData(DateTime time) // TODO: add bigger only?
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

            foreach (var item in data.OfType<JObject>())
            {
                if (!item.ContainsKey("name") && !item.ContainsKey("value"))
                {
                    logger.Warn($"Try to add invalid data item({item}) in sensor({this.Name})");
                    continue;
                }
                var name = (string)item["name"];
                var value = (item["value"].Type == JTokenType.Boolean) ? ((bool)item["value"] ? 1 : 0) : (double)item["value"];
                var aggrType = item.ContainsKey("aggrType") ? (string)item["aggrType"] : "avg";
                if (aggrType == "avg")
                {
                    // TODO: addBiggerOnly?
                    this.Data[time][name] = value;
                }
                else // sum type - differentiate
                {
                    var prevValue = this.LastValues[name];
                    if (prevValue > value) // if the new value is smaller than previous - it's reset
                        prevValue = 0;
                    this.Data[time][name] = value - prevValue;
                    //TODO: value -= prevValue;
                }
                item.Remove("name");
                item.Remove("value");
                this.Metadata[name] = item.ToObject<Dictionary<string, object>>();//.Where(kvp => kvp.Key != "name" && kvp.Key != "value").ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
            }
            this.ArchiveData();
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
            var groupedDate = times.GroupBy(t => t.Date);
            foreach (var group in groupedDate) // date / times
            {
                if (group.Count() == 1) // already archived
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

        protected abstract JToken ReadDataInternal();
    }
}
