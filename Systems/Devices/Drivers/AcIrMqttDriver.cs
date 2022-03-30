using System;

using MyHome.Utils;

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;

using NLog;

namespace MyHome.Systems.Devices.Drivers
{
    public class AcIrMqttDriver : BaseDriver
    {
        private static readonly ILogger logger = LogManager.GetCurrentClassLogger();

        private enum STATE_NAMES
        {
            Power,
            Mode,
            FanSpeed,
            SwingV,
            SwingH,
            Temp,
            Quiet,
            Turbo,
            Econo,
            Light,
            Filter,
            Clean,
            Beep,
            Sleep
        }

        public enum AcMode
        {
            Off,
            Auto,
            Cool,
            Heat,
            Dry,
            Fan
        }

        public enum AcFanSpeed
        {
            Auto,
            Min,
            Low,
            Medium,
            High,
            Max,
        }

        public enum AcSwingV
        {
            Auto,
            Off,
            Min,
            Low,
            Middle,
            High,
            Highest
        }

        public enum AcSwingH
        {
            Auto,
            Off,
            LeftMax,
            Left,
            Middle,
            Right,
            RightMax,
            Wide
        }


        [UiProperty]
        public bool Power
        {
            get => (bool)this.State[STATE_NAMES.Power.ToString()];
            set => this.SetState(STATE_NAMES.Power.ToString(), value, this.SendStateDebounced);
        }

        [UiProperty]
        public AcMode Mode
        {
            get => Enum.Parse<AcMode>(this.State[STATE_NAMES.Mode.ToString()].ToString());
            set => this.SetState(STATE_NAMES.Mode.ToString(), value, this.SendStateDebounced);
        }

        [UiProperty]
        public AcFanSpeed FanSpeed
        {
            get => Enum.Parse<AcFanSpeed>(this.State[STATE_NAMES.FanSpeed.ToString()].ToString());
            set => this.SetState(STATE_NAMES.FanSpeed.ToString(), value, this.SendStateDebounced);
        }

        [UiProperty]
        public AcSwingV SwingV
        {
            get => Enum.Parse<AcSwingV>(this.State[STATE_NAMES.SwingV.ToString()].ToString());
            set => this.SetState(STATE_NAMES.SwingV.ToString(), value, this.SendStateDebounced);
        }

        [UiProperty]
        public AcSwingH SwingH
        {
            get => Enum.Parse<AcSwingH>(this.State[STATE_NAMES.SwingH.ToString()].ToString());
            set => this.SetState(STATE_NAMES.SwingH.ToString(), value, this.SendStateDebounced);
        }

        [UiProperty]
        public double Temperature
        {
            get => (double)this.State[STATE_NAMES.Temp.ToString()];
            set => this.SetState(STATE_NAMES.Temp.ToString(), value, this.SendStateDebounced);
        }

        [UiProperty]
        public bool Quiet
        {
            get => (bool)this.State[STATE_NAMES.Quiet.ToString()];
            set => this.SetState(STATE_NAMES.Quiet.ToString(), value, this.SendStateDebounced);
        }

        [UiProperty]
        public bool Turbo
        {
            get => (bool)this.State[STATE_NAMES.Turbo.ToString()];
            set => this.SetState(STATE_NAMES.Turbo.ToString(), value, this.SendStateDebounced);
        }

        [UiProperty]
        public bool Econo
        {
            get => (bool)this.State[STATE_NAMES.Econo.ToString()];
            set => this.SetState(STATE_NAMES.Econo.ToString(), value, this.SendStateDebounced);
        }

        [UiProperty]
        public bool Light
        {
            get => (bool)this.State[STATE_NAMES.Light.ToString()];
            set => this.SetState(STATE_NAMES.Light.ToString(), value, this.SendStateDebounced);
        }

        [UiProperty]
        public bool Filter
        {
            get => (bool)this.State[STATE_NAMES.Filter.ToString()];
            set => this.SetState(STATE_NAMES.Filter.ToString(), value, this.SendStateDebounced);
        }

        [UiProperty]
        public bool Clean
        {
            get => (bool)this.State[STATE_NAMES.Clean.ToString()];
            set => this.SetState(STATE_NAMES.Clean.ToString(), value, this.SendStateDebounced);
        }

        [UiProperty]
        public bool Beep
        {
            get => (bool)this.State[STATE_NAMES.Beep.ToString()];
            set => this.SetState(STATE_NAMES.Beep.ToString(), value, this.SendStateDebounced);
        }

        [UiProperty]
        public int Sleep
        {
            get => (int)this.State[STATE_NAMES.Sleep.ToString()];
            set => this.SetState(STATE_NAMES.Sleep.ToString(), value, this.SendStateDebounced);
        }


        [UiProperty(true)]
        public string Vendor { get; set; }

        [UiProperty(true, "(topic, json path)")]
        public (string topic, string jsonPath) StateGetMqttTopic
        {
            get
            {
                var (topic, jsonPath) = this.MqttGetTopics[STATE_NAMES.Power.ToString()];
                return (topic, jsonPath.Replace("." + STATE_NAMES.Power, "").Replace(STATE_NAMES.Power.ToString(), ""));
            }
            set
            {
                foreach (var state in Enum.GetNames<STATE_NAMES>())
                {
                    var jPath = string.IsNullOrEmpty(value.jsonPath) ? state : value.jsonPath + "." + state;
                    this.SetGetTopic(state, (value.topic, jPath));
                }
            }
        }

        [UiProperty(true, "(topic, json path)")]
        public (string topic, string jsonPath) StateSetMqttTopic { get; set; }

        private readonly Action SendStateDebounced;



        public AcIrMqttDriver()
        {
            this.State.Add(STATE_NAMES.Power.ToString(), true);
            this.State.Add(STATE_NAMES.Mode.ToString(), AcMode.Heat);
            this.State.Add(STATE_NAMES.FanSpeed.ToString(), AcFanSpeed.Medium);
            this.State.Add(STATE_NAMES.SwingV.ToString(), AcSwingV.Auto);
            this.State.Add(STATE_NAMES.SwingH.ToString(), AcSwingH.Auto);
            this.State.Add(STATE_NAMES.Temp.ToString(), 22.0);
            this.State.Add(STATE_NAMES.Quiet.ToString(), false);
            this.State.Add(STATE_NAMES.Turbo.ToString(), false);
            this.State.Add(STATE_NAMES.Econo.ToString(), false);
            this.State.Add(STATE_NAMES.Light.ToString(), false);
            this.State.Add(STATE_NAMES.Filter.ToString(), false);
            this.State.Add(STATE_NAMES.Clean.ToString(), false);
            this.State.Add(STATE_NAMES.Beep.ToString(), false);
            this.State.Add(STATE_NAMES.Sleep.ToString(), -1);
            foreach (var state in Enum.GetNames<STATE_NAMES>())
                this.MqttGetTopics.Add(state, ("", ""));
            this.StateSetMqttTopic = ("", "");

            this.SendStateDebounced = new Action(this.SendState).Debounce();
        }

        protected override bool AcceptPayload(string payload)
        {
            var jsonToken = JToken.Parse(payload).SelectToken(this.StateGetMqttTopic.jsonPath + "." + nameof(this.Vendor));
            return jsonToken?.ToString() == this.Vendor;
        }


        private void SendState()
        {
            var serializer = JsonSerializer.CreateDefault();
            serializer.Converters.Add(new StringEnumConverter());
            var value = new JObject();
            foreach (var state in Enum.GetNames<STATE_NAMES>())
                value[state] = JToken.FromObject(this.State[state], serializer);
            value[nameof(this.Vendor)] = this.Vendor;

            if (!string.IsNullOrEmpty(this.StateSetMqttTopic.jsonPath))
            {
                var json = new JObject
                {
                    [this.StateSetMqttTopic.jsonPath] = value
                };
                value = json;
            }

            if (MyHome.Instance.MqttClient.IsConnected)
            {
                logger.Info($"Send state to {this.Name} ({this.Room.Name}): {value}");
                MyHome.Instance.MqttClient.Publish(this.StateSetMqttTopic.topic, value.ToString());
            }
        }
    }
}
