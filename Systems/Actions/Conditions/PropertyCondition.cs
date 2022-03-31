using System;
using System.Linq;

using MyHome.Models;
using MyHome.Systems.Devices;
using MyHome.Utils;

using NLog;

namespace MyHome.Systems.Actions.Conditions
{
    public class PropertyCondition : BaseCondition
    {
        private static readonly ILogger logger = LogManager.GetCurrentClassLogger();

        protected Room Room
        {
            get
            {
                var split = this.Target.Split(".");
                return MyHome.Instance.Rooms.FirstOrDefault(r => r.Name == split[0]);
            }
        }

        protected Device Device
        {
            get
            {
                var split = this.Target.Split(".");
                if (split.Length > 1)
                    return this.Room?.Devices.FirstOrDefault(d => d.Name == split[1]);
                return null;
            }
        }

        [UiProperty(true, selector: "GetTarget")]
        public string Target { get; set; }

        [UiProperty(true, selector: "GetProperties")]
        public string Property { get; set; }

        [UiProperty(true)]
        public Condition Condition { get; set; }

        [UiProperty(true)]
        public string ConditionValue { get; set; }


        public PropertyCondition()
        {
        }


        public override bool Check()
        {
            if (!string.IsNullOrEmpty(this.Property))
            {
                var propertyName = this.Property.Split('.')[1]; // remove object type
                object value;
                if (this.Device != null)
                    value = GetPropertyValue(this.Device, propertyName);
                else
                    value = GetPropertyValue(this.Room, propertyName);

                if (value is IComparable)
                {
                    var conditionValue = Utils.Utils.ParseValue(this.ConditionValue, value.GetType());
                    return Utils.Utils.CheckCondition(value as IComparable, conditionValue, this.Condition);
                }
            }
            return false;
        }


        protected static object GetPropertyValue(object obj, string property)
        {
            if (obj == null)
            {
                logger.Error($"Try to set value on invalid object");
                return null;
            }

            var prop = obj.GetType().GetProperty(property);
            if (prop == null)
            {
                logger.Error($"Try to set value on invalid property: {property}");
                return null;
            }

            try
            {
                return prop.GetValue(obj, null);
            }
            catch (Exception e)
            {
                logger.Error("Failed to set property value");
                logger.Debug(e);
            }
            return null;
        }
    }
}