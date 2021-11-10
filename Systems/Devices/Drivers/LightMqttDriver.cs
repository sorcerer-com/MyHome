using MyHome.Utils;

using Newtonsoft.Json.Linq;

namespace MyHome.Systems.Devices.Drivers
{
    public class LightMqttDriver : SwitchMqttDriver
    {
        private const string COLOR_STATE_NAME = "Color";

        [UiProperty]
        public string Color
        {
            get => "#" + ((string)this.State[COLOR_STATE_NAME]).TrimStart('#')[..6]; // ensure there is # in front and 6 digits
            set
            {
                this.State[COLOR_STATE_NAME] = value;
                this.SendColorState();
            }
        }

        [UiProperty(true, "(topic, json path)")]
        public (string topic, string jsonPath) ColorGetMqttTopic
        {
            get => this.MqttGetTopics[COLOR_STATE_NAME];
            set
            {
                if (MyHome.Instance.MqttClient.IsConnected)
                {
                    MyHome.Instance.MqttClient.Unsubscribe(this.MqttGetTopics[COLOR_STATE_NAME].topic);
                    MyHome.Instance.MqttClient.Subscribe(value.topic);
                }
                this.MqttGetTopics[COLOR_STATE_NAME] = value;
            }
        }

        [UiProperty(true, "(topic, json path)")]
        public (string topic, string jsonPath) ColorSetMqttTopic
        {
            get => this.MqttSetTopics[COLOR_STATE_NAME];
            set => this.MqttSetTopics[COLOR_STATE_NAME] = value;
        }


        public LightMqttDriver()
        {
            this.State.Add(COLOR_STATE_NAME, "#ffffff");
            this.MqttGetTopics.Add(COLOR_STATE_NAME, ("", ""));
            this.MqttSetTopics.Add(COLOR_STATE_NAME, ("", ""));
        }


        private void SendColorState()
        {
            var value = this.Color;
            if (!string.IsNullOrEmpty(this.ColorSetMqttTopic.jsonPath))
            {
                var json = new JObject
                {
                    [this.ColorSetMqttTopic.jsonPath] = this.Color
                };
                value = json.ToString();
            }

            if (MyHome.Instance.MqttClient.IsConnected)
                MyHome.Instance.MqttClient.Publish(this.ColorSetMqttTopic.topic, value);
        }
    }
}
