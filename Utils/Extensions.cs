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

        public static object ToUiObject(this object obj)
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
                var result = new Dictionary<string, object>();
                var subtypes = new Dictionary<string, Dictionary<string, object>>();

                var pis = obj.GetType().GetProperties();
                foreach (var pi in pis)
                {
                    var uiPropertyAttr = pi.GetCustomAttribute<UiPropertyAttribute>();
                    if (uiPropertyAttr == null)
                        continue;

                    result[pi.Name] = pi.GetValue(obj).ToUiObject();
                    subtypes[pi.Name] = pi.PropertyType.ToUiType();
                    subtypes[pi.Name]["setting"] = uiPropertyAttr.Setting;
                    subtypes[pi.Name]["hint"] = uiPropertyAttr.Hint;

                    if (!string.IsNullOrEmpty(uiPropertyAttr.Selector))
                    {
                        var mi = typeof(Selectors).GetMethod(uiPropertyAttr.Selector);
                        if (mi == null)
                            mi = obj.GetType().GetMethod(uiPropertyAttr.Selector);
                        if (mi != null)
                        {
                            var values = (IEnumerable<(string, string)>)mi.Invoke(obj, null);
                            subtypes[pi.Name]["type"] = "select";
                            subtypes[pi.Name]["select"] = values.ToDictionary(v => v.Item1, v => v.Item2);
                        }
                    }
                }
                result.Add("$type", obj.GetType().ToString());
                result.Add("$subtypes", subtypes);
                return result;
            }
        }

        private static Dictionary<string, object> ToUiType(this Type type)
        {
            var result = new Dictionary<string, object>();

            var name = type.Name
                .Replace("`1", "")
                .Replace("`2", "")
                .Replace("IEnumerable", "List")
                .Replace("ObservableCollection", "List");
            result["type"] = name;

            if (type.IsGenericType)
            {
                var genericTypes = type.GenericTypeArguments.Select(t => t.ToUiType());
                result["genericTypes"] = genericTypes.ToList();
            }
            else if (type.IsEnum)
            {
                var values = type.GetFields().Where(f => f.IsLiteral).Select(f => f.Name);
                result["enums"] = values.ToList();
            }
            return result;
        }

        public static void SetObject(this Microsoft.AspNetCore.Http.IFormCollection form, object obj)
        {
            var processingDicts = new List<string>();

            var type = obj.GetType();
            foreach (var item in form)
            {
                if (item.Key.Contains("$subtypes"))
                    continue;

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

        public static Action Debounce(this Action func, int milliseconds = 100)
        {
            System.Threading.CancellationTokenSource cancelTokenSource = null;

            return () =>
            {
                cancelTokenSource?.Cancel();
                cancelTokenSource = new System.Threading.CancellationTokenSource();

                Task.Delay(milliseconds, cancelTokenSource.Token)
                    .ContinueWith(t =>
                    {
                        if (t.IsCompletedSuccessfully)
                        {
                            func();
                        }
                    }, TaskScheduler.Default);
            };
        }
    }
}
