using System;

using MyHome.Systems.Devices.Drivers.Types;
using MyHome.Utils;

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;

using static MyHome.Systems.Devices.Drivers.Types.IAcDriver;

namespace MyHome.Systems.Devices.Drivers.Mqtt
{
    public class AcIrMqttDriver : MqttDriver, IAcDriver
    {
        private const string AC_STATE_NAME = "AC";

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

        [UiProperty]
        public bool Power
        {
            get => (bool)this.States[STATE_NAMES.Power.ToString()];
            set => this.SetState(STATE_NAMES.Power.ToString(), value, this.SendStateDebounced);
        }

        [UiProperty]
        public AcMode Mode
        {
            get => Enum.Parse<AcMode>(this.States[STATE_NAMES.Mode.ToString()].ToString());
            set => this.SetState(STATE_NAMES.Mode.ToString(), value, this.SendStateDebounced);
        }

        [UiProperty]
        public AcFanSpeed FanSpeed
        {
            get => Enum.Parse<AcFanSpeed>(this.States[STATE_NAMES.FanSpeed.ToString()].ToString());
            set => this.SetState(STATE_NAMES.FanSpeed.ToString(), value, this.SendStateDebounced);
        }

        [UiProperty]
        public AcSwingV SwingV
        {
            get => Enum.Parse<AcSwingV>(this.States[STATE_NAMES.SwingV.ToString()].ToString());
            set => this.SetState(STATE_NAMES.SwingV.ToString(), value, this.SendStateDebounced);
        }

        [UiProperty]
        public AcSwingH SwingH
        {
            get => Enum.Parse<AcSwingH>(this.States[STATE_NAMES.SwingH.ToString()].ToString());
            set => this.SetState(STATE_NAMES.SwingH.ToString(), value, this.SendStateDebounced);
        }

        [UiProperty]
        public double Temperature
        {
            get => (double)this.States[STATE_NAMES.Temp.ToString()];
            set => this.SetState(STATE_NAMES.Temp.ToString(), value, this.SendStateDebounced);
        }

        [UiProperty]
        public bool Quiet
        {
            get => (bool)this.States[STATE_NAMES.Quiet.ToString()];
            set => this.SetState(STATE_NAMES.Quiet.ToString(), value, this.SendStateDebounced);
        }

        [UiProperty]
        public bool Turbo
        {
            get => (bool)this.States[STATE_NAMES.Turbo.ToString()];
            set => this.SetState(STATE_NAMES.Turbo.ToString(), value, this.SendStateDebounced);
        }

        [UiProperty]
        public bool Econo
        {
            get => (bool)this.States[STATE_NAMES.Econo.ToString()];
            set => this.SetState(STATE_NAMES.Econo.ToString(), value, this.SendStateDebounced);
        }

        [UiProperty]
        public bool Light
        {
            get => (bool)this.States[STATE_NAMES.Light.ToString()];
            set => this.SetState(STATE_NAMES.Light.ToString(), value, this.SendStateDebounced);
        }

        [UiProperty]
        public bool Filter
        {
            get => (bool)this.States[STATE_NAMES.Filter.ToString()];
            set => this.SetState(STATE_NAMES.Filter.ToString(), value, this.SendStateDebounced);
        }

        [UiProperty]
        public bool Clean
        {
            get => (bool)this.States[STATE_NAMES.Clean.ToString()];
            set => this.SetState(STATE_NAMES.Clean.ToString(), value, this.SendStateDebounced);
        }

        [UiProperty]
        public bool Beep
        {
            get => (bool)this.States[STATE_NAMES.Beep.ToString()];
            set => this.SetState(STATE_NAMES.Beep.ToString(), value, this.SendStateDebounced);
        }

        [UiProperty]
        public int Sleep
        {
            get => (int)this.States[STATE_NAMES.Sleep.ToString()];
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
        public (string topic, string jsonPath) StateSetMqttTopic
        {
            get => this.MqttSetTopics[AC_STATE_NAME];
            set => this.MqttSetTopics[AC_STATE_NAME] = value;
        }

        private readonly Action SendStateDebounced;



        public AcIrMqttDriver()
        {
            this.States.Add(STATE_NAMES.Power.ToString(), true);
            this.States.Add(STATE_NAMES.Mode.ToString(), AcMode.Heat);
            this.States.Add(STATE_NAMES.FanSpeed.ToString(), AcFanSpeed.Medium);
            this.States.Add(STATE_NAMES.SwingV.ToString(), AcSwingV.Auto);
            this.States.Add(STATE_NAMES.SwingH.ToString(), AcSwingH.Auto);
            this.States.Add(STATE_NAMES.Temp.ToString(), 22.0);
            this.States.Add(STATE_NAMES.Quiet.ToString(), false);
            this.States.Add(STATE_NAMES.Turbo.ToString(), false);
            this.States.Add(STATE_NAMES.Econo.ToString(), false);
            this.States.Add(STATE_NAMES.Light.ToString(), false);
            this.States.Add(STATE_NAMES.Filter.ToString(), false);
            this.States.Add(STATE_NAMES.Clean.ToString(), false);
            this.States.Add(STATE_NAMES.Beep.ToString(), false);
            this.States.Add(STATE_NAMES.Sleep.ToString(), -1);
            foreach (var state in Enum.GetNames<STATE_NAMES>())
                this.MqttGetTopics.Add(state, ("", ""));
            this.MqttSetTopics[AC_STATE_NAME] = ("", "");

            this.SendStateDebounced = new Action(this.SendAcState).Debounce();
        }

        protected override bool AcceptPayload(string payload)
        {
            var jsonToken = JToken.Parse(payload).SelectToken(this.StateGetMqttTopic.jsonPath + "." + nameof(this.Vendor));
            return jsonToken?.ToString() == this.Vendor;
        }


        private void SendAcState()
        {
            var serializer = JsonSerializer.CreateDefault();
            serializer.Converters.Add(new StringEnumConverter());
            var value = new JObject();
            foreach (var state in Enum.GetNames<STATE_NAMES>())
                value[state] = JToken.FromObject(this.States[state], serializer);
            value[nameof(this.Vendor)] = this.Vendor;

            this.SendState(AC_STATE_NAME, value.ToString());
        }
    }
}
