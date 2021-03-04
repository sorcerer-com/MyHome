using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace MyHome.Utils
{
    public static class Extensions
    {
        public static IEnumerable<Type> GetSubClasses(this Type type)
        {
            return Assembly
                .GetAssembly(type)
                .GetTypes()
                .Where(t => t.IsClass && !t.IsAbstract && t.IsSubclassOf(type));
        }

        public static void RunForEach<T>(this IEnumerable<T> source, Action<T> action)
        {
            Task.WaitAll(source.Select(i => Task.Run(() => action.Invoke(i))).ToArray());
        }

        public static object ToUiObject(this object obj)
        {
            if (obj == null)
                return null;

            var type = obj.GetType();
            if (type.IsPrimitive || type == typeof(string) || type == typeof(DateTime))
            {
                return obj;
            }
            else if (type.GetInterface(nameof(IDictionary)) != null)
            {
                var dict = new Dictionary<object, object>();
                var value = (IDictionary)obj;
                foreach (var key in value.Keys)
                    dict.Add(key, value[key].ToUiObject());
                return dict;
            }
            else if (type.GetInterface(nameof(IEnumerable)) != null)
            {

                var list = new List<object>();
                var value = (IEnumerable)obj;
                foreach (var item in value)
                    list.Add(item.ToUiObject());
                return list;
            }
            else
            {
                // TODO: if it's read-only?
                var result = new Dictionary<string, object>();
                var pis = obj.GetType().GetProperties().Where(pi => pi.GetCustomAttribute<UiProperty>() != null);
                foreach (var pi in pis)
                    result.Add(pi.Name, pi.GetValue(obj).ToUiObject());
                return result;
            }
        }
    }
}
