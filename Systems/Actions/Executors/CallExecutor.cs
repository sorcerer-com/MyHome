using System;
using System.Linq;

using MyHome.Utils;

using NLog;

namespace MyHome.Systems.Actions.Executors
{
    public class CallExecutor : BaseExecutor
    {
        private static readonly ILogger logger = LogManager.GetCurrentClassLogger();

        [UiProperty(true, selector: "GetFunctions")]
        public string Function { get; set; }

        [UiProperty(true)]
        public string Arguments { get; set; }


        public CallExecutor()
        {
        }


        public override void Execute()
        {
            if (!string.IsNullOrEmpty(this.Function))
            {
                var methodName = this.Function.Split('.')[1]; // remove object type
                if (this.Device != null)
                    CallMethod(this.Device, methodName, this.Arguments);
                else
                    CallMethod(this.Room, methodName, this.Arguments);
            }
        }


        protected static void CallMethod(object obj, string methodName, string arguments = null)
        {
            if (obj == null)
            {
                logger.Error($"Try to call method on invalid object");
                return;
            }

            var method = obj.GetType().GetMethod(methodName);
            if (method == null)
            {
                logger.Error($"Try to call invalid method: {methodName}");
                return;
            }

            try
            {
                var methodParams = method.GetParameters();
                var args = Array.Empty<object>();
                if (arguments != null)
                {
                    args = arguments.Split(",").Select((a, i) =>
                        Utils.Utils.ParseValue(a.Trim(), methodParams[i].ParameterType)).ToArray();
                }
                method.Invoke(obj, args);
            }
            catch (Exception e)
            {
                logger.Error("Failed to call method");
                logger.Debug(e);
            }
        }
    }
}
