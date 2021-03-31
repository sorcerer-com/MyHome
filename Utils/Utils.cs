using System;
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

        public static dynamic ParseValue(string value)
        {
            if (value == "true" || value == "false")
                return value == "true";
            else if (int.TryParse(value, out int i))
                return i;
            else if (double.TryParse(value, out double d))
                return d;
            else if (GetType(value.Split('.')[0])?.IsEnum == true &&
                Enum.TryParse(GetType(value.Split('.')[0]), value.Split('.')[1], out object e)) // Enum
            {
                return e;
            }
            else if (DateTime.TryParse(value, out DateTime dt))
                return dt;
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
