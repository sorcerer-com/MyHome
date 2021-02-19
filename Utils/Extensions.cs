using System;
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
    }
}
