using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

using MyHome.Models;

using Newtonsoft.Json.Linq;

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
                    if (!pi.CanWrite && !pi.PropertyType.IsGenericType) // exclude generic types (lists, dicts, etc.)
                        subtypes[pi.Name]["readonly"] = true;

                    if (!string.IsNullOrEmpty(uiPropertyAttr.Selector))
                    {
                        var mi = typeof(Selectors).GetMethod(uiPropertyAttr.Selector)
                            ?? obj.GetType().GetMethod(uiPropertyAttr.Selector);
                        if (mi != null)
                        {
                            var values = (IEnumerable<(string, string)>)mi.Invoke(obj, null);
                            subtypes[pi.Name]["type"] = "select";
                            subtypes[pi.Name]["select"] = values.ToDictionary(v => v.Item1, v => v.Item2);
                        }
                    }

                    if (uiPropertyAttr.Code)
                        subtypes[pi.Name]["type"] = "code";
                }
                result.Add("$type", obj.GetType().ToString());
                result.Add("$subtypes", subtypes);
                var baseTypes = new List<string> { obj.GetType().BaseType.ToString() };
                baseTypes.AddRange(obj.GetType().GetInterfaces().Select(i => i.ToString()));
                result.Add("$baseTypes", baseTypes);
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

        public static void SetObject(this JToken token, object obj)
        {
            var type = obj.GetType();
            foreach (var item in token.OfType<JProperty>())
            {
                if (item.Name.StartsWith('$'))
                    continue;

                var prop = type.GetProperty(item.Name);
                if (prop == null)
                    continue;

                if (item.Value.Type == JTokenType.Array) // list
                {
                    if (prop.GetValue(obj) is IList list)
                    {
                        // workaround since Clear() doesn't work well with observable collections
                        while (list.Count > 0)
                            list.RemoveAt(0);
                        var values = item.Value.OfType<JValue>().Select(v => Utils.ParseValue(v.Value?.ToString(), prop.PropertyType.GenericTypeArguments[0]));
                        foreach (var value in values)
                        {
                            if (value?.GetType() == prop.PropertyType.GenericTypeArguments[0])
                                list.Add(value);
                        }
                    }
                }
                else if (item.Value.Type == JTokenType.Object)
                {
                    if (prop.PropertyType.IsGenericType) // dictionary
                    {
                        if (prop.GetValue(obj) is IDictionary dict)
                        {
                            dict.Clear();
                            var values = item.Value.OfType<JProperty>().ToDictionary(
                                v => Utils.ParseValue(v.Name, prop.PropertyType.GenericTypeArguments[0]),
                                v => Utils.ParseValue(v.Value?.ToString(), prop.PropertyType.GenericTypeArguments[1]));
                            foreach (var value in values)
                            {
                                if (value.Key?.GetType() == prop.PropertyType.GenericTypeArguments[0] &&
                                    value.Value?.GetType() == prop.PropertyType.GenericTypeArguments[1])
                                    dict[value.Key] = value.Value;
                            }
                        }
                    }
                    else // object
                    {
                        var obj2 = prop.GetValue(obj);
                        item.SetObject(obj2);
                    }
                }
                else if (prop.CanWrite)
                {
                    prop.SetValue(obj, Utils.ParseValue((string)item.Value, prop.PropertyType));
                }
            }
        }

        public static object CallMethod(this object obj, string funcName, Microsoft.AspNetCore.Http.IFormCollection form)
        {
            var method = obj.GetType().GetMethod(funcName);
            if (method != null)
            {
                var methodParams = method.GetParameters();
                var args = form?.Select((kvp, i) =>
                        Utils.ParseValue(kvp.Value.ToString(), methodParams[i].ParameterType)).ToArray();
                return method.Invoke(obj, args);
            }
            throw new ArgumentException("No such function: " + funcName);
        }

        public static Action Debounce(this Action func, int milliseconds = 100)
        {
            var debouncer = Utils.Debouncer(milliseconds);
            return () => debouncer(func);
        }
    }
}
