using System.Collections.Generic;

using MyHome.Models;

namespace MyHome.Systems.Actions
{
    public class SensorTriggeredAction : EventTriggeredAction
    {
        public string TriggerSensorSubname { get; set; }

        public Condition TriggerCondition { get; set; } // TODO: convert in UI (check Utils)

        public double TriggerConditionValue { get; set; }

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
