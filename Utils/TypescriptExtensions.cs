using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using System.Text;

namespace MyHome.Utils
{
    public static class TypescriptExtensions
    {
        public static string ToTypescript(this Assembly assembly)
        {
            var result = new StringBuilder();
            foreach (var type in assembly.GetTypes().Where(IsValidType))
                result.Append(type.ToTypescript() + "\n\n");

            return result.ToString();
        }

        public static string ToTypescript(this Type type)
        {
            var result = new StringBuilder();
            if (type.IsEnum)
            {
                result.Append($"enum {type.Name} {{\n");
                result.Append("  ").Append(string.Join(",\n  ", type.GetFields().Where(f => f.IsLiteral).Select(f => f.Name))).Append('\n');
                result.Append('}');
            }
            else if (type.IsInterface) // workaround for interfaces just to be added as strings
            {
                result.Append($"let {type.Name}; // interface");
            }
            else
            {
                var baseType = type.BaseType != null && type.BaseType != typeof(object) ? $"extends {type.BaseType.Name} " : "";
                var dataType = type.IsInterface ? "interface" : "class";
                result.Append($"{dataType} {type.Name} {baseType}{{\n");
                // properties
                foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                    result.Append("  ").Append($"{prop.Name}: {GetTypescriptType(prop.PropertyType)};\n");
                foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly))
                    result.Append("  ").Append($"static {prop.Name}: {GetTypescriptType(prop.PropertyType)};\n");

                // functions
                foreach (var func in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                {
                    if (!func.IsSpecialName)
                        result.Append("  ").Append(GetTypescriptFunction(func));
                }
                foreach (var func in type.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly))
                {
                    if (!func.IsSpecialName)
                        result.Append("  ").Append("static " + GetTypescriptFunction(func));
                }

                result.Append('}');
            }

            return result.ToString();
        }


        private static bool IsValidType(Type type)
        {
            return type.Namespace?.Contains(type.Assembly.GetName().Name) == true &&
                    !type.IsNestedPrivate && !(type.IsAbstract && type.IsSealed) && // not anonymous or static
                    (type.BaseType == typeof(object) || type.IsEnum || type.IsInterface || IsValidType(type.BaseType));
        }

        private static string GetTypescriptType(Type type)
        {
            if (type.IsNumericType())
                return "number";
            else if (type.GetInterface(nameof(IDictionary)) != null)
                return "{} /* " + string.Join(", ", type.GenericTypeArguments.Select(GetTypescriptType)).Replace("*/", "") + " */";
            else if (type.GetInterface(nameof(IEnumerable)) != null && type.GenericTypeArguments.Length > 0)
                return GetTypescriptType(type.GenericTypeArguments[0]) + "[]";
            else if (type == typeof(void))
                return "void";
            if (type.Name.Contains('`'))
                return "any";
            return type.Name;
        }

        private static string GetTypescriptFunction(MethodInfo func)
        {
            var args = string.Join(", ", func.GetParameters().Select(p => p.Name.Replace("function", "_function") + ": " + GetTypescriptType(p.ParameterType)));
            var returnType = GetTypescriptType(func.ReturnType);
            var body = func.IsAbstract ? ";" : " { }";
            return $"{func.Name}({args}): {returnType}{body}\n";
        }

        private static bool IsNumericType(this Type type)
        {
            if (type.IsEnum)
                return false;
            return Type.GetTypeCode(type) switch
            {
                TypeCode.Byte or TypeCode.SByte or TypeCode.UInt16 or TypeCode.UInt32 or TypeCode.UInt64 or TypeCode.Int16 or TypeCode.Int32 or TypeCode.Int64 or TypeCode.Decimal or TypeCode.Double or TypeCode.Single => true,
                _ => false,
            };
        }
    }
}