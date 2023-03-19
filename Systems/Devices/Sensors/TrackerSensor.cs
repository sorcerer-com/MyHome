using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;

using MQTTnet;

using MyHome.Models;
using MyHome.Utils;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using NLog;

namespace MyHome.Systems.Devices.Sensors
{
    public class WifiTrackerSensor : BaseSensor
    {
        private static readonly ILogger logger = LogManager.GetCurrentClassLogger();

        private static readonly string netInfoCacheFilePath = Path.Join(Config.BinPath, "wifi_net_info_cache.json");
        private static readonly Dictionary<string, (DateTime time, JToken netInfo)> netInfoCache = new();
        private const int NET_INFO_CACHE_VALIDITY = 7; // days

        private const string STATE_TOPIC = "STATE";
        private const string SENSOR_TOPIC = "SENSOR";
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Minor Code Smell", "S1075:URIs should not be hardcoded")]
        private const string WIGLE_API_URL = "https://api.wigle.net/api/v2";

        private string baseMqttTopic;
        [UiProperty(true)]
        public string BaseMqttTopic
        {
            get => this.baseMqttTopic;
            set
            {
                if (this.baseMqttTopic == value) return;

                MyHome.Instance.MqttClient.Unsubscribe(this.BaseMqttTopic + "/" + STATE_TOPIC);
                MyHome.Instance.MqttClient.Unsubscribe(this.BaseMqttTopic + "/" + SENSOR_TOPIC);
                this.baseMqttTopic = value;
                MyHome.Instance.MqttClient.Subscribe(this.BaseMqttTopic + "/" + STATE_TOPIC);
                MyHome.Instance.MqttClient.Subscribe(this.BaseMqttTopic + "/" + SENSOR_TOPIC);
            }
        }

        [JsonIgnore]
        public Dictionary<DateTime, string> WifiLists { get; private set; }

        private DateTime nextCall;
        private readonly HttpClient httpClient;
        private int wifiIdx;
        private int itemIdx;
        private readonly Dictionary<int, JToken> netInfos;


        public WifiTrackerSensor()
        {
            this.WifiLists = new Dictionary<DateTime, string>();
            this.SubNamesMap["Vcc"] = "Vcc";
            this.SubNamesMap["lat"] = "Latitude";
            this.SubNamesMap["long"] = "Longitude";
            this.SubNamesMap["acc"] = "Accuracy";

            this.nextCall = DateTime.Now + TimeSpan.FromMinutes(1);
            this.httpClient = new HttpClient();
            this.wifiIdx = -1;
            this.itemIdx = 0;
            this.netInfos = new Dictionary<int, JToken>();
        }

        public override void Setup()
        {
            base.Setup();

            // subscribe for the topics
            MyHome.Instance.MqttClient.Subscribe(this.BaseMqttTopic + "/" + STATE_TOPIC);
            MyHome.Instance.MqttClient.Subscribe(this.BaseMqttTopic + "/" + SENSOR_TOPIC);

            // process messages
            MyHome.Instance.MqttClient.ApplicationMessageReceived += this.MqttClient_ApplicationMessageReceived;

            // wigle http client
            var token = Encoding.UTF8.GetString(Convert.FromBase64String(MyHome.Instance.Config.WigleApiToken));
            var byteArray = new UTF8Encoding().GetBytes($"{MyHome.Instance.Config.WigleApiName}:{token}");
            this.httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("basic", Convert.ToBase64String(byteArray));

            this.LoadWifiLists();
        }

        public override void Stop()
        {
            base.Stop();

            MyHome.Instance.MqttClient.Unsubscribe(this.BaseMqttTopic + "/" + STATE_TOPIC);
            MyHome.Instance.MqttClient.Unsubscribe(this.BaseMqttTopic + "/" + SENSOR_TOPIC);

            // remove handlers
            MyHome.Instance.MqttClient.ApplicationMessageReceived -= this.MqttClient_ApplicationMessageReceived;
        }

        public override void Update()
        {
            base.Update();

            if (this.WifiLists.Count == 0 || DateTime.Now < this.nextCall)
                return;
            this.nextCall = DateTime.Now + TimeSpan.FromSeconds(10);

            if (this.wifiIdx == -1) // if we need to process new wifi list, set wifiIdx to the last one
                this.wifiIdx = this.WifiLists.Count - 1;
            var entry = this.WifiLists.ElementAt(this.wifiIdx);
            var json = JToken.Parse(entry.Value);
            if (this.itemIdx >= json.Count()) // no more items in this list
            {
                this.ProcessNetInfo(entry);
                return;
            }

            var bssid = (string)json[this.itemIdx]["BSSID"];
            var info = this.GetNetworkInfo(bssid);
            if (info == null) // exceed the daily queries quota
            {
                logger.Warn($"Sensor '{this.Name}' ({this.Room.Name}) exceed getting network info daily quota");
                if (this.netInfos.Count > 0) // if there is at least one info add the location
                    this.ProcessNetInfo(entry);
                this.nextCall = DateTime.Now + TimeSpan.FromDays(1);
                return;
            }
            logger.Debug($"Sensor '{this.Name}' ({this.Room.Name}) info for network {json[this.itemIdx]}: {info["success"]}");
            if (!(bool)info["success"]) // no such network - remove it from json and continue
            {
                json[this.itemIdx].Remove();
                this.WifiLists[entry.Key] = json.ToString(Formatting.None);
                this.SaveWifiLists();
                return;
            }

            this.netInfos.Add(this.itemIdx, info);
            this.itemIdx++;
            if (this.netInfos.Count == 2)
                this.ProcessNetInfo(entry);
        }

        private void MqttClient_ApplicationMessageReceived(object sender, MqttApplicationMessageReceivedEventArgs e)
        {
            if (e.ApplicationMessage.Topic != this.BaseMqttTopic + "/" + STATE_TOPIC &&
                e.ApplicationMessage.Topic != this.BaseMqttTopic + "/" + SENSOR_TOPIC)
                return;

            logger.Trace($"Process sensor '{this.Name}' ({this.Room.Name}) MQTT message with topic: {e.ApplicationMessage.Topic}");

            try
            {
                var payload = Encoding.UTF8.GetString(e.ApplicationMessage.Payload);

                if (e.ApplicationMessage.Topic == this.BaseMqttTopic + "/" + STATE_TOPIC)
                {
                    var json = JToken.Parse(payload);
                    var token = json.SelectToken("Vcc");
                    if (token != null)
                    {
                        var property = token.Parent as JProperty;
                        var value = property.Value;

                        var data = new Dictionary<string, object>
                        {
                            { "Vcc", (double)value }
                        };
                        this.AddData(DateTime.Now, data);
                    }
                }
                else
                {
                    this.WifiLists.Add(DateTime.Now, payload);
                    this.SaveWifiLists();
                }
            }
            catch (Exception ex)
            {
                logger.Error("Failed to process MQTT message");
                logger.Debug(ex);
            }
        }

        private void LoadWifiLists()
        {
            // load net info cache if it isn't already
            if (netInfoCache.Count == 0 && File.Exists(netInfoCacheFilePath))
                JsonConvert.PopulateObject(File.ReadAllText(netInfoCacheFilePath), netInfoCache);

            if (this.baseMqttTopic == null)
                return;

            var trackerId = this.baseMqttTopic[this.baseMqttTopic.LastIndexOf("/")..];
            if (File.Exists($"bin/{trackerId}.txt"))
            {
                var lines = File.ReadAllLines($"bin/{trackerId}.txt");
                var now = DateTime.Now;
                for (int i = 0; i < lines.Length; i += 2)
                {
                    var time = DateTime.Parse(lines[i]);
                    if (lines.Length - i > 2 && now - time > TimeSpan.FromDays(1)) // if there are another entries skip older than 1 day
                        continue;
                    this.WifiLists.Add(time, lines[i + 1]);
                }
                logger.Trace($"Load sensor '{this.Name}' ({this.Room.Name}) Wifi lists: {this.WifiLists.Count}");
            }
        }

        private void SaveWifiLists()
        {
            logger.Trace($"Save sensor '{this.Name}' ({this.Room.Name}) Wifi lists: {this.WifiLists.Count}");
            var trackerId = this.baseMqttTopic[this.baseMqttTopic.LastIndexOf("/")..];
            File.WriteAllLines($"bin/{trackerId}.txt", this.WifiLists.Select(kvp => kvp.Key + "\n" + kvp.Value));

            // remove invalid entries and save net info cache
            var now = DateTime.Now;
            var toRemove = netInfoCache.Where(kvp => now - kvp.Value.time > TimeSpan.FromDays(NET_INFO_CACHE_VALIDITY)).ToList();
            toRemove.ForEach(kvp => netInfoCache.Remove(kvp.Key));
            var formatting = MyHome.Instance.Config.SavePrettyJson ? Formatting.Indented : Formatting.None;
            File.WriteAllText(netInfoCacheFilePath, JsonConvert.SerializeObject(netInfoCache, formatting));
        }

        private JToken GetNetworkInfo(string bssid)
        {
            // if there is entry in the cache
            if (netInfoCache.TryGetValue(bssid, out var item))
                return item.netInfo;

            JToken result = null;
            try
            {
                var response = this.httpClient.GetAsync(WIGLE_API_URL + "/network/detail?type=WIFI&netid=" + Uri.EscapeDataString(bssid)).Result;
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    result = new JObject
                    {
                        ["success"] = false
                    };
                }
                else if (response.IsSuccessStatusCode)
                    result = JToken.Parse(response.Content.ReadAsStringAsync().Result);
            }
            catch (Exception ex)
            {
                logger.Error($"Sensor '{this.Name}' ({this.Room.Name}) failed to get network info for: {bssid}");
                logger.Debug(ex);
            }

            logger.Trace($"Sensor '{this.Name}' ({this.Room.Name}) get info for network {bssid}: {result}");
            if (result != null)
                netInfoCache[bssid] = (DateTime.Now, result);
            return result;
        }

        private void ProcessNetInfo(KeyValuePair<DateTime, string> entry)
        {
            if (this.AddLocationData(entry.Key)) // if success check next after SensorsCheckInterval minutes
            {
                this.nextCall = DateTime.Now + TimeSpan.FromMinutes(MyHome.Instance.DevicesSystem.SensorsCheckInterval);
                // remove older entries in SensorsCheckInterval minutes range
                var keysToRemove = this.WifiLists.Keys
                    .Where(t => t < entry.Key && entry.Key - t < TimeSpan.FromMinutes(MyHome.Instance.DevicesSystem.SensorsCheckInterval));
                foreach (var key in keysToRemove)
                    this.WifiLists.Remove(key);
            }
            this.WifiLists.Remove(entry.Key);
            this.SaveWifiLists();
            this.wifiIdx = -1;
            this.itemIdx = 0;
            this.netInfos.Clear();
        }

        private bool AddLocationData(DateTime time)
        {
            if (this.netInfos.Count == 0) // no real info
            {
                logger.Debug($"Sensor '{this.Name}' ({this.Room.Name}) no location to add");
                return false;
            }

            var data = new Dictionary<string, object>();
            if (this.netInfos.Count == 1)
            {
                JToken info = this.netInfos.Values.First();
                data["lat"] = info["results"][0]["locationData"].Average(c => (double)c["latitude"]);
                data["long"] = info["results"][0]["locationData"].Average(c => (double)c["longitude"]);
                data["acc"] = -info["results"][0]["locationData"].Average(c => (double)c["accuracy"]);
            }
            else
            {
                // triangulate (2D) by reported signal of the two networks
                var json = JToken.Parse(this.WifiLists[time]);
                var info1 = this.netInfos.First();
                var info2 = this.netInfos.Last();
                int rssi1 = Math.Abs((int)json[info1.Key]["RSSI"]);
                int rssi2 = Math.Abs((int)json[info2.Key]["RSSI"]);
                double ratio1 = (double)rssi1 / (rssi1 + rssi2);
                double ratio2 = (double)rssi2 / (rssi1 + rssi2);

                data["lat"] = info1.Value["results"][0]["locationData"].Average(c => (double)c["latitude"]) * ratio1 +
                    info2.Value["results"][0]["locationData"].Average(c => (double)c["latitude"]) * ratio2;
                data["long"] = info1.Value["results"][0]["locationData"].Average(c => (double)c["longitude"]) * ratio1 +
                    info2.Value["results"][0]["locationData"].Average(c => (double)c["longitude"]) * ratio2;
                data["acc"] = info1.Value["results"][0]["locationData"].Average(c => (double)c["accuracy"]) * ratio1 +
                    info2.Value["results"][0]["locationData"].Average(c => (double)c["accuracy"]) * ratio2;
            }
            logger.Trace($"Sensor '{this.Name}' ({this.Room.Name}) add location {time} lat:{data["lat"]} long:{data["long"]} acc:{data["acc"]}");
            this.AddData(time, data);
            return true;
        }
    }
}
