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

        public static object ToUiObject(this object obj, bool settingsOnly = false)
        {
            if (obj == null)
                return null;

            var type = obj.GetType();
            if (type.IsPrimitive || type == typeof(string) || type == typeof(DateTime))
            {
                return obj;
            }
            else if (type.IsEnum)
            {
                return type.Name + "." + obj.ToString();
            }
            else if (type.GetInterface(nameof(IDictionary)) != null)
            {
                var dict = new Dictionary<object, object>();
                var value = (IDictionary)obj;
                foreach (var key in value.Keys)
                    dict.Add(key, value[key].ToUiObject(settingsOnly));
                return dict;
            }
            else if (type.GetInterface(nameof(IEnumerable)) != null)
            {

                var list = new List<object>();
                var value = (IEnumerable)obj;
                foreach (var item in value)
                    list.Add(item.ToUiObject(settingsOnly));
                return list;
            }
            else
            {
                var result = new Dictionary<string, object>();
                var metadata = new Dictionary<string, string>();
                var pis = obj.GetType().GetProperties();
                foreach (var pi in pis)
                {
                    var uiPropertyAttr = pi.GetCustomAttribute<UiProperty>();
                    if (uiPropertyAttr == null)
                        continue;

                    if (settingsOnly && uiPropertyAttr.Setting == false)
                        continue;

                    result.Add(pi.Name, pi.GetValue(obj).ToUiObject(settingsOnly));
                    metadata[pi.Name] = GetMetadata(pi.PropertyType);
                }
                result.Add("$type", obj.GetType().ToString());
                if (settingsOnly)
                    result.Add("$meta", metadata);
                return result;
            }
        }
        private static string GetMetadata(Type type)
        {
            var name = type.Name
                .Replace("`1", "")
                .Replace("`2", "")
                .Replace("IEnumerable", "List");

            if (type.IsGenericType)
            {
                var genericTypes = type.GenericTypeArguments.Select(t => GetMetadata(t));
                return $"{name} <{string.Join(", ", genericTypes)}>";
            }
            else if (type.IsEnum)
            {
                var values = type.GetFields().Where(f => f.IsLiteral).Select(f => type.Name + "." + f.Name);
                return $"Enum ({string.Join(", ", values)})";
            }
            return name;
        }

        public static void SetObject(this Microsoft.AspNetCore.Http.IFormCollection form, object obj)
        {
            var type = obj.GetType();
            foreach (var item in form)
            {
                var prop = type.GetProperty(item.Key.Replace("[]", ""));
                if (prop == null)
                    continue;

                if (prop.CanWrite && !item.Key.EndsWith("[]"))
                {
                    prop.SetValue(obj, Utils.ParseValue(item.Value, prop.PropertyType));
                }
                else if (item.Key.EndsWith("[]")) // list
                {
                    var values = item.Value.Select(v => Utils.ParseValue(v, prop.PropertyType.GenericTypeArguments[0]));
                    var list = prop.GetValue(obj) as IList;
                    list.Clear();
                    foreach (var value in values)
                        list.Add(value);
                }
            }
        }
    }
}
