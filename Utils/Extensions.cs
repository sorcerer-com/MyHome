using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json.Serialization;

using NLog;

namespace MyHome.Utils
{
    public static class Extensions
    {
        private static readonly ILogger logger = LogManager.GetCurrentClassLogger();

        public static IEnumerable<Type> GetSubClasses(this Type type)
        {
            return Assembly
                .GetAssembly(type)
                .GetTypes()
                .Where(t => t.IsClass && !t.IsAbstract && t.IsSubclassOf(type));
        }

        public static IEnumerable<PropertyInfo> GetJsonProperties(this Type type)
        {
            return type
                .GetProperties()
                .Where(pi => pi.GetCustomAttribute<JsonIgnoreAttribute>() == null && pi.CanRead && pi.CanWrite);
        }

        public static Dictionary<string, object> GetJsonValues(this object obj)
        {
            return obj.GetType()
                .GetJsonProperties()
                .ToDictionary(pi => pi.Name, pi => pi.GetValue(obj));
        }

        public static void SetJsonValues(this object obj, Dictionary<string, object> values)
        {
            var propertyInfos = obj.GetType().GetJsonProperties();
            foreach (var pi in propertyInfos)
            {
                if (values.ContainsKey(pi.Name) && pi.CanWrite)
                {
                    try
                    {
                        logger.Debug($"Set value '{values[pi.Name]}' to property '{pi.Name}' of object of type '{obj.GetType().Name}'");
                        pi.SetValue(obj, values[pi.Name]);
                    }
                    catch (Exception e)
                    {
                        logger.Warn(e, $"Failed to set value '{values[pi.Name]}' to property '{pi.Name}' of object of type '{obj.GetType().Name}'");
                    }
                }
            }
        }
    }
}
