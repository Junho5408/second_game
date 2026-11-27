using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace Origami.Tests.Folding
{
    /// <summary>
    /// Reflection helper pinning a type's public declared surface (same format as v1 SurfaceContractTests).
    /// Changing a surface = contract change request (INTERFACE_CONTRACTS header).
    /// </summary>
    internal static class SurfaceAssert
    {
        private const BindingFlags PublicDeclared =
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

        public static string[] Surface(Type t) =>
            t.GetMembers(PublicDeclared)
                .Where(m => !(m is MethodInfo mi && mi.IsSpecialName && !mi.Name.StartsWith("op_", StringComparison.Ordinal)))
                .Where(m => !(t.IsEnum && m is FieldInfo f && f.IsSpecialName))
                .Select(Describe)
                .OrderBy(s => s, StringComparer.Ordinal)
                .ToArray();

        private static string Describe(MemberInfo m)
        {
            switch (m)
            {
                case ConstructorInfo c:
                    return ".ctor(" + string.Join(",", c.GetParameters().Select(p => p.ParameterType.Name)) + ")";
                case MethodInfo mi:
                    return (mi.IsStatic ? "static " : "") + mi.ReturnType.Name + " " + mi.Name + "(" +
                           string.Join(",", mi.GetParameters().Select(p => p.ParameterType.Name)) + ")";
                case PropertyInfo p:
                    return (p.GetMethod != null && p.GetMethod.IsStatic ? "static " : "") + p.PropertyType.Name + " " + p.Name +
                           (p.CanWrite && p.SetMethod != null && p.SetMethod.IsPublic ? " {get;set;}" : " {get;}");
                case FieldInfo f:
                    return (f.IsLiteral ? "const " : f.IsStatic ? "static " : "") +
                           (f.IsInitOnly ? "readonly " : "") + f.FieldType.Name + " " + f.Name;
                case Type nested:
                    return "nested " + nested.Name;
                default:
                    return m.MemberType + " " + m.Name;
            }
        }

        public static void Is(Type t, params string[] expected)
        {
            CollectionAssert.AreEqual(expected.OrderBy(s => s, StringComparer.Ordinal).ToArray(), Surface(t), t.Name);
        }

        /// <summary>Enum names and values in declaration order.</summary>
        public static void EnumIs(Type t, params string[] nameEqualsValue)
        {
            Assert.IsTrue(t.IsEnum, t.Name);
            var actual = Enum.GetValues(t).Cast<object>().Select(v => Enum.GetName(t, v) + "=" + Convert.ToInt32(v)).ToArray();
            CollectionAssert.AreEqual(nameEqualsValue, actual, t.Name);
        }

        /// <summary>No public type in the namespace other than the listed ones (C-1xx list is exhaustive).</summary>
        public static void NamespaceHasOnly(Assembly asm, string ns, params string[] names)
        {
            var actual = asm.GetTypes().Where(x => x.IsPublic && x.Namespace == ns).Select(x => x.Name)
                .OrderBy(s => s, StringComparer.Ordinal).ToArray();
            CollectionAssert.AreEqual(names.OrderBy(s => s, StringComparer.Ordinal).ToArray(), actual, ns);
        }
    }
}
