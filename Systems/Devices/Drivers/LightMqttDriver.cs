using MyHome.Models;
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
                if (this.MqttClient?.IsConnected == true)
                {
                    this.MqttClient.Unsubscribe(this.MqttGetTopics[COLOR_STATE_NAME].topic);
                    this.MqttClient.Subscribe(value.topic);
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


        private LightMqttDriver() : this(null, null, null) { } // for json deserialization

        public LightMqttDriver(DevicesSystem owner, string name, Room room) : base(owner, name, room)
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

            this.MqttClient?.Publish(this.ColorSetMqttTopic.topic, value);
        }
    }
}
