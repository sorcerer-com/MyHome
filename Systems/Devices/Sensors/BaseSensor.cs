using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using MyHome.Models;
using MyHome.Utils;

using Newtonsoft.Json;

using NLog;

namespace MyHome.Systems.Devices.Sensors
{
    public abstract class BaseSensor : Device
    {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();

        public class SensorValue : Dictionary<string, double> { }; // subName / value

        public enum AggregationType
        {
            AverageByTime,
            Average,
            SumDiff, // differentiate income values and sum them on aggregate
            Sum // sum incoming values on aggregate
        }

        public override DateTime LastOnline => this.Data.Keys.OrderBy(t => t).LastOrDefault();


        [JsonIgnore]
        public Dictionary<DateTime, SensorValue> Data { get; }

        [UiProperty(true, "real name / custom name")]
        public Dictionary<string, string> SubNamesMap { get; } // map sensor subname to custom subname

        [UiProperty(true, "name / aggregation type")]
        public Dictionary<string, AggregationType> AggregationMap { get; } // subname / aggregation type

        [UiProperty(true, "name / expression of x")]
        public Dictionary<string, string> Calibration { get; } // calibration values per subname: newValue = expression(realValue)

        [UiProperty(true, "name / unit")]
        public Dictionary<string, string> Units { get; } // subname / unit name

        [UiProperty(true, "name")]
        public List<string> NotTimeseries { get; } // subnames that need code to generate intermediate values

        [UiProperty(true)]
        public bool Grouped { get; set; } // whether subnames should be grouped in UI 


        [JsonProperty]
        private Dictionary<string, double> LastReadings { get; } // subname / value


        [JsonIgnore]
        public Dictionary<string, Dictionary<string, object>> Metadata => this.GetMetadata();

        [JsonIgnore]
        [UiProperty]
        public Dictionary<string, double> Values => this.GetValues();


        [JsonProperty]
        protected string dataFilePath;
        private DateTime? saveTime;
        [JsonProperty]
        private DateTime lastBackupTime;


        protected BaseSensor()
        {
            this.Data = new Dictionary<DateTime, SensorValue>();
            this.SubNamesMap = new Dictionary<string, string>();
            this.AggregationMap = new Dictionary<string, AggregationType>();
            this.Calibration = new Dictionary<string, string>();
            this.Units = new Dictionary<string, string>();
            this.NotTimeseries = new List<string>();
            this.Grouped = false;

            this.LastReadings = new Dictionary<string, double>();
            this.dataFilePath = null;

            this.saveTime = null;
            this.lastBackupTime = DateTime.Now;
        }

        public override void Setup()
        {
            base.Setup();

            if (File.Exists(this.dataFilePath))
            {
                try
                {
                    try
                    {
                        var json = File.ReadAllText(this.dataFilePath);
                        JsonConvert.PopulateObject(json, this.Data);
                    }
                    catch
                    {
                        // retry with the copy file
                        logger.Warn($"Cannot load sensor data '{this.Name}' ({this.Room.Name}), retry with the copy");
                        var json2 = File.ReadAllText(this.dataFilePath + "1");
                        JsonConvert.PopulateObject(json2, this.Data);
                    }
                    // TODO: remove data older than 365 days, replace save with append, file format without opening and closing json brackets
                }
                catch (Exception e)
                {
                    logger.Error($"Cannot load sensor data '{this.Name}' ({this.Room.Name})");
                    logger.Debug(e);
                    Alert.Create("Cannot load sensor data")
                        .Details($"'{this.Name}' ({this.Room.Name})")
                        .Validity(TimeSpan.FromHours(1))
                        .Send();
                }
            }
        }

        public override void Stop()
        {
            base.Stop();

            // ensure saved on stop
            this.saveTime = DateTime.Now - TimeSpan.FromSeconds(1);
            this.Update();
        }

        public override void Update()
        {
            base.Update();

            if (DateTime.Now > this.saveTime)
            {
                this.saveTime = null;

                logger.Trace($"Save sensor '{this.Name}' ({this.Room.Name}) data");
                var fileName = Path.Join(Config.BinPath, Utils.Utils.GetValidFileName($"data_{this.Room.Name}_{this.Name}.json"));
                this.dataFilePath ??= fileName; // initial set if  DataFilePath is null
                if (this.dataFilePath != fileName) // if there is a change in the name (room/sensor rename)
                {
                    if (File.Exists(fileName))
                        logger.Warn($"Sensor data file already exists: {fileName}");
                    else
                    {
                        if (File.Exists(this.dataFilePath))
                            File.Move(this.dataFilePath, fileName);
                        this.dataFilePath = fileName;
                    }
                }

                // backup data file every day
                if (DateTime.Now - this.lastBackupTime > TimeSpan.FromDays(1))
                {
                    this.lastBackupTime = DateTime.Now;
                    if (File.Exists(this.dataFilePath))
                        File.Copy(this.dataFilePath, this.dataFilePath + ".bak", true);
                }

                lock (this.Data)
                {
                    Utils.Utils.Retry(_ =>
                    {
                        var formatting = MyHome.Instance.Config.SavePrettyJson ? Formatting.Indented : Formatting.None;
                        var json = JsonConvert.SerializeObject(this.Data, formatting);
                        File.WriteAllText(this.dataFilePath, json);
                        File.WriteAllText(this.dataFilePath + "1", json); // save a copy if the first one is broken
                    }, 3, logger, $"save sensor '{this.Name}' ({this.Room.Name}) data");
                }
            }
        }

        public virtual void SaveData()
        {
            // save only once per minute
            if (this.saveTime == null)
                this.saveTime = DateTime.Now + TimeSpan.FromMinutes(1);
        }


        public void AddData(DateTime time, Dictionary<string, object> data)
        {
            lock (this.Data)
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

                    var value = Convert.ToDouble(item.Value);
                    if (this.Calibration.ContainsKey(name)) // mapped name
                    {
                        MyHome.Instance.ExecuteJint(jint => value = (double)jint.SetValue("x", value).Evaluate(this.Calibration[name]).ToObject(),
                            "calculate value: " + this.Calibration[name]);
                    }

                    this.AggregationMap.TryGetValue(name, out var aggrType);
                    if (aggrType != AggregationType.SumDiff)
                        this.Data[time][name] = value;
                    else // sum diff type - differentiate
                    {
                        this.LastReadings.TryGetValue(name, out var prevValue);
                        // TODO: uncomment once the Energy Monitor values are fixed
                        //if (prevValue - 1 > value) // if the new value is smaller (with epsilon - 1) than previous - it's reset
                        //    prevValue = 0;
                        if (prevValue + 1 < value)
                            this.Data[time][name] = Math.Round(value - prevValue, 4); // round to 4 decimals after the point
                        else
                            this.Data[time][name] = 0;
                        this.LastReadings[name] = value;
                    }
                }
                this.Data[time].TrimExcess();
                MyHome.Instance.Events.Fire(this, GlobalEventTypes.SensorDataAdded, this.Data[time]);
                this.ArchiveData();
                this.SaveData();
            }
        }

        public void GenerateTimeseries()
        {
            lock (this.Data)
            {
                // if subsensor is not timeseries, add last value to fill the gaps in time
                var now = DateTime.Now;
                foreach (var subname in this.NotTimeseries.Distinct())
                {
                    if (!this.Data.ContainsKey(now))
                        this.Data[now] = new SensorValue();
                    this.Data[now].Add(subname, this.Values.ContainsKey(subname) ? this.Values[subname] : 0);
                }
                this.ArchiveData();
                this.SaveData();
            }
        }

        public void AggregateData(IEnumerable<IGrouping<DateTime, DateTime>> groupedDates)
        {
            lock (this.Data)
            {
                foreach (var group in groupedDates) // new time / list of times to be grouped
                {
                    if (group.Count() == 1) // already grouped
                        continue;

                    var items = group.ToDictionary(t => t, t => this.Data[t]);
                    // delete old records
                    foreach (var t in group)
                        this.Data.Remove(t);
                    // add one new
                    this.Data[group.Key] = new SensorValue();
                    var subNames = items.Select(i => i.Value.Keys).SelectMany(x => x).Distinct();
                    foreach (var subName in subNames)
                    {
                        var values = items.Where(i => i.Value.ContainsKey(subName)).ToDictionary(i => i.Key, i => i.Value[subName]);
                        if (!values.Any())
                            continue;

                        this.AggregationMap.TryGetValue(subName, out var aggrType);

                        double newValue = 0.0;
                        if (aggrType == AggregationType.Average)
                            newValue = Math.Round(values.Values.Average(), 4);
                        else if (aggrType == AggregationType.AverageByTime)
                            newValue = Math.Round(AverageByTime(values), 4);
                        else // AggregationType.SumDiff and AggregationType.Sum
                            newValue = Math.Round(values.Values.Sum(), 4);
                        this.Data[group.Key][subName] = newValue;
                    }
                    this.Data[group.Key].TrimExcess();
                }
            }
        }

        private void ArchiveData()
        {
            this.RemoveUnwantedSubData();

            var now = DateTime.Now;
            // TODO: don't delete for now to see how file size will be
            // delete entries older then 3 year
            /*var times = this.Data.Keys.Where(t => t < now.AddYears(-3));
            foreach (var time in times)
                this.Data.Remove(time);*/

            // for older then 24 hour, save only 1 per day
            var times = this.Data.Keys.Where(t => t < now.Date.AddDays(-1));
            var groupedDates = times.GroupBy(t => t.Date);
            this.AggregateData(groupedDates);
        }

        private void RemoveUnwantedSubData()
        {
            // remove subdata with subname not defined in SubNamesMap
            var unwantedDataSubNames = this.Values.Keys.Where(subName => !this.SubNamesMap.ContainsValue(subName));
            if (!unwantedDataSubNames.Any())
                return;

            logger.Warn($"The following '{this.Name}' sensor sub-data will be removed: {string.Join(", ", unwantedDataSubNames)}");
            foreach (var date in this.Data.Keys)
            {
                foreach (var subName in unwantedDataSubNames)
                    this.Data[date].Remove(subName);
            }

            foreach (var subName in unwantedDataSubNames)
                this.LastReadings.Remove(subName);
        }

        private Dictionary<string, Dictionary<string, object>> GetMetadata()
        {
            var result = new Dictionary<string, Dictionary<string, object>>();
            foreach (var key in this.Values.Keys)
            {
                this.AggregationMap.TryGetValue(key, out var aggrType);
                var info = new Dictionary<string, object>
                {
                    { "aggrType", aggrType.ToString() },
                    { "LastOnline", this.LastOnline }
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
                .Where(g => this.AggregationMap.ContainsKey(g.Key) && this.AggregationMap[g.Key] == AggregationType.SumDiff)
                .ToList();
            sumType.ForEach(g => result[g.Key] = g.Select(x => x.Value).Sum());
            return result;
        }

        private static double AverageByTime(Dictionary<DateTime, double> values)
        {
            if (values.Count <= 2)
                return values.Values.Average();

            double result = 0.0;
            var ordered = values.OrderBy(kvp => kvp.Key).ToList();
            for (int i = 0; i < ordered.Count - 1; i++)
            {
                var duration = (ordered[i + 1].Key - ordered[i].Key).TotalSeconds;
                result += ordered[i].Value * duration;
            }
            // add last value multiplied by average duration
            var totalTime = (ordered[^1].Key - ordered[0].Key).TotalSeconds;
            var avgTime = totalTime / (ordered.Count - 1);
            result += ordered[^1].Value * avgTime;

            return result / (totalTime + avgTime);
        }
    }
}