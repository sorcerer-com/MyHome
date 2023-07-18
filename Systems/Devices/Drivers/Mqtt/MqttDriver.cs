using System;
using System.Collections.Generic;
using System.Linq;

using MQTTnet;
using MQTTnet.Client;

using MyHome.Utils;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using NLog;

namespace MyHome.Systems.Devices.Drivers.Mqtt
{
    public abstract class MqttDriver : BaseDriver
    {
        private static readonly ILogger logger = LogManager.GetCurrentClassLogger();


        private string onlineMqttTopic;
        [UiProperty(true)]
        public string OnlineMqttTopic
        {
            get => this.onlineMqttTopic;
            set
            {
                if (this.onlineMqttTopic == value)
                    return;

                if (MyHome.Instance.MqttClient.IsConnected)
                {
                    MyHome.Instance.MqttClient.Unsubscribe(this.onlineMqttTopic);
                    MyHome.Instance.MqttClient.Subscribe(value);
                }
                this.onlineMqttTopic = value;
            }
        }

        private DateTime lastOnline;
        [JsonIgnore]
        [UiProperty]
        public override DateTime LastOnline => this.lastOnline;


        protected Dictionary<string, (string topic, string jsonPath)> MqttGetTopics { get; }

        protected Dictionary<string, (string topic, string jsonPath)> MqttSetTopics { get; }


        protected MqttDriver()
        {
            this.onlineMqttTopic = "";
            this.lastOnline = DateTime.Now;
            this.MqttGetTopics = new Dictionary<string, (string topic, string jsonPath)>();
            this.MqttSetTopics = new Dictionary<string, (string topic, string jsonPath)>();
        }


        public override void Setup()
        {
            base.Setup();

            // subscribe for the topics
            MyHome.Instance.MqttClient.Subscribe(this.onlineMqttTopic);

            foreach (var (topic, _) in this.MqttGetTopics.Values)
                MyHome.Instance.MqttClient.Subscribe(topic);

            // process messages
            MyHome.Instance.MqttClient.ApplicationMessageReceived += this.MqttClient_ApplicationMessageReceived;
        }

        public override void Stop()
        {
            base.Stop();

            // unsubscribe for the topics
            foreach (var (topic, _) in this.MqttGetTopics.Values)
                MyHome.Instance.MqttClient.Unsubscribe(topic);

            MyHome.Instance.MqttClient.Unsubscribe(this.onlineMqttTopic);

            // remove handlers
            MyHome.Instance.MqttClient.ApplicationMessageReceived -= this.MqttClient_ApplicationMessageReceived;
        }


        private void MqttClient_ApplicationMessageReceived(object sender, MqttApplicationMessageReceivedEventArgs e)
        {
            if (this.onlineMqttTopic != e.ApplicationMessage.Topic &&
                !this.MqttGetTopics.Values.Any(value => e.ApplicationMessage.Topic == value.topic))
                return;

            logger.Trace($"Process driver '{this.Name}' ({this.Room.Name}) MQTT message with topic: {e.ApplicationMessage.Topic}");

            try
            {
                // online topic was updated, so update LastOnline time
                if (this.onlineMqttTopic == e.ApplicationMessage.Topic)
                    this.lastOnline = DateTime.Now;

                var payload = e.ApplicationMessage.ConvertPayloadToString();
                if (string.IsNullOrEmpty(payload) || !this.AcceptPayload(payload))
                {
                    logger.Trace($"The received payload is not accepted: {payload}");
                    return;
                }
                bool changed = false, save = false;
                foreach (var item in this.MqttGetTopics.Where(kvp => kvp.Value.topic == e.ApplicationMessage.Topic))
                {
                    var value = payload;
                    if (!string.IsNullOrEmpty(item.Value.jsonPath))
                    {
                        var jsonToken = JToken.Parse(payload).SelectToken(item.Value.jsonPath);
                        if (jsonToken == null)
                            continue;
                        value = jsonToken.ToString();
                    }
                    var oldValue = this.States[item.Key];
                    if (this.States[item.Key] is bool && (value.ToUpper() == "ON" || value.ToUpper() == "OFF"))
                        this.States[item.Key] = value.ToUpper() == "ON";
                    else
                        this.States[item.Key] = Utils.Utils.ParseValue(value, this.States[item.Key]?.GetType());

                    if (oldValue != this.States[item.Key])
                    {
                        changed = true;
                        // allow every sub-driver to return if specific state should be saved
                        if (this.NewStateReceived(item.Key, oldValue, this.States[item.Key]))
                            save = true;
                    }
                }
                if (changed)
                {
                    MyHome.Instance.SystemChanged = save;
                    MyHome.Instance.Events.Fire(this, GlobalEventTypes.DriverStateChanged, this.States);
                }
            }
            catch (Exception ex)
            {
                logger.Error($"Failed to process MQTT message from topic: {e.ApplicationMessage.Topic}");
                logger.Debug(ex);
            }
        }

        protected virtual bool AcceptPayload(string payload)
        {
            return true;
        }

        protected void SetGetTopic(string name, (string topic, string jsonPath) value)
        {
            if (this.MqttGetTopics[name] == value)
                return;

            if (MyHome.Instance.MqttClient.IsConnected &&
                this.MqttGetTopics[name].topic != value.topic)
            {
                MyHome.Instance.MqttClient.Unsubscribe(this.MqttGetTopics[name].topic);
                MyHome.Instance.MqttClient.Subscribe(value.topic);
            }
            this.MqttGetTopics[name] = value;
        }

        protected virtual bool NewStateReceived(string name, object oldValue, object newValue)
        {
            return true;
        }

        protected void SetStateAndSend(string name, object value)
        {
            if (base.SetState(name, value))
                this.SendState(name, value.ToString());
        }

        protected void SendState(string name, string value)
        {
            if (!MyHome.Instance.MqttClient.IsConnected)
                return;

            if (!string.IsNullOrEmpty(this.MqttSetTopics[name].jsonPath))
            {
                var json = new JObject
                {
                    [this.MqttSetTopics[name].jsonPath] = value
                };
                value = json.ToString();
            }

            logger.Debug($"Send {name} state of {this.Name} ({this.Room.Name}): {value}");
            MyHome.Instance.MqttClient.Publish(this.MqttSetTopics[name].topic, value);
        }
    }
}
