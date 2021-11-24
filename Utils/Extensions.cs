using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

using MyHome.Models;

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
            else if (type == typeof(TimeSpan))
            {
                var value = (TimeSpan)obj;
                return value.ToString();
            }
            else if (type.IsEnum)
            {
                return type.Name + "." + obj.ToString();
            }
            else if (type.GetInterface(nameof(ITuple)) != null)
            {
                var value = (ITuple)obj;
                return value.ToString();
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
                var subtypes = new Dictionary<string, string>();
                var hints = new Dictionary<string, string>();

                var pis = obj.GetType().GetProperties();
                foreach (var pi in pis)
                {
                    var uiPropertyAttr = pi.GetCustomAttribute<UiPropertyAttribute>();
                    if (uiPropertyAttr == null)
                        continue;

                    if (settingsOnly && !uiPropertyAttr.Setting)
                        continue;

                    result[pi.Name] = pi.GetValue(obj).ToUiObject(settingsOnly);
                    subtypes[pi.Name] = pi.PropertyType.ToUiType();
                    hints[pi.Name] = uiPropertyAttr.Hint;

                    if (!string.IsNullOrEmpty(uiPropertyAttr.Selector))
                    {
                        var mi = typeof(Selectors).GetMethod(uiPropertyAttr.Selector);
                        if (mi == null)
                            mi = obj.GetType().GetMethod(uiPropertyAttr.Selector);
                        if (mi != null)
                        {
                            var values = (IEnumerable<string>)mi.Invoke(obj, null);
                            subtypes[pi.Name] = $"Select: {string.Join(", ", values)}";
                        }
                    }
                }
                result.Add("$type", obj.GetType().ToString());
                if (settingsOnly)
                {
                    result.Add("$subtypes", subtypes);
                    result.Add("$hints", hints);
                }
                return result;
            }
        }

        public static string ToUiType(this Type type)
        {
            var name = type.Name
                .Replace("`1", "")
                .Replace("`2", "")
                .Replace("IEnumerable", "List")
                .Replace("ObservableCollection", "List");

            if (type.IsGenericType)
            {
                var genericTypes = type.GenericTypeArguments.Select(t => t.ToUiType());
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
            var processingDicts = new List<string>();

            var type = obj.GetType();
            foreach (var item in form)
            {
                var propName = item.Key.Replace("[]", "");
                if (propName.Contains('[')) // dictionary
                    propName = propName[..item.Key.IndexOf('[')];
                var prop = type.GetProperty(propName);
                if (prop == null)
                    continue;

                if (item.Key.EndsWith("[]")) // list
                {
                    var list = prop.GetValue(obj) as IList;
                    // workaround since Clear() doesn't work well with observable collections
                    while (list.Count > 0)
                        list.RemoveAt(0);
                    var values = item.Value.Select(v => Utils.ParseValue(v, prop.PropertyType.GenericTypeArguments[0]));
                    foreach (var value in values)
                        list.Add(value);
                }
                else if (item.Key.IndexOf('[') < item.Key.IndexOf(']'))
                {
                    if (prop.PropertyType.IsGenericType) // dictionary
                    {
                        var dict = prop.GetValue(obj) as IDictionary;
                        if (!processingDicts.Contains(propName))
                        {
                            dict.Clear();
                            processingDicts.Add(propName);
                        }
                        var key = Utils.ParseValue(item.Key[(item.Key.IndexOf('[') + 1)..item.Key.IndexOf(']')], prop.PropertyType.GenericTypeArguments[0]);
                        dict[key] = Utils.ParseValue(item.Value, prop.PropertyType.GenericTypeArguments[1]);
                    }
                    else // object
                    {
                        var obj2 = prop.GetValue(obj);
                        var property = item.Key[(item.Key.IndexOf('[') + 1)..item.Key.IndexOf(']')];
                        var prop2 = obj2.GetType().GetProperty(property);
                        if (prop2 == null)
                            continue;
                        if (prop2.CanWrite)
                            prop2.SetValue(obj2, Utils.ParseValue(item.Value, prop2.PropertyType));
                    }
                }
                else if (prop.CanWrite)
                {
                    prop.SetValue(obj, Utils.ParseValue(item.Value, prop.PropertyType));
                }
            }
        }
    }
}
