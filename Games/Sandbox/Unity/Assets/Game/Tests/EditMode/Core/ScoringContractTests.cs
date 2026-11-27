using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Sandbox.Core.Scoring;

namespace Sandbox.Tests
{
    // QA (SANDBOX-002): pins the public surface fixed by INTERFACE_CONTRACTS.md C-001..C-003 and the
    // already-complete ScoreSnapshot value type. Behaviour of ScoreCounter / MultiplierForCombo is
    // SANDBOX-003's job and is deliberately not exercised here. Engine-free (dotnet + Unity EditMode).
    public class ScoringContractTests
    {
        private const BindingFlags PublicDeclared =
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

        private static string[] PublicSurface(Type t) =>
            t.GetMembers(PublicDeclared)
                .Where(m => !(m is MethodInfo mi && mi.IsSpecialName && !mi.Name.StartsWith("op_")))
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
                    return p.PropertyType.Name + " " + p.Name + (p.CanWrite ? " {get;set;}" : " {get;}");
                case FieldInfo f:
                    return (f.IsLiteral ? "const " : f.IsStatic ? "static " : "") +
                           (f.IsInitOnly ? "readonly " : "") + f.FieldType.Name + " " + f.Name;
                default:
                    return m.MemberType + " " + m.Name;
            }
        }

        // ---- C-001 ScoreCounter: signatures only ----

        [Test]
        public void ScoreCounter_IsSealedClass_WithExactlyTheContractedPublicSurface()
        {
            var t = typeof(ScoreCounter);
            Assert.IsTrue(t.IsClass && t.IsSealed && t.IsPublic, "C-001: public sealed class");
            Assert.AreEqual("Sandbox.Core.Scoring", t.Namespace);
            CollectionAssert.AreEqual(
                new[] { ".ctor()", "Boolean Press(Double)", "ScoreSnapshot GetState(Double)", "Void Reset()" }
                    .OrderBy(s => s, StringComparer.Ordinal).ToArray(),
                PublicSurface(t));
        }

        // ---- C-002 ScoreRules: constants and signature ----

        [Test]
        public void ScoreRules_ConstantsMatchGameRules()
        {
            Assert.AreEqual(1, ScoreRules.BasePoints);
            Assert.AreEqual(1.0, ScoreRules.ComboWindowSeconds);
            Assert.AreEqual(4, ScoreRules.MaxMultiplier);
        }

        [Test]
        public void ScoreRules_IsStaticClass_WithExactlyTheContractedPublicSurface()
        {
            var t = typeof(ScoreRules);
            Assert.IsTrue(t.IsAbstract && t.IsSealed && t.IsPublic, "C-002: public static class");
            Assert.AreEqual("Sandbox.Core.Scoring", t.Namespace);
            CollectionAssert.AreEqual(
                new[]
                    {
                        "const Int32 BasePoints", "const Double ComboWindowSeconds", "const Int32 MaxMultiplier",
                        "static Int32 MultiplierForCombo(Int32)",
                    }
                    .OrderBy(s => s, StringComparer.Ordinal).ToArray(),
                PublicSurface(t));
        }

        // ---- C-003 ScoreSnapshot: complete value type ----

        [Test]
        public void ScoreSnapshot_IsReadonlyEquatableStruct()
        {
            var t = typeof(ScoreSnapshot);
            Assert.IsTrue(t.IsValueType && t.IsPublic);
            Assert.IsTrue(typeof(IEquatable<ScoreSnapshot>).IsAssignableFrom(t));
            Assert.IsTrue(t.GetCustomAttributes(false).Any(a => a.GetType().Name == "IsReadOnlyAttribute"),
                "C-003: readonly struct");
            Assert.AreEqual("Sandbox.Core.Scoring", t.Namespace);
        }

        [Test]
        public void ScoreSnapshot_Initial_IsZeroZeroOne()
        {
            Assert.AreEqual(0, ScoreSnapshot.Initial.Score);
            Assert.AreEqual(0, ScoreSnapshot.Initial.Combo);
            Assert.AreEqual(1, ScoreSnapshot.Initial.Multiplier);
            Assert.AreEqual(new ScoreSnapshot(0, 0, 1), ScoreSnapshot.Initial);
        }

        [Test]
        public void ScoreSnapshot_Default_IsNotInitial()
        {
            // C-003: default(ScoreSnapshot) is (0,0,0) and must not be used as the initial value.
            Assert.AreNotEqual(ScoreSnapshot.Initial, default(ScoreSnapshot));
            Assert.AreEqual(0, default(ScoreSnapshot).Multiplier);
        }

        [Test]
        public void ScoreSnapshot_Constructor_StoresFieldsInOrder()
        {
            var s = new ScoreSnapshot(6, 5, 2);
            Assert.AreEqual(6, s.Score);
            Assert.AreEqual(5, s.Combo);
            Assert.AreEqual(2, s.Multiplier);
        }

        [TestCase(6, 5, 2, true)]
        [TestCase(7, 5, 2, false)]
        [TestCase(6, 4, 2, false)]
        [TestCase(6, 5, 1, false)]
        public void ScoreSnapshot_Equality_ComparesAllThreeFields(int score, int combo, int mult, bool expected)
        {
            var a = new ScoreSnapshot(6, 5, 2);
            var b = new ScoreSnapshot(score, combo, mult);
            Assert.AreEqual(expected, a.Equals(b), "Equals(ScoreSnapshot)");
            Assert.AreEqual(expected, a.Equals((object)b), "Equals(object)");
            Assert.AreEqual(expected, a == b, "operator ==");
            Assert.AreEqual(!expected, a != b, "operator !=");
            if (expected) Assert.AreEqual(a.GetHashCode(), b.GetHashCode(), "equal values hash equally");
        }

        [Test]
        public void ScoreSnapshot_EqualsObject_RejectsOtherTypesAndNull()
        {
            var s = new ScoreSnapshot(1, 1, 1);
            Assert.IsFalse(s.Equals(null));
            Assert.IsFalse(s.Equals("Score=1 Combo=1 x1"));
            Assert.IsFalse(s.Equals((object)1));
        }

        [Test]
        public void ScoreSnapshot_HashCode_DistinguishesFieldPermutations()
        {
            // Not required by the contract, but a hash that ignores field order would make
            // snapshots like (1,2,1) and (2,1,1) collide; check the common small cases differ.
            Assert.AreNotEqual(new ScoreSnapshot(1, 2, 1).GetHashCode(), new ScoreSnapshot(2, 1, 1).GetHashCode());
            Assert.AreNotEqual(new ScoreSnapshot(6, 5, 2).GetHashCode(), new ScoreSnapshot(5, 6, 2).GetHashCode());
        }

        [Test]
        public void ScoreSnapshot_HashCode_DoesNotThrowAtIntExtremes()
        {
            Assert.DoesNotThrow(() => new ScoreSnapshot(int.MaxValue, int.MaxValue, int.MaxValue).GetHashCode());
            Assert.DoesNotThrow(() => new ScoreSnapshot(int.MinValue, int.MinValue, int.MinValue).GetHashCode());
        }

        [Test]
        public void ScoreSnapshot_ToString_MatchesContractDebugFormat()
        {
            Assert.AreEqual("Score=6 Combo=5 x2", new ScoreSnapshot(6, 5, 2).ToString());
            Assert.AreEqual("Score=0 Combo=0 x1", ScoreSnapshot.Initial.ToString());
        }
    }
}
