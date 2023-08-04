using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using MQTTnet;
using MQTTnet.Client;

using MyHome.Models;
using MyHome.Systems.Devices;
using MyHome.Systems.Devices.Drivers;
using MyHome.Systems.Devices.Drivers.Mqtt;
using MyHome.Systems.Devices.Sensors;
using MyHome.Utils;
using MyHome.Utils.Ewelink;
using MyHome.Utils.Tuya;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using NLog;

namespace MyHome.Systems
{
    public class DevicesSystem : BaseSystem
    {
        private static readonly ILogger logger = LogManager.GetCurrentClassLogger();

        [UiProperty(true, "minutes")]
        public int SensorsCheckInterval { get; set; } // minutes

        public List<Device> Devices { get; }

        [JsonIgnore]
        public IEnumerable<BaseSensor> Sensors => this.Devices.OfType<BaseSensor>();

        [JsonIgnore]
        public IEnumerable<Camera> Cameras => this.Devices.OfType<Camera>();

        [JsonIgnore]
        public IEnumerable<BaseDriver> Drivers => this.Devices.OfType<BaseDriver>();

        private bool autoDiscovery;
        [UiProperty(true)]
        public bool AutoDiscovery
        {
            get => this.autoDiscovery;
            set
            {
                if (this.autoDiscovery == value)
                    return;

                this.autoDiscovery = value;
                if (value)
                {
                    MyHome.Instance.MqttClient.Subscribe("tele/#");
                    MyHome.Instance.MqttClient.ApplicationMessageReceived += this.MqttClient_ApplicationMessageReceived;

                    this.tuyaScanner.Start();
                    this.tuyaScanner.OnNewDeviceInfoReceived += this.TuyaScanner_OnNewDeviceInfoReceived;
                }
                else
                {
                    MyHome.Instance.MqttClient.ApplicationMessageReceived -= this.MqttClient_ApplicationMessageReceived;
                    MyHome.Instance.MqttClient.Unsubscribe("tele/#");

                    this.tuyaScanner.OnNewDeviceInfoReceived -= this.TuyaScanner_OnNewDeviceInfoReceived;
                    this.tuyaScanner.Stop();

                    this.DiscoveredDevices.Clear();
                }
            }
        }

        [JsonIgnore]
        [UiProperty]
        public List<Device> DiscoveredDevices { get; }

        [JsonIgnore]
        public EwelinkV2 Ewelink { get; }

        private DateTime nextSensorCheckTime;
        private readonly TuyaScanner tuyaScanner;


        public DevicesSystem()
        {
            this.SensorsCheckInterval = 15;
            this.Devices = new List<Device>();
            this.DiscoveredDevices = new List<Device>();

            var ewelinkPassword = Encoding.UTF8.GetString(Convert.FromBase64String(MyHome.Instance.Config.EwelinkPassword));
            this.Ewelink = new EwelinkV2(MyHome.Instance.Config.EwelinkCountryCode, MyHome.Instance.Config.EwelinkEmail, ewelinkPassword);

            this.tuyaScanner = new TuyaScanner();

            Directory.CreateDirectory(MyHome.Instance.Config.CameraRecordsPath);
        }


        public override void Setup()
        {
            base.Setup();

            // set after loading the GetSensorDataInterval
            this.nextSensorCheckTime = this.GetNextSensorCheckTime();

            foreach (var device in this.Devices)
                device.Setup();
        }

        public override void Stop()
        {
            base.Stop();

            foreach (var device in this.Devices)
                device.Stop();

            this.tuyaScanner.Stop();
        }

        protected override void Update()
        {
            base.Update();

            this.Devices.RunForEach(device => device.Update());

            if (DateTime.Now < this.nextSensorCheckTime)
                return;

            // if SensorsCheckInterval is changed
            if (this.nextSensorCheckTime.Minute % this.SensorsCheckInterval != 0)
                this.nextSensorCheckTime = this.GetNextSensorCheckTime();

            var alertMsg = "";
            this.Devices.RunForEach(device =>
            {
                // check for inactive device
                if (device.LastOnline <= this.nextSensorCheckTime.AddMinutes(-this.SensorsCheckInterval * 4) &&
                    device.LastOnline > this.nextSensorCheckTime.AddMinutes(-this.SensorsCheckInterval * 5))
                {
                    var type = device is BaseSensor ? "Sensor" : "Driver";
                    logger.Warn($"{type} {device.Name} ({device.Room.Name}) is not active from {device.LastOnline}");
                    alertMsg += $"{device.Name} ({device.Room.Name}) inactive ";
                }

                // TODO: maybe move to sensor update
                if (device is BaseSensor sensor)
                {
                    sensor.GenerateTimeseries();

                    // aggregate one value per SensorsCheckInterval
                    var now = DateTime.Now;
                    var times = sensor.Data.Keys.Where(t => t < now.AddHours(-1) && t >= now.Date.AddHours(now.Hour).AddDays(-1)); // last 24 hours
                    var groupedDates = times.GroupBy(t => new DateTime(t.Year, t.Month, t.Day, t.Hour, t.Minute / this.SensorsCheckInterval * this.SensorsCheckInterval, 0));
                    sensor.AggregateData(groupedDates);
                }
            });

            if (!string.IsNullOrEmpty(alertMsg))
                Alert.Create($"{alertMsg.Trim()} alarm activated!").Send();

            // discover devices
            if (this.autoDiscovery)
                Task.Run(() => this.DiscoverEwelinkDevices());

            this.nextSensorCheckTime += TimeSpan.FromMinutes(this.SensorsCheckInterval);
            MyHome.Instance.SystemChanged = true;
        }

        private DateTime GetNextSensorCheckTime()
        {
            var now = DateTime.Now;
            var time = new DateTime(now.Year, now.Month, now.Day, now.Hour, 0, 0, 0);
            while (time < now)
                time += TimeSpan.FromMinutes(this.SensorsCheckInterval);
            return time;
        }


        // Device Discovery
        public void RemoveDiscoveredDevice(int idx)
        {
            this.DiscoveredDevices.RemoveAt(idx);
        }

        private void MqttClient_ApplicationMessageReceived(object sender, MqttApplicationMessageReceivedEventArgs e)
        {
            try
            {
                var topic = e.ApplicationMessage.Topic;
                var knownDevices = this.Devices.Union(this.DiscoveredDevices);
                if (knownDevices.OfType<MqttSensor>().Any(s => s.MqttTopics.Any(t => t.topic == topic)) ||
                    knownDevices.OfType<MqttDriver>().Any(d => d.OnlineMqttTopic == topic))
                {
                    // there is already such device
                    return;
                }

                var payload = e.ApplicationMessage.ConvertPayloadToString();
                var json = JToken.Parse(payload);

                List<Device> devices = new();
                // all topics currently starts with "tele/"
                if (topic.EndsWith("SENSOR"))
                {
                    var name = topic.Replace("tele/", "").Replace("/SENSOR", "");
                    var props = this.GetValueProperties(json).Where(p => p.Name != "Time");
                    // sensor
                    if (props.Any() && !name.StartsWith("tracker"))
                    {
                        var mqttSensor = new MqttSensor();
                        foreach (var prop in props)
                        {
                            mqttSensor.MqttTopics.Add((topic, prop.Path));
                            mqttSensor.SubNamesMap.Add(prop.Path, prop.Name);
                        }
                        mqttSensor.Name = name;
                        devices.Add(mqttSensor);
                    }
                    // tracker
                    if (name.StartsWith("tracker"))
                    {
                        devices.Add(new WifiTrackerSensor()
                        {
                            Name = name,
                            BaseMqttTopic = $"tele/{name}"
                        });
                    }
                    // switch driver
                    if (props.Any(p => p.Name == "Switch1"))
                    {
                        devices.Add(new SwitchMqttDriver
                        {
                            Name = name,
                            OnlineMqttTopic = topic,
                            IsOnGetMqttTopic = ($"stat/{name}/RESULT", "POWER"),
                            IsOnSetMqttTopic = ($"cmnd/{name}/power", ""),
                            ConfirmationRequired = true
                        });
                    }
                }
                else if (topic.EndsWith("STATE"))
                {
                    var name = topic.Replace("tele/", "").Replace("/STATE", "");
                    // light driver
                    if (json.SelectToken("Color") is JValue)
                    {
                        devices.Add(new LightMqttDriver
                        {
                            Name = name,
                            OnlineMqttTopic = topic,
                            ColorGetMqttTopic = ($"stat/{name}/RESULT", "Color"),
                            ColorSetMqttTopic = ($"cmnd/{name}/Color", ""),
                            IsOnGetMqttTopic = ($"stat/{name}/RESULT", "POWER"),
                            IsOnSetMqttTopic = ($"cmnd/{name}/power", "")
                        });
                    }
                }
                else if (topic.EndsWith("RESULT"))
                {
                    var name = topic.Replace("tele/", "").Replace("/RESULT", "");
                    // IR AC receiver
                    if (json.SelectToken("IrReceived.IRHVAC") != null)
                    {
                        devices.Add(new AcIrMqttDriver()
                        {
                            Name = name,
                            OnlineMqttTopic = $"tele/{name}/STATE",
                            Vendor = (string)json.SelectToken("IrReceived.IRHVAC.Vendor"),
                            StateGetMqttTopic = (topic, "IrReceived.IRHVAC"),
                            StateSetMqttTopic = ($"cmnd/{name}/IRhvac", "")
                        });
                    }
                }
                // media agent
                else if (topic.EndsWith("MEDIA_LIST"))
                {
                    var name = topic.Replace("tele/", "").Replace("/MEDIA_LIST", "");
                    if (knownDevices.OfType<MediaAgentMqttDriver>().Any(d => d.OnlineMqttTopic == $"tele/{name}/SENSOR"))
                        return;
                    devices.Add(new MediaAgentMqttDriver
                    {
                        Name = name,
                        OnlineMqttTopic = $"tele/{name}/SENSOR",
                        AgentHostName = name
                    });
                }

                if (devices.Count > 0)
                {
                    this.DiscoveredDevices.AddRange(devices);
                    this.DiscoveredDevices.Sort((a, b) => a.Name.CompareTo(b.Name));
                }
            }
            catch (Exception ex)
            {
                logger.Debug("Failed to process MQTT message");
                logger.Debug(ex);
            }
        }

        private IEnumerable<JProperty> GetValueProperties(JToken jToken)
        {
            var list = new List<JProperty>();
            if (jToken is JObject jObj)
            {
                foreach (var prop in jObj.Properties())
                {
                    if (prop.Value is JValue)
                        list.Add(prop);
                    else
                        list.AddRange(this.GetValueProperties(prop.Value));
                }
            }
            else if (jToken is JArray jArray)
            {
                foreach (var item in jArray)
                    list.AddRange(this.GetValueProperties(item));
            }
            return list;
        }

        private void DiscoverEwelinkDevices()
        {
            try
            {
                var knownDevices = this.Devices.Union(this.DiscoveredDevices);

                var devices = this.Ewelink.GetDevices().Result;
                foreach (var device in devices)
                {
                    if (knownDevices.OfType<EwelinkRfDriver>().Any(d => d.EwelinkDeviceId == device.Deviceid))
                        continue;

                    // RF bridge
                    if (device.ProductModel == "RFBridge433" || device.ProductModel == "RF_Bridge")
                    {
                        foreach (var rfObj in device.Paramaters.RfList.OfType<JObject>())
                        {
                            var chl = (int)rfObj.SelectToken("rfChl");
                            this.DiscoveredDevices.Add(new EwelinkRfDriver
                            {
                                Name = $"{device.Name} {chl}",
                                EwelinkDeviceId = device.Deviceid,
                                EwelinkRfChannel = chl
                            });
                        }
                    }
                }
                this.DiscoveredDevices.Sort((a, b) => a.Name.CompareTo(b.Name));
            }
            catch (Exception ex)
            {
                logger.Debug("Failed to get eWeLink devices");
                logger.Debug(ex);
            }
        }

        private void TuyaScanner_OnNewDeviceInfoReceived(object sender, TuyaDeviceScanInfo e)
        {
            try
            {
                var knownDevices = this.Devices.Union(this.DiscoveredDevices);
                if (knownDevices.OfType<TuyaSwitchDriver>().Any(d => d.TuyaDeviceId == e.GwId))
                    return;

                this.DiscoveredDevices.Add(new TuyaSwitchDriver
                {
                    Name = e.IP,
                    TuyaDeviceId = e.GwId,
                    TuyaDeviceIp = e.IP,
                    TuyaSwitchIdx = 1
                });
            }
            catch (Exception ex)
            {
                logger.Debug("Failed to process new Tuya device");
                logger.Debug(ex);
            }
        }
    }
}
