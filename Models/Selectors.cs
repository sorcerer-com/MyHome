﻿using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace MyHome.Models
{
    public static class Selectors
    {
        public static IEnumerable<string> GetRooms()
        {
            return MyHome.Instance.Rooms.Select(r => r.Name);
        }

        public static IEnumerable<string> GetTarget()
        {
            var result = MyHome.Instance.Rooms.Select(r => $"{r.Name} ({r.GetType().Name})");
            result = result.Union(
                MyHome.Instance.Rooms.SelectMany(r =>
                    r.Devices.Select(d => $"{r.Name}.{d.Name} ({d.GetType().Name})")));
            return result;
        }

        public static IEnumerable<string> GetSensorsSubnames()
        {
            return MyHome.Instance.Rooms.SelectMany(r => r.SensorsValues.Keys).Distinct().OrderBy(s => s);
        }

        public static IEnumerable<string> GetFunctions()
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

        public static IEnumerable<string> GetProperties()
        {
            var functions = MyHome.Instance.Rooms.SelectMany(r => GetProperties(r));
            functions = functions.Union(
                MyHome.Instance.Rooms.SelectMany(r =>
                    r.Devices.SelectMany(d => GetProperties(d))));
            return functions.Distinct();
        }

        private static IEnumerable<string> GetProperties(object obj)
        {
            return obj.GetType()
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(pi => pi.CanWrite &&
                    (pi.PropertyType.IsPrimitive || pi.PropertyType == typeof(string)) &&
                    !pi.DeclaringType.IsAbstract)
                .Select(pi => $"{pi.DeclaringType.Name}.{pi.Name} ({pi.PropertyType.Name})");
        }
    }
}