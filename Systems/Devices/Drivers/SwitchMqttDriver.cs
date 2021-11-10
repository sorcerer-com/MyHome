﻿using MyHome.Utils;

using Newtonsoft.Json.Linq;

namespace MyHome.Systems.Devices.Drivers
{
    public class SwitchMqttDriver : BaseDriver
    {
        private const string SWITCH_STATE_NAME = "On";

        [UiProperty]
        public bool IsOn
        {
            get => (bool)this.State[SWITCH_STATE_NAME];
            set
            {
                this.State[SWITCH_STATE_NAME] = value;
                this.SendSwitchState();
            }
        }

        [UiProperty(true, "(topic, json path)")]
        public (string topic, string jsonPath) IsOnGetMqttTopic
        {
            get => this.MqttGetTopics[SWITCH_STATE_NAME];
            set
            {
                if (MyHome.Instance.MqttClient.IsConnected)
                {
                    MyHome.Instance.MqttClient.Unsubscribe(this.MqttGetTopics[SWITCH_STATE_NAME].topic);
                    MyHome.Instance.MqttClient.Subscribe(value.topic);
                }
                this.MqttGetTopics[SWITCH_STATE_NAME] = value;
            }
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
            this.State.Add(SWITCH_STATE_NAME, false);
            this.MqttGetTopics.Add(SWITCH_STATE_NAME, ("", ""));
            this.MqttSetTopics.Add(SWITCH_STATE_NAME, ("", ""));
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
                MyHome.Instance.MqttClient.Publish(this.IsOnSetMqttTopic.topic, value);
        }
    }
}
