using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Origami.Core.Flow;
using Origami.Core.Rules;
using Origami.Core.Run;

namespace Origami.Tests.Contracts
{
    // ORIGAMI-002: pins the public surface of the rule/flow types whose bodies are implemented later
    // (INTERFACE_CONTRACTS.md C-006..C-010, C-020, C-021). Signatures only, no behaviour:
    // behaviour tests belong to the implementation tasks. Changing a surface = contract change request.
    public class SurfaceContractTests
    {
        private const BindingFlags PublicDeclared =
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

        private static string[] Surface(Type t) =>
            t.GetMembers(PublicDeclared)
                .Where(m => !(m is MethodInfo mi && mi.IsSpecialName && !mi.Name.StartsWith("op_", StringComparison.Ordinal)))
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

        private static void AssertSurface(Type t, params string[] expected)
        {
            CollectionAssert.AreEqual(expected.OrderBy(s => s, StringComparer.Ordinal).ToArray(), Surface(t), t.Name);
        }

        [Test]
        public void OrigamiRun_Surface()
        {
            var t = typeof(OrigamiRun);
            Assert.IsTrue(t.IsSealed && t.IsPublic);
            AssertSurface(t,
                ".ctor()",
                "CommandResult NewRun()",
                "CommandResult Start(String)",
                "CommandResult Fold(FoldAction)",
                "CommandResult Hint()",
                "CommandResult Abandon()",
                "CommandResult EndDay()",
                "RunSnapshot GetSnapshot()");
        }

        [Test]
        public void RuleFunctions_Surface()
        {
            AssertSurface(typeof(Grading),
                "static Int32 Penalty(Int32,Int32)", "static Grade GradeFor(Int32)", "static Int32 GradePct(Grade)");
            AssertSurface(typeof(Pricing),
                "static Int32 DemandPct(Boolean)", "static Int32 SalePrice(Int32,Grade,Boolean)",
                "static Int32 SalePrice(String,Grade,Int32)");
            AssertSurface(typeof(Unlocking),
                "static Boolean IsUnlocked(String,CompletionCounts)", "static Int32 Progress(String,CompletionCounts)");
            AssertSurface(typeof(EndingRules),
                "static String Evaluate(RunEndReason,Int32,CompletionCounts)");
        }

        [Test]
        public void OrigamiFlow_Surface()
        {
            var t = typeof(OrigamiFlow);
            Assert.IsTrue(t.IsSealed && t.IsPublic);
            AssertSurface(t,
                ".ctor()",
                "FlowScreen Screen {get;}",
                "RunSnapshot Snapshot {get;}",
                "String SelectedModelId {get;}",
                "CommandResult LastResult {get;}",
                "SettlementResult Settlement {get;}",
                "String Message {get;}",
                "Boolean SelectModel(String)",
                "Boolean ClosePreview()",
                "Boolean StartSelected()",
                "Boolean Fold(FoldAction)",
                "Boolean Hint()",
                "Boolean RequestAbandon()",
                "Boolean ConfirmAbandon()",
                "Boolean CancelAbandon()",
                "Boolean EndDay()",
                "Boolean Acknowledge()",
                "Boolean Restart()",
                "Boolean ProcessKeys(FlowKeys)");
        }

        [Test]
        public void FlowTexts_Surface()
        {
            AssertSurface(typeof(FlowTexts),
                "const String BankruptNotice",
                "static String DayText(Int32)",
                "static String AttemptsText(Int32)",
                "static String MoneyText(Int32)",
                "static String Stars(Grade)",
                "static String ActionGlyph(FoldAction)",
                "static String ActionName(FoldAction)",
                "static String UnlockCondition(ModelDefinition,CompletionCounts)",
                "static String Rejection(RejectReason,ModelDefinition,CompletionCounts)",
                "static IReadOnlyList`1 ScreenLines(OrigamiFlow)");
            Assert.AreEqual("돈이 없어 가게 문을 닫았습니다", FlowTexts.BankruptNotice);
        }

        [Test]
        public void FlowEnums_Members()
        {
            CollectionAssert.AreEqual(
                new[] { "Workbench", "Preview", "Folding", "AbandonConfirm", "Notice", "Settlement", "Result" },
                Enum.GetNames(typeof(FlowScreen)));
            CollectionAssert.AreEqual(
                new[] { "None", "FoldUp", "FoldDown", "FoldLeft", "FoldRight", "Flip", "Hint", "Escape", "Enter" },
                Enum.GetNames(typeof(FlowKeys)));
            Assert.IsTrue(typeof(FlowKeys).GetCustomAttributes(typeof(FlagsAttribute), false).Length == 1);
            CollectionAssert.AreEqual(
                new[] { "Rejected", "RunStarted", "FoldStarted", "StepCorrect", "StepMistake", "FoldCompleted", "FoldRuined",
                        "HintRevealed", "FoldAbandoned", "DayEnded", "RunFinished" },
                Enum.GetNames(typeof(CommandOutcome)));
        }

        [Test]
        public void Core_HasNoUnityReference()
        {
            var asm = typeof(OrigamiRun).Assembly;
            Assert.IsFalse(asm.GetReferencedAssemblies().Any(a => a.Name.StartsWith("UnityEngine", StringComparison.Ordinal)));
        }
    }
}
