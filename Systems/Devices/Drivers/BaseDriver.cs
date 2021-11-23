﻿using System;
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

        protected Dictionary<string, (string topic, string jsonPath)> MqttSetTopics { get; }


        protected BaseDriver()
        {
            this.State = new Dictionary<string, object>();

            this.MqttGetTopics = new Dictionary<string, (string topic, string jsonPath)>();
            this.MqttSetTopics = new Dictionary<string, (string topic, string jsonPath)>();
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
                    if (this.State[item.Key] is bool && (value == "ON" || value == "OFF"))
                        this.State[item.Key] = value == "ON";
                    else
                        this.State[item.Key] = Utils.Utils.ParseValue(value, this.State[item.Key].GetType());

                    if (oldValue != this.State[item.Key])
                    {
                        MyHome.Instance.SystemChanged = true;
                        MyHome.Instance.Events.Fire(this, GlobalEventTypes.DriverStateChanged,
                            this.State.Where(kvp => kvp.Key == item.Key).ToDictionary(kvp => kvp.Key, kvp => kvp.Value));
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Error("Failed to process MQTT message");
                logger.Debug(ex);
            }
        }
    }
}
