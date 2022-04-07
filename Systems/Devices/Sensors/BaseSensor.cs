﻿using System;
using System.Collections.Generic;
using System.Linq;

using MyHome.Utils;

using Newtonsoft.Json;

using NLog;

namespace MyHome.Systems.Devices.Sensors
{
    public abstract class BaseSensor : Device
    {
        private static readonly ILogger logger = LogManager.GetCurrentClassLogger();

        public class SensorValue : Dictionary<string, double> { }; // subName / value


        public Dictionary<DateTime, SensorValue> Data { get; }

        [UiProperty(true, "real name / custom name")]
        public Dictionary<string, string> SubNamesMap { get; } // map sensor subname to custom subname

        [UiProperty(true, "names")]
        public List<string> SumAggregated { get; } // subnames

        [UiProperty(true, "name / expression of x")]
        public Dictionary<string, string> Calibration { get; } // calibration values per subname: newValue = expression(realValue)

        [UiProperty(true, "name / unit")]
        public Dictionary<string, string> Units { get; } // subname / unit name (if not provide by metadata)


        [JsonProperty]
        private Dictionary<string, double> LastReadings { get; } // subname / value


        [JsonIgnore]
        public Dictionary<string, Dictionary<string, object>> Metadata => this.GetMetadata();

        [JsonIgnore]
        [UiProperty]
        public Dictionary<string, double> Values => this.GetValues();


        protected BaseSensor()
        {
            this.Data = new Dictionary<DateTime, SensorValue>();
            this.SubNamesMap = new Dictionary<string, string>();
            this.SumAggregated = new List<string>();
            this.Calibration = new Dictionary<string, string>();
            this.Units = new Dictionary<string, string>();

            this.LastReadings = new Dictionary<string, double>();
        }


        public void AddData(DateTime time, Dictionary<string, object> data)
        {
            logger.Trace($"Sensor '{this.Name}' ({this.Room.Name}) add data at {time:dd/MM/yyyy HH:mm:ss}: {string.Join('\n', data)}");
            if (data.Count == 0)
                return;

            if (!this.Data.ContainsKey(time))
                this.Data.Add(time, new SensorValue());

            foreach (var item in data)
            {
                var name = item.Key;
                if (this.SubNamesMap.ContainsKey(name))
                    name = this.SubNamesMap[name];

                var value = (item.Value is bool b) ? (b ? 1 : 0) : (double)item.Value;
                if (this.Calibration.ContainsKey(name)) // mapped name
                {
                    MyHome.Instance.ExecuteJint(jint => value = (double)jint.SetValue("x", value).Evaluate(this.Calibration[name]).ToObject(),
                        "calculate value: " + this.Calibration[name]);
                }

                var sumAggr = this.SumAggregated.Contains(name);
                if (!sumAggr)
                    this.Data[time][name] = value;
                else // sum type - differentiate
                {
                    this.LastReadings.TryGetValue(name, out var prevValue);
                    // TODO: uncomment once the Energy Monitor values are fixed
                    //if (prevValue - 1 > value) // if the new value is smaller (with epsilon - 1) than previous - it's reset
                    //    prevValue = 0;
                    if (prevValue + 1 < value)
                        this.Data[time][name] = Math.Round(value - prevValue, 2); // round to 2 decimals after the point
                    else
                        this.Data[time][name] = 0;
                    this.LastReadings[name] = value;
                }
                this.Data[time].TrimExcess();
            }
            MyHome.Instance.Events.Fire(this, GlobalEventTypes.SensorDataAdded, this.Data[time]);
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

                    var sumAggr = this.SumAggregated.Contains(subName);

                    double newValue = 0.0;
                    if (!sumAggr)
                        newValue = Math.Round(values.Average(), 2); // round to 2 decimals after the point
                    else
                        newValue = Math.Round(values.Sum(), 2);
                    this.Data[group.Key][subName] = newValue;
                }
                this.Data[group.Key].TrimExcess();
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

        private Dictionary<string, Dictionary<string, object>> GetMetadata()
        {
            var result = new Dictionary<string, Dictionary<string, object>>();
            foreach (var key in this.Values.Keys)
            {
                var info = new Dictionary<string, object>
                {
                    { "aggrType", this.SumAggregated.Contains(key) ? "sum" : "avg" }
                };
                if (this.Units.ContainsKey(key))
                    info.Add("unit", this.Units[key]);
                result.Add(key, info);
            }
            return result;
        }

        private Dictionary<string, double> GetValues()
        {
            var result = this.Data.OrderBy(kvp => kvp.Key)
                .SelectMany(kvp => kvp.Value)
                .GroupBy(x => x.Key)
                .ToDictionary(g => g.Key, g => g.Last().Value);

            // for sum type sensors - set value to be the sum for the day
            var sumType = this.Data.Where(kvp => kvp.Key > DateTime.Now.Date)
                .SelectMany(kvp => kvp.Value)
                .GroupBy(x => x.Key)
                .Where(g => this.SumAggregated.Contains(g.Key))
                .ToList();
            sumType.ForEach(g => result[g.Key] = g.Select(x => x.Value).Sum());
            return result;
        }
    }
}
