using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;

using MQTTnet;
using MQTTnet.Client;

using MyHome.Utils;

using Newtonsoft.Json.Linq;

using NLog;

namespace MyHome.Systems.Devices.Sensors
{
    public class MqttSensor : BaseSensor
    {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();

        [UiProperty(true, "(topic, json path)")]
        public ObservableCollection<(string topic, string jsonPath)> MqttTopics { get; }


        public MqttSensor()
        {
            this.MqttTopics = new ObservableCollection<(string topic, string jsonPath)>();
            this.MqttTopics.CollectionChanged += this.MqttTopics_CollectionChanged;
        }

        public override void Setup()
        {
            base.Setup();

            // subscribe for the topics
            foreach (var (topic, _) in this.MqttTopics)
                MyHome.Instance.MqttClient.Subscribe(topic);

            // process messages
            MyHome.Instance.MqttClient.ApplicationMessageReceived += this.MqttClient_ApplicationMessageReceived;
        }

        public override void Stop()
        {
            base.Stop();

            foreach (var (topic, _) in this.MqttTopics)
                MyHome.Instance.MqttClient.Unsubscribe(topic);

            // remove handlers
            MyHome.Instance.MqttClient.ApplicationMessageReceived -= this.MqttClient_ApplicationMessageReceived;
        }

        private void MqttTopics_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (!MyHome.Instance.MqttClient.IsConnected)
                return;

            if (e.Action == NotifyCollectionChangedAction.Reset)
            {
                foreach (var (topic, _) in this.MqttTopics)
                    MyHome.Instance.MqttClient.Unsubscribe(topic);
            }
            else
            {
                if (e.OldItems != null)
                {
                    foreach (var (topic, _) in e.OldItems.OfType<(string topic, string jsonPath)>())
                        MyHome.Instance.MqttClient.Unsubscribe(topic);
                }
                if (e.NewItems != null)
                {
                    foreach (var (topic, _) in e.NewItems.OfType<(string topic, string jsonPath)>())
                        MyHome.Instance.MqttClient.Subscribe(topic);
                }
            }
        }

        private void MqttClient_ApplicationMessageReceived(object sender, MqttApplicationMessageReceivedEventArgs e)
        {
            if (!this.MqttTopics.Any(value => value.topic == e.ApplicationMessage.Topic))
                return;

            logger.Trace($"Process sensor '{this.Name}' ({this.Room.Name}) MQTT message with topic: {e.ApplicationMessage.Topic}");

            try
            {
                var payload = e.ApplicationMessage.ConvertPayloadToString();
                var json = JToken.Parse(payload);

                var data = new Dictionary<string, object>();
                foreach (var (_, jsonPath) in this.MqttTopics.Where(kvp => kvp.topic == e.ApplicationMessage.Topic))
                {
                    var token = json.SelectToken(jsonPath);
                    if (token == null)
                        continue;

                    var property = token.Parent as JProperty;
                    var value = property.Value;
                    if (value.Type == JTokenType.String && ((string)value == "ON" || (string)value == "OFF"))
                        value = (string)value == "ON";
                    data.Add(jsonPath, (double)value);
                }
                this.AddData(DateTime.Now, data);
            }
            catch (Exception ex)
            {
                logger.Error("Failed to process MQTT message");
                logger.Debug(ex);
            }
        }
    }
}
