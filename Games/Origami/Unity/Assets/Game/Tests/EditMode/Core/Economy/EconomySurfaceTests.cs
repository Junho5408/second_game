using NUnit.Framework;
using Origami.Core.Economy;
using Origami.Tests.Folding;

namespace Origami.Tests.Economy
{
    // C-130: pins the public surface (INTERFACE_CONTRACTS — a change needs a contract change request).
    public class EconomySurfaceTests
    {
        [Test]
        public void Namespace_ContainsOnlyContractTypes()
        {
            SurfaceAssert.NamespaceHasOnly(typeof(Pricing).Assembly, "Origami.Core.Economy",
                "CompletionCounts", "EndingRules", "Pricing", "RuleConstants", "RunEndReason");
        }

        [Test]
        public void RuleConstants_Surface()
        {
            var t = typeof(RuleConstants);
            Assert.IsTrue(t.IsSealed && t.IsAbstract, "static");
            SurfaceAssert.Is(t,
                "const Int32 TotalDays", "const Int32 StartMoney", "const Int32 PaperCost", "const Int32 DailySheets",
                "const Int32 GradePctStar3", "const Int32 GradePctStar2", "const Int32 GradePctStar1",
                "const Int32 PopularPct", "const Int32 NormalPct", "const Int32 PriceDivisor",
                "const Int32 EmptyHandedMoney", "const Int32 TulipGardenCount", "const Int32 TycoonMoney");
        }

        [Test]
        public void Pricing_Surface()
        {
            var t = typeof(Pricing);
            Assert.IsTrue(t.IsSealed && t.IsAbstract, "static");
            SurfaceAssert.Is(t,
                "static Int32 GradePct(Grade)",
                "static Int32 SalePrice(Int32,Grade,Boolean)",
                "static Int32 SalePrice(String,Grade,Int32)");
        }

        [Test]
        public void CompletionCounts_Surface()
        {
            var t = typeof(CompletionCounts);
            Assert.IsTrue(t.IsSealed && !t.IsAbstract);
            Assert.IsTrue(typeof(System.IEquatable<CompletionCounts>).IsAssignableFrom(t));
            SurfaceAssert.Is(t,
                "static readonly CompletionCounts Empty",
                "Int32 Get(String,Grade)",
                "Int32 Total(String)",
                "CompletionCounts With(String,Grade,Int32)",
                "CompletionCounts Increment(String,Grade)",
                "Boolean Equals(CompletionCounts)",
                "Boolean Equals(Object)",
                "Int32 GetHashCode()");
        }

        [Test]
        public void RunEndReason_Values() => SurfaceAssert.EnumIs(typeof(RunEndReason), "None=0", "Completed=1", "Bankrupt=2");

        [Test]
        public void EndingRules_Surface()
        {
            var t = typeof(EndingRules);
            Assert.IsTrue(t.IsSealed && t.IsAbstract, "static");
            SurfaceAssert.Is(t, "static String Evaluate(RunEndReason,Int32,CompletionCounts)");
        }
    }
}
