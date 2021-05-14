using System;
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

        public bool IsConnected => this.MqttClient.IsConnected;

        public event EventHandler<MqttClientConnectedEventArgs> Connected;
        public event EventHandler<MqttClientDisconnectedEventArgs> Disconnected;
        public event EventHandler<MqttApplicationMessageReceivedEventArgs> ApplicationMessageReceived;


        public MqttClientWrapper()
        {
            var factory = new MqttFactory();
            this.MqttClient = factory.CreateMqttClient();
        }


        public void Connect(string clientId, string server, int? port, string username, string password, bool autoreconnect = true)
        {
            logger.Debug($"Connect MQTT client '{clientId}' to {server}:{port}");

            this.MqttClient.UseConnectedHandler(e => this.Connected?.Invoke(this, e));

            this.MqttClient.UseDisconnectedHandler(e =>
            {
                Disconnected?.Invoke(this, e);
                if (autoreconnect)
                {
                    logger.Debug("MQTT client disconnected. Try to reconnect...");
                    Thread.Sleep(5);
                    this.MqttClient.ReconnectAsync();
                }
            });

            this.MqttClient.UseApplicationMessageReceivedHandler(e => this.ApplicationMessageReceived?.Invoke(this, e));

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
            this.MqttClient.DisconnectAsync();
        }

        public void Subscribe(string topic)
        {
            this.MqttClient.SubscribeAsync(topic);
        }

        public void Unsubscribe(string topic)
        {
            this.MqttClient.UnsubscribeAsync(topic);
        }
    }
}
