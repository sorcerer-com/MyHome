﻿using System;
using System.Linq;
using System.Reflection;

namespace MyHome.Utils
{
    public static class Utils
    {
        public static Type GetType(string typeName)
        {
            return Assembly
                .GetExecutingAssembly()
                .GetTypes()
                .FirstOrDefault(t => t.Name == typeName);
        }

        public static dynamic ParseValue(string value, Type type)
        {
            if ((type == null || type == typeof(bool)) &&
                (value == "true" || value == "false"))
            {
                return value == "true";
            }
            else if ((type == null || type.Name.StartsWith("Int")) &&
                int.TryParse(value, out int i))
            {
                return i;
            }
            else if ((type == null || type == typeof(double)) &&
                double.TryParse(value, out double d))
            {
                return d;
            }
            else if (GetType(value.Split('.')[0])?.IsEnum == true &&
                Enum.TryParse(GetType(value.Split('.')[0]), value.Split('.')[1], out object e)) // Enum
            {
                return e;
            }
            else if ((type == null || type == typeof(DateTime)) &&
                DateTime.TryParse(value, out DateTime dt))
            {
                return dt;
            }
            return value;
        }

        public static string GenerateRandomToken(int bytesCount)
        {
            var rand = new Random();
            var buffer = new byte[bytesCount];
            rand.NextBytes(buffer);
            return BitConverter.ToString(buffer).ToLower().Replace("-", string.Empty);
        }
    }
}
