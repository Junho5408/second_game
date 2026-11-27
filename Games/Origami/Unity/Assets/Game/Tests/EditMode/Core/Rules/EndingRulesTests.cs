using System;
using NUnit.Framework;
using Origami.Core.Content;
using Origami.Core.Rules;

namespace Origami.Tests.Rules
{
    // ORIGAMI-003: C-009 EndingRules (R-014). F-006 AC-01..09 are pure judgement-input tests.
    // Unspecified counts are 0; "5종 모두 ★3 ≥ 1" means exactly one ★3 of every model (FEATURE_SPECS 공통 표기).
    public class EndingRulesTests
    {
        private static readonly CompletionCounts Empty = CompletionCounts.Empty;

        private static CompletionCounts AllStar3()
        {
            var c = Empty;
            foreach (var m in OrigamiCatalog.Models) c = c.With(m.Id, Grade.Star3, 1);
            return c;
        }

        private static string Eval(RunEndReason r, int money, CompletionCounts c) => EndingRules.Evaluate(r, money, c);

        [Test]
        public void F006_AC01_BankruptMoney0_E01()
        {
            Assert.AreEqual(EndingIds.EmptyHanded, Eval(RunEndReason.Bankrupt, 0, Empty));
        }

        [Test]
        public void F006_AC02_Money20IsEmptyHanded_Money21IsLocalShop()
        {
            Assert.AreEqual(EndingIds.EmptyHanded, Eval(RunEndReason.Completed, 20, Empty));
            Assert.AreEqual(EndingIds.LocalShop, Eval(RunEndReason.Completed, 21, Empty));
        }

        [Test]
        public void F006_AC03_Crane15AllStar1_E02()
        {
            Assert.AreEqual(EndingIds.CraneWish, Eval(RunEndReason.Completed, 1200, Empty.With(ModelIds.Crane, Grade.Star1, 15)));
            var mixed = Empty.With(ModelIds.Crane, Grade.Star1, 5).With(ModelIds.Crane, Grade.Star2, 5).With(ModelIds.Crane, Grade.Star3, 5);
            Assert.AreEqual(EndingIds.CraneWish, Eval(RunEndReason.Completed, 1200, mixed), "any grade");
        }

        [Test]
        public void F006_AC04_Crane14Money1200_E03()
        {
            Assert.AreEqual(EndingIds.Tycoon, Eval(RunEndReason.Completed, 1200, Empty.With(ModelIds.Crane, Grade.Star1, 14)));
        }

        [Test]
        public void F006_AC05_Money1000IsTycoon_Money999IsLocalShop()
        {
            Assert.AreEqual(EndingIds.Tycoon, Eval(RunEndReason.Completed, 1000, Empty));
            Assert.AreEqual(EndingIds.LocalShop, Eval(RunEndReason.Completed, 999, Empty));
        }

        [Test]
        public void F006_AC06_AllStar3Money500_E04()
        {
            Assert.AreEqual(EndingIds.Master, Eval(RunEndReason.Completed, 500, AllStar3()));
        }

        [Test]
        public void F006_AC07_PriorityTycoonOverMaster_CraneOverAll()
        {
            Assert.AreEqual(EndingIds.Tycoon, Eval(RunEndReason.Completed, 1000, AllStar3()));
            var c = AllStar3().With(ModelIds.Crane, Grade.Star3, 15);
            Assert.AreEqual(EndingIds.CraneWish, Eval(RunEndReason.Completed, 1500, c));
        }

        [Test]
        public void F006_AC08_DragonOnlyStar2_E05()
        {
            var c = AllStar3().With(ModelIds.Dragon, Grade.Star3, 0).With(ModelIds.Dragon, Grade.Star2, 3);
            Assert.AreEqual(EndingIds.LocalShop, Eval(RunEndReason.Completed, 500, c));
        }

        [Test]
        public void F006_AC09_EmptyHandedBeatsCraneWish()
        {
            Assert.AreEqual(EndingIds.EmptyHanded, Eval(RunEndReason.Completed, 20, Empty.With(ModelIds.Crane, Grade.Star3, 20)));
            Assert.AreEqual(EndingIds.EmptyHanded, Eval(RunEndReason.Bankrupt, 0, Empty.With(ModelIds.Crane, Grade.Star1, 15)));
        }

        [Test]
        public void R014_BankruptWinsEvenWithMoney()
        {
            // Bankrupt implies money 0 in a real run; the rule is "Bankrupt OR money ≤ 20".
            Assert.AreEqual(EndingIds.EmptyHanded, Eval(RunEndReason.Bankrupt, 5000, AllStar3()));
        }

        [Test]
        public void C009_InvalidArguments_Throw()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => Eval(RunEndReason.None, 100, Empty));
            Assert.Throws<ArgumentOutOfRangeException>(() => Eval((RunEndReason)7, 100, Empty));
            Assert.Throws<ArgumentNullException>(() => Eval(RunEndReason.Completed, 100, null));
        }
    }
}
