using MyHome.Utils;

namespace MyHome.Systems.Devices.Drivers.Mqtt
{
    public class LightMqttDriver : SwitchMqttDriver
    {
        private const string COLOR_STATE_NAME = "Color";

        [UiProperty]
        public string Color
        {
            get => "#" + ((string)this.States[COLOR_STATE_NAME]).TrimStart('#')[..6]; // ensure there is # in front and 6 digits
            set => this.SetStateAndSend(COLOR_STATE_NAME, value);
        }

        [UiProperty(true, "(topic, json path)")]
        public (string topic, string jsonPath) ColorGetMqttTopic
        {
            get => this.MqttGetTopics[COLOR_STATE_NAME];
            set => this.SetGetTopic(COLOR_STATE_NAME, value);
        }

        [UiProperty(true, "(topic, json path)")]
        public (string topic, string jsonPath) ColorSetMqttTopic
        {
            get => this.MqttSetTopics[COLOR_STATE_NAME];
            set => this.MqttSetTopics[COLOR_STATE_NAME] = value;
        }


        public LightMqttDriver()
        {
            this.States.Add(COLOR_STATE_NAME, "#ffffff");
            this.MqttGetTopics.Add(COLOR_STATE_NAME, ("", ""));
            this.MqttSetTopics.Add(COLOR_STATE_NAME, ("", ""));
        }
    }
}
