using System;
using System.Collections.Generic;
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
        // TODO: check for offline - alert - LWT
        private static readonly ILogger logger = LogManager.GetCurrentClassLogger();

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0051:Remove unused private member")]
        private new string Address { get; set; }

        private string mqttTopic;
        [UiProperty(true)]
        public string MqttTopic
        {
            get => this.mqttTopic;
            set
            {
                if (this.MqttClient?.IsConnected == true)
                {
                    this.MqttClient.Unsubscribe(this.mqttTopic);
                    this.MqttClient.Subscribe(value);
                }
                this.mqttTopic = value;
            }
        }

        [UiProperty(true)]
        public List<string> JsonPaths { get; private set; }


        private MqttClientWrapper MqttClient => this.Owner?.Owner.MqttClient;


        private MqttSensor() : this(null, null, null, null) { } // for json deserialization

        public MqttSensor(DevicesSystem owner, string name, Room room, string mqttTopic) : base(owner, name, room, null)
        {
            this.MqttTopic = mqttTopic;
            this.JsonPaths = new List<string>();
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

            // remove handlers
            this.MqttClient.Unsubscribe(this.mqttTopic);
            this.MqttClient.Connected -= this.MqttClient_Connected;
            this.MqttClient.ApplicationMessageReceived -= this.MqttClient_ApplicationMessageReceived;
        }

        private void MqttClient_Connected(object sender, MqttClientConnectedEventArgs e)
        {
            this.MqttClient.Subscribe(this.mqttTopic);
        }

        private void MqttClient_ApplicationMessageReceived(object sender, MqttApplicationMessageReceivedEventArgs e)
        {
            if (e.ApplicationMessage.Topic != this.mqttTopic)
                return;

            logger.Debug($"Process sensor '{this.Name}' ({this.Room.Name}) MQTT message with topic: {this.mqttTopic}");

            try
            {
                var payload = Encoding.UTF8.GetString(e.ApplicationMessage.Payload);
                var json = JToken.Parse(payload);

                var data = new JArray();
                foreach (var path in this.JsonPaths)
                {
                    var token = json.SelectToken(path);
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
