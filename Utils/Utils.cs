using System;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

using NLog;

namespace MyHome.Utils
{
    public enum Condition
    {
        Equal,
        NotEqual,
        Less,
        LessOrEqual,
        Greater,
        GreaterOrEqual
    }

    public static class Utils
    {
        public static void Retry(Action<int> action, int times, ILogger logger = null, string operation = null)
        {
            operation = operation != null ? $"'{operation}'" : "";
            for (int i = 0; i < times; i++)
            {
                try
                {
                    action.Invoke(i);
                    return;
                }
                catch (Exception e)
                {
                    logger?.Warn($"Action {operation} failed, retry {i + 1} of {times}");
                    logger?.Debug(e);
                }
            }
            logger?.Error($"Action {operation} failed, retries exceeded");
        }

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
            else if ((type == null || type == typeof(DateTime)) &&
                DateTime.TryParse(value, out DateTime dt))
            {
                return dt;
            }
            else if ((type == null || type == typeof(TimeSpan)) &&
                TimeSpan.TryParse(value, out TimeSpan ts))
            {
                return ts;
            }
            else if (GetType(value.Split('.')[0])?.IsEnum == true &&
                Enum.TryParse(GetType(value.Split('.')[0]), value.Split('.')[1], out object e)) // Enum
            {
                return e;
            }
            else if (type != null && type.GetInterface(nameof(ITuple)) != null)
            {
                var values = value[1..^1].Split(", ");
                if (values.Length == 2)
                    return ValueTuple.Create(ParseValue(values[0], type.GenericTypeArguments[0]), ParseValue(values[1], type.GenericTypeArguments[1]));
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

        public static (string host, int? port) SplitAddress(string address)
        {
            if (!address.Contains(":"))
                return (address, null);

            var split = address.Split(":");
            return (split[0], int.Parse(split[1]));
        }

        public static bool CheckCondition<T>(T value1, T value2, Condition condition) where T : IComparable
        {
            var compare = value1.CompareTo(value2);
            return condition switch
            {
                Condition.Equal => compare == 0,
                Condition.NotEqual => compare != 0,
                Condition.Less => compare < 0,
                Condition.LessOrEqual => compare <= 0,
                Condition.Greater => compare > 0,
                Condition.GreaterOrEqual => compare >= 0,
                _ => throw new Exception("Check invalid condition: " + condition),
            };
        }
    }
}
