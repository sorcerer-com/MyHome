using System;

using MyHome.Utils;

using NLog;

namespace MyHome.Systems.Actions.Executors
{
    public class SetExecutor : BaseExecutor
    {
        private static readonly ILogger logger = LogManager.GetCurrentClassLogger();

        [UiProperty(true, selector: "GetProperties")]
        public string Property { get; set; }

        [UiProperty(true)]
        public string Value { get; set; }


        public SetExecutor()
        {
        }


        public override void Execute()
        {
            if (!string.IsNullOrEmpty(this.Property))
            {
                var propertyName = this.Property.Split('.')[1]; // remove object type
                if (this.Device != null)
                    SetProperty(this.Device, propertyName, this.Value);
                else
                    SetProperty(this.Room, propertyName, this.Value);
            }
        }


        protected static void SetProperty(object obj, string property, string value)
        {
            if (obj == null)
            {
                logger.Error($"Try to set value on invalid object");
                return;
            }

            var prop = obj.GetType().GetProperty(property);
            if (prop == null)
            {
                logger.Error($"Try to set value on invalid property: {property}");
                return;
            }

            try
            {
                prop.SetValue(obj, Utils.Utils.ParseValue(value, prop.PropertyType));
            }
            catch (Exception e)
            {
                logger.Error("Failed to set property value");
                logger.Debug(e);
            }
        }
    }
}
