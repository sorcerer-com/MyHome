using MyHome.Utils;

using Newtonsoft.Json.Linq;

using NLog;

namespace MyHome.Systems.Devices.Drivers.Mqtt
{
    public class SwitchMqttDriver : MqttDriver
    {
        private static readonly ILogger logger = LogManager.GetCurrentClassLogger();

        private const string SWITCH_STATE_NAME = "On";

        [UiProperty]
        public bool IsOn
        {
            get => (bool)this.State[SWITCH_STATE_NAME];
            set => this.SetState(SWITCH_STATE_NAME, value, this.SendSwitchState);
        }

        [UiProperty(true, "(topic, json path)")]
        public (string topic, string jsonPath) IsOnGetMqttTopic
        {
            get => this.MqttGetTopics[SWITCH_STATE_NAME];
            set => this.SetGetTopic(SWITCH_STATE_NAME, value);
        }

        [UiProperty(true, "(topic, json path)")]
        public (string topic, string jsonPath) IsOnSetMqttTopic { get; set; }

        [UiProperty(true)]
        public bool ConfirmationRequired { get; set; }


        public SwitchMqttDriver()
        {
            this.State.Add(SWITCH_STATE_NAME, false);
            this.MqttGetTopics.Add(SWITCH_STATE_NAME, ("", ""));
            this.IsOnSetMqttTopic = ("", "");
        }

        public bool Toggle()
        {
            this.IsOn = !this.IsOn;
            return this.IsOn;
        }


        private void SendSwitchState()
        {
            var value = this.IsOn.ToString();
            if (!string.IsNullOrEmpty(this.IsOnSetMqttTopic.jsonPath))
            {
                var json = new JObject
                {
                    [this.IsOnSetMqttTopic.jsonPath] = this.IsOn
                };
                value = json.ToString();
            }

            if (MyHome.Instance.MqttClient.IsConnected)
            {
                logger.Info($"Send state to {this.Name} ({this.Room.Name}): {value}");
                MyHome.Instance.MqttClient.Publish(this.IsOnSetMqttTopic.topic, value);
            }
        }
    }
}
