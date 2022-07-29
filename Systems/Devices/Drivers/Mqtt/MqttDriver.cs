using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using MQTTnet;

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


        protected MqttDriver()
        {
            this.onlineMqttTopic = "";
            this.lastOnline = DateTime.Now;
            this.MqttGetTopics = new Dictionary<string, (string topic, string jsonPath)>();
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
                // update online topic was updated, so update LastOnline time
                if (this.onlineMqttTopic == e.ApplicationMessage.Topic)
                {
                    this.lastOnline = DateTime.Now;
                    return;
                }

                var payload = Encoding.UTF8.GetString(e.ApplicationMessage.Payload);
                if (!this.AcceptPayload(payload))
                {
                    logger.Trace("The received payload is not accepted");
                    return;
                }
                bool changed = false;
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
                    var oldValue = this.State[item.Key];
                    if (this.State[item.Key] is bool && (value.ToUpper() == "ON" || value.ToUpper() == "OFF"))
                        this.State[item.Key] = value.ToUpper() == "ON";
                    else
                        this.State[item.Key] = Utils.Utils.ParseValue(value, this.State[item.Key].GetType());

                    if (oldValue != this.State[item.Key])
                    {
                        changed = true;
                        this.NewStateReceived(item.Key, oldValue, this.State[item.Key]);
                    }
                }
                if (changed)
                {
                    MyHome.Instance.SystemChanged = true;
                    MyHome.Instance.Events.Fire(this, GlobalEventTypes.DriverStateChanged, this.State);
                }
            }
            catch (Exception ex)
            {
                logger.Error("Failed to process MQTT message");
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

        protected virtual void NewStateReceived(string state, object oldValue, object newValue)
        {
        }
    }
}
