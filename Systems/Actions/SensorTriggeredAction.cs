using System.Collections.Generic;
using System.Linq;

using MyHome.Models;
using MyHome.Systems.Actions.Executors;
using MyHome.Utils;

namespace MyHome.Systems.Actions
{
    public class SensorTriggeredAction : EventTriggeredAction
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0051:Remove unused private members")]
        private new GlobalEventTypes EventType { get; set; }

        [UiProperty(true, selector: "GetSensorSubname")]
        public string SensorSubname { get; set; }

        [UiProperty(true)]
        public Condition Condition { get; set; }

        [UiProperty(true)]
        public double ConditionValue { get; set; }


        private SensorTriggeredAction() : this(null, null, true, null, null, null, Condition.Equal, 0) { }  // for json deserialization

        public SensorTriggeredAction(ActionsSystem owner, string name, bool isEnabled, BaseExecutor executor,
            Room eventRoom, string sensorSubname, Condition condition, double conditionValue) :
            base(owner, name, isEnabled, executor, eventRoom, GlobalEventTypes.SensorDataAdded)
        {
            this.SensorSubname = sensorSubname;
            this.Condition = condition;
            this.ConditionValue = conditionValue;
        }


        public IEnumerable<string> GetSensorSubname() // EventRoomName selector
        {
            return this.Owner.Owner.Rooms.SelectMany(r => r.SensorsValues.Keys).Distinct().OrderBy(s => s);
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
