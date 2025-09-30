using System.Collections.Generic;
using System.Linq;

using MyHome.Utils;

using Newtonsoft.Json;

namespace MyHome.Systems.Actions
{
    public class SensorTriggeredAction : EventTriggeredAction
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0051:Remove unused private members")]
        private new GlobalEventTypes EventType { get; set; }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0051:Remove unused private members")]
        private new string DeviceName { get; set; }


        [JsonIgnore]
        [UiProperty(true, selector: "GetSensors")]
        public string SensorName
        {
            get => this.device != null ? $"{this.device.Room.Name}.{this.device.Name}" : "";
            set
            {
                var split = value?.Split('.');
                if (split?.Length == 2)
                    this.device = MyHome.Instance.Rooms.Find(r => r.Name == split[0])?.Sensors.FirstOrDefault(s => s.Name == split[1]);
            }
        }

        [UiProperty(true, selector: "GetSensorsSubnames")]
        public string SensorSubname { get; set; }

        [UiProperty(true)]
        public Condition Condition { get; set; }

        [UiProperty(true)]
        public double ConditionValue { get; set; }


        public SensorTriggeredAction()
        {
        }

        public override void Setup()
        {
            base.EventType = GlobalEventTypes.SensorDataAdded;
            base.Setup();
        }


        protected override bool IsTriggered(object sender, GlobalEventArgs e)
        {
            if (!base.IsTriggered(sender, e))
                return false;

            if (e.Data is not Dictionary<string, double> values)
                return false;

            if (!values.TryGetValue(this.SensorSubname, out var value))
                return false;

            return Utils.Utils.CheckCondition(value, this.ConditionValue, this.Condition);
        }
    }
}
