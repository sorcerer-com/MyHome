using System.Collections.Generic;

using MyHome.Utils;

namespace MyHome.Systems.Actions
{
    public class SensorTriggeredAction : EventTriggeredAction
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0051:Remove unused private members")]
        private new GlobalEventTypes EventType { get; set; }

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

            if (!values.ContainsKey(this.SensorSubname))
                return false;

            return Utils.Utils.CheckCondition(values[this.SensorSubname], this.ConditionValue, this.Condition);
        }
    }
}
