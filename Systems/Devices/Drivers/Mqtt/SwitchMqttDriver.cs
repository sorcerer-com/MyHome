using MyHome.Utils;

namespace MyHome.Systems.Devices.Drivers.Mqtt
{
    public class SwitchMqttDriver : MqttDriver
    {
        private const string SWITCH_STATE_NAME = "On";

        [UiProperty]
        public bool IsOn
        {
            get => (bool)this.States[SWITCH_STATE_NAME];
            set => this.SetStateAndSend(SWITCH_STATE_NAME, value);
        }

        [UiProperty(true, "(topic, json path)")]
        public (string topic, string jsonPath) IsOnGetMqttTopic
        {
            get => this.MqttGetTopics[SWITCH_STATE_NAME];
            set => this.SetGetTopic(SWITCH_STATE_NAME, value);
        }

        [UiProperty(true, "(topic, json path)")]
        public (string topic, string jsonPath) IsOnSetMqttTopic
        {
            get => this.MqttSetTopics[SWITCH_STATE_NAME];
            set => this.MqttSetTopics[SWITCH_STATE_NAME] = value;
        }

        [UiProperty(true)]
        public bool ConfirmationRequired { get; set; }


        public SwitchMqttDriver()
        {
            this.States.Add(SWITCH_STATE_NAME, false);
            this.MqttGetTopics.Add(SWITCH_STATE_NAME, ("", ""));
            this.MqttSetTopics.Add(SWITCH_STATE_NAME, ("", ""));
        }

        public bool Toggle()
        {
            this.IsOn = !this.IsOn;
            return this.IsOn;
        }
    }
}
