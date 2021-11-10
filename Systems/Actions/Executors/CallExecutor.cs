using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

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
                var methodName = this.Function.Split(new char[] { ' ', '.' })[1]; // remove object type and function arguments
                if (this.Device != null)
                    CallMethod(this.Device, methodName, this.Arguments);
                else
                    CallMethod(this.Room, methodName, this.Arguments);
            }
        }

        public IEnumerable<string> GetFunctions() // Function selector
        {
            var functions = MyHome.Instance.Rooms.SelectMany(r => GetFunctions(r));
            functions = functions.Union(
                MyHome.Instance.Rooms.SelectMany(r =>
                    r.Devices.SelectMany(d => GetFunctions(d))));
            return functions.Distinct();
        }

        private static IEnumerable<string> GetFunctions(object obj)
        {
            return obj.GetType()
                .GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Where(mi => !mi.IsSpecialName &&
                    mi.DeclaringType != typeof(object) &&
                    !mi.DeclaringType.IsAbstract &&
                    !mi.IsVirtual)
                .Select(mi => $"{mi.DeclaringType.Name}.{mi.Name} ({string.Join(",", mi.GetParameters().Select(p => p.Name))})");
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
