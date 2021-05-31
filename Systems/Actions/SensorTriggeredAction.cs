using System.Collections.Generic;

using MyHome.Models;
using MyHome.Utils;

namespace MyHome.Systems.Actions
{
    public class SensorTriggeredAction : EventTriggeredAction
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0051:Remove unused private member")]
        private new string TriggerEvent { get; set; }

        [UiProperty(true)]
        public string TriggerSensorSubname { get; set; }

        [UiProperty(true)]
        public Condition TriggerCondition { get; set; }

        [UiProperty(true)]
        public double TriggerConditionValue { get; set; }


        private SensorTriggeredAction() : this(null, null, null, Condition.Equal, 0, null, null) { }  // for json deserialization

        public SensorTriggeredAction(ActionsSystem owner, Room triggerRoom,
            string triggerSensorSubname, Condition triggerCondition, double triggerConditionValue,
            Room room, string action) :
            base(owner, triggerRoom, "SensorDataAdded", room, action)
        {
            this.TriggerSensorSubname = triggerSensorSubname;
            this.TriggerCondition = triggerCondition;
            this.TriggerConditionValue = triggerConditionValue;
        }


        protected override bool IsTriggered(object sender, GlobalEvent.GlobalEventArgs e)
        {
            if (!base.IsTriggered(sender, e))
                return false;

            if (e.Data is not Dictionary<string, double> values)
                return false;

            if (!values.ContainsKey(this.TriggerSensorSubname))
                return false;

            return Utils.Utils.CheckCondition(values[this.TriggerSensorSubname], this.TriggerConditionValue, this.TriggerCondition);
        }
    }
}
