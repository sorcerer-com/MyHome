using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using MQTTnet;
using MQTTnet.Client.Connecting;

using MyHome.Utils;

using Newtonsoft.Json.Linq;

using NLog;

namespace MyHome.Systems.Devices.Drivers
{
    public abstract class BaseDriver : Device
    {
        private static readonly ILogger logger = LogManager.GetCurrentClassLogger();


        protected Dictionary<string, object> State { get; }

        protected Dictionary<string, (string topic, string jsonPath)> MqttGetTopics { get; }


        protected BaseDriver()
        {
            this.State = new Dictionary<string, object>();

            this.MqttGetTopics = new Dictionary<string, (string topic, string jsonPath)>();
        }


        public override void Setup()
        {
            base.Setup();

            // subscribe to topic
            MyHome.Instance.MqttClient.Connected += this.MqttClient_Connected;

            // process messages
            MyHome.Instance.MqttClient.ApplicationMessageReceived += this.MqttClient_ApplicationMessageReceived;
        }

        public override void Stop()
        {
            base.Stop();

            foreach (var (topic, _) in this.MqttGetTopics.Values)
                MyHome.Instance.MqttClient.Unsubscribe(topic);

            // remove handlers
            MyHome.Instance.MqttClient.Connected -= this.MqttClient_Connected;
            MyHome.Instance.MqttClient.ApplicationMessageReceived -= this.MqttClient_ApplicationMessageReceived;
        }


        private void MqttClient_Connected(object sender, MqttClientConnectedEventArgs e)
        {
            foreach (var (topic, _) in this.MqttGetTopics.Values)
                MyHome.Instance.MqttClient.Subscribe(topic);
        }

        private void MqttClient_ApplicationMessageReceived(object sender, MqttApplicationMessageReceivedEventArgs e)
        {
            if (!this.MqttGetTopics.Values.Any(value => e.ApplicationMessage.Topic == value.topic))
                return;

            logger.Trace($"Process driver '{this.Name}' ({this.Room.Name}) MQTT message with topic: {e.ApplicationMessage.Topic}");

            try
            {
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

                    changed |= oldValue != this.State[item.Key];
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

        protected void SetState(string name, object value, Action call)
        {
            if (this.State[name] == value)
                return;

            this.State[name] = value;
            call?.Invoke();
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
    }
}
