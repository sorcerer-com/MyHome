using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Protocol;

using NLog;

namespace MyHome.Utils
{
    public class MqttClientWrapper
    {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();

        private readonly IMqttClient MqttClient;
        private readonly Dictionary<string, int> Subscriptions;

        public bool IsConnected => this.MqttClient.IsConnected;
        public DateTime LastMessageReceived { get; private set; }

        public event EventHandler<MqttClientConnectedEventArgs> Connected;
        public event EventHandler<MqttClientDisconnectedEventArgs> Disconnected;
        public event EventHandler<MqttApplicationMessageReceivedEventArgs> ApplicationMessageReceived;


        public MqttClientWrapper()
        {
            var factory = new MqttFactory();
            this.MqttClient = factory.CreateMqttClient();
            this.Subscriptions = new Dictionary<string, int>();

            this.LastMessageReceived = DateTime.Now;
        }


        public void Connect(string clientId, string server, int? port, string username, string password, bool autoreconnect = true)
        {
            logger.Debug($"Connect MQTT client '{clientId}' to {server}:{port}");

            this.MqttClient.ConnectedAsync += e =>
            {
                logger.Debug("MQTT client connected");
                this.Connected?.Invoke(this, e);

                // re-subscribe for the topics
                foreach (var topic in this.Subscriptions.Keys)
                    this.MqttClient.SubscribeAsync(topic);

                return Task.CompletedTask;
            };

            this.MqttClient.DisconnectedAsync += e =>
            {
                this.Disconnected?.Invoke(this, e);
                if (autoreconnect)
                {
                    logger.Debug("MQTT client disconnected. Try to reconnect...");
                    Thread.Sleep(1000);
                    this.MqttClient.ReconnectAsync();
                }

                return Task.CompletedTask;
            };

            this.MqttClient.ApplicationMessageReceivedAsync += e =>
            {
                logger.Trace($"Process MQTT message with topic '{e.ApplicationMessage.Topic}': {e.ApplicationMessage.ConvertPayloadToString()}");
                this.LastMessageReceived = DateTime.Now;
                this.ApplicationMessageReceived?.Invoke(this, e);

                return Task.CompletedTask;
            };

            var options = new MqttClientOptionsBuilder()
                .WithClientId(clientId + AppDomain.CurrentDomain.BaseDirectory.GetHashCode()) // add "random" hash since cannot connect two clients with same id
                .WithTcpServer(server, port)
                .WithCredentials(username, password)
                .WithTimeout(TimeSpan.FromSeconds(10))
                .WithCleanSession()
                .Build();

            this.MqttClient.ConnectAsync(options, CancellationToken.None);
        }

        public void Disconnect()
        {
            logger.Debug($"Disconnect MQTT client '{this.MqttClient.Options?.ClientId}'");
            this.MqttClient.DisconnectAsync();
            this.Subscriptions.Clear();
        }

        public void Subscribe(string topic)
        {
            if (string.IsNullOrEmpty(topic))
                return;

            if (!this.Subscriptions.ContainsKey(topic))
                this.Subscriptions.Add(topic, 0);
            this.Subscriptions[topic] = this.Subscriptions[topic] + 1;

            logger.Trace($"Subscribe MQTT client '{this.MqttClient.Options?.ClientId}' for topic: {topic} ({this.Subscriptions[topic]})");
            if (this.MqttClient.IsConnected)
                this.MqttClient.SubscribeAsync(topic);
        }

        public void Unsubscribe(string topic)
        {
            if (string.IsNullOrEmpty(topic))
                return;

            logger.Trace($"Unsubscribe MQTT client '{this.MqttClient.Options?.ClientId}' for topic: {topic} " +
                $"({(this.Subscriptions.ContainsKey(topic) ? this.Subscriptions[topic] : 0)})");
            if (this.Subscriptions.ContainsKey(topic))
            {
                this.Subscriptions[topic] = this.Subscriptions[topic] - 1;
                if (this.Subscriptions[topic] != 0)
                    return; // don't unsubscribe if there are more "clients" subscribed
                else
                    this.Subscriptions.Remove(topic);
            }

            if (this.MqttClient.IsConnected)
                this.MqttClient.UnsubscribeAsync(topic);
        }

        public void Publish(string topic, string payload, int qualityOfService = 0, bool retain = false)
        {
            if (!this.MqttClient.IsConnected)
                return;

            logger.Trace($"Publish message on topic '{topic}' (QoS: {qualityOfService}, retain: {retain}): {payload}");
            try
            {
                var message = new MqttApplicationMessageBuilder()
                    .WithTopic(topic)
                    .WithPayload(payload)
                    .WithQualityOfServiceLevel((MqttQualityOfServiceLevel)qualityOfService)
                    .WithRetainFlag(retain)
                    .Build();

                this.MqttClient.PublishAsync(message, CancellationToken.None);
            }
            catch (Exception e)
            {
                logger.Error("Failed to publish message on topic: {topic}");
                logger.Debug(e);
            }
        }
    }
}
