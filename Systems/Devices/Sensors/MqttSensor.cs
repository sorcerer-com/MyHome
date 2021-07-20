using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Text;

using MQTTnet;
using MQTTnet.Client.Connecting;

using MyHome.Models;
using MyHome.Utils;

using Newtonsoft.Json.Linq;

using NLog;

namespace MyHome.Systems.Devices.Sensors
{
    public class MqttSensor : BaseSensor
    {
        private static readonly ILogger logger = LogManager.GetCurrentClassLogger();

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0051:Remove unused private member")]
        private new string Address { get; set; }

        [UiProperty(true, "(topic, json path)")]
        public ObservableCollection<(string topic, string jsonPath)> MqttTopics { get; }


        private MqttClientWrapper MqttClient => this.Owner?.Owner.MqttClient;


        private MqttSensor() : this(null, null, null) { } // for json deserialization

        public MqttSensor(DevicesSystem owner, string name, Room room) : base(owner, name, room, null)
        {
            this.MqttTopics = new ObservableCollection<(string topic, string jsonPath)>();
            this.MqttTopics.CollectionChanged += this.MqttTopics_CollectionChanged;
        }

        public override void Setup()
        {
            base.Setup();

            // subscribe to topic
            this.MqttClient.Connected += this.MqttClient_Connected;

            // process messages
            this.MqttClient.ApplicationMessageReceived += this.MqttClient_ApplicationMessageReceived;
        }

        public override void Stop()
        {
            base.Stop();

            foreach (var (topic, _) in this.MqttTopics)
                this.MqttClient.Unsubscribe(topic);

            // remove handlers
            this.MqttClient.Connected -= this.MqttClient_Connected;
            this.MqttClient.ApplicationMessageReceived -= this.MqttClient_ApplicationMessageReceived;
        }

        private void MqttTopics_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (this.MqttClient?.IsConnected != true)
                return;

            if (e.Action == NotifyCollectionChangedAction.Reset)
            {
                foreach (var (topic, _) in this.MqttTopics)
                    this.MqttClient.Unsubscribe(topic);
            }
            else
            {
                if (e.OldItems != null)
                {
                    foreach (var (topic, _) in e.OldItems.OfType<(string topic, string jsonPath)>())
                        this.MqttClient.Unsubscribe(topic);
                }
                if (e.NewItems != null)
                {
                    foreach (var (topic, _) in e.NewItems.OfType<(string topic, string jsonPath)>())
                        this.MqttClient.Subscribe(topic);
                }
            }
        }

        private void MqttClient_Connected(object sender, MqttClientConnectedEventArgs e)
        {
            foreach (var (topic, _) in this.MqttTopics)
                this.MqttClient.Subscribe(topic);
        }

        private void MqttClient_ApplicationMessageReceived(object sender, MqttApplicationMessageReceivedEventArgs e)
        {
            if (!this.MqttTopics.Any(value => value.topic == e.ApplicationMessage.Topic))
                return;

            logger.Debug($"Process sensor '{this.Name}' ({this.Room.Name}) MQTT message with topic: {e.ApplicationMessage.Topic}");

            try
            {
                var payload = Encoding.UTF8.GetString(e.ApplicationMessage.Payload);
                var json = JToken.Parse(payload);

                var data = new JArray();
                foreach (var (_, jsonPath) in this.MqttTopics.Where(kvp => kvp.topic == e.ApplicationMessage.Topic))
                {
                    var token = json.SelectToken(jsonPath);
                    if (token == null)
                        continue;

                    var property = token.Parent as JProperty;
                    var value = property.Value;
                    if (value.Type == JTokenType.String && ((string)value == "ON" || (string)value == "OFF"))
                        value = (string)value == "ON";
                    var item = new JObject
                    {
                        ["name"] = property.Name,
                        ["value"] = value
                    };
                    data.Add(item);
                }
                this.AddData(DateTime.Now, data);
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Failed to process MQTT message");
            }
        }

        protected override JToken ReadDataInternal()
        {
            return new JArray();
        }
    }
}
