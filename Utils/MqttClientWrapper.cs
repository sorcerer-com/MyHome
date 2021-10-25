using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Client.Connecting;
using MQTTnet.Client.Disconnecting;
using MQTTnet.Client.Options;

using NLog;

namespace MyHome.Utils
{
    public class MqttClientWrapper
    {
        private static readonly ILogger logger = LogManager.GetCurrentClassLogger();

        private readonly IMqttClient MqttClient;
        private readonly Dictionary<string, int> Subscriptions;

        public bool IsConnected => this.MqttClient.IsConnected;

        public event EventHandler<MqttClientConnectedEventArgs> Connected;
        public event EventHandler<MqttClientDisconnectedEventArgs> Disconnected;
        public event EventHandler<MqttApplicationMessageReceivedEventArgs> ApplicationMessageReceived;


        public MqttClientWrapper()
        {
            var factory = new MqttFactory();
            this.MqttClient = factory.CreateMqttClient();
            this.Subscriptions = new Dictionary<string, int>();
        }


        public void Connect(string clientId, string server, int? port, string username, string password, bool autoreconnect = true)
        {
            logger.Debug($"Connect MQTT client '{clientId}' to {server}:{port}");

            this.MqttClient.UseConnectedHandler(e =>
            {
                logger.Debug("MQTT client connected");
                this.Connected?.Invoke(this, e);
            });

            this.MqttClient.UseDisconnectedHandler(e =>
            {
                Disconnected?.Invoke(this, e);
                if (autoreconnect)
                {
                    logger.Debug("MQTT client disconnected. Try to reconnect...");
                    Thread.Sleep(5000);
                    this.MqttClient.ReconnectAsync();
                }
            });

            this.MqttClient.UseApplicationMessageReceivedHandler(e =>
            {
                logger.Trace($"Process MQTT message with topic '{e.ApplicationMessage.Topic}': {Encoding.UTF8.GetString(e.ApplicationMessage.Payload)}");
                this.ApplicationMessageReceived?.Invoke(this, e);
            });

            var options = new MqttClientOptionsBuilder()
                .WithClientId(clientId)
                .WithTcpServer(server, port)
                .WithCredentials(username, password)
                .WithCommunicationTimeout(TimeSpan.FromSeconds(10))
                .WithCleanSession()
                .Build();

            this.MqttClient.ConnectAsync(options, CancellationToken.None);
        }

        public void Disconnect()
        {
            logger.Debug($"Disconnect MQTT client '{this.MqttClient.Options.ClientId}'");
            this.MqttClient.DisconnectAsync();
        }

        public void Subscribe(string topic)
        {
            if (string.IsNullOrEmpty(topic))
                return;

            if (!this.Subscriptions.ContainsKey(topic))
                this.Subscriptions.Add(topic, 0);
            this.Subscriptions[topic] = this.Subscriptions[topic] + 1;

            logger.Trace($"Subscribe MQTT client '{this.MqttClient.Options.ClientId}' for topic: {topic} ({this.Subscriptions[topic]})");
            this.MqttClient.SubscribeAsync(topic);
        }

        public void Unsubscribe(string topic)
        {
            if (string.IsNullOrEmpty(topic))
                return;

            logger.Trace($"Unsubscribe MQTT client '{this.MqttClient.Options.ClientId}' for topic: {topic} ({this.Subscriptions[topic]})");
            if (this.Subscriptions.ContainsKey(topic))
            {
                this.Subscriptions[topic] = this.Subscriptions[topic] - 1;
                if (this.Subscriptions[topic] != 0)
                    return; // don't unsubscribe if there are more "clients" subscribed
                else
                    this.Subscriptions.Remove(topic);
            }

            this.MqttClient.UnsubscribeAsync(topic);
        }

        public void Publish(string topic, string payload, int qualityOfService = 0, bool retain = false)
        {
            logger.Trace($"Publish message on topic '{topic}' (QoS: {qualityOfService}, retain: {retain}): {payload}");
            try
            {
                var message = new MqttApplicationMessageBuilder()
                    .WithTopic(topic)
                    .WithPayload(payload)
                    .WithQualityOfServiceLevel(qualityOfService)
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
