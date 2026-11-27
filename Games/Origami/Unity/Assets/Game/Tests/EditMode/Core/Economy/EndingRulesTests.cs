using System;
using NUnit.Framework;
using Origami.Core.Catalog;
using Origami.Core.Economy;
using Origami.Core.Recognition;

namespace Origami.Tests.Economy
{
    // C-130 EndingRules — R-018, F-012 AC-01~07 (counts not mentioned are 0).
    public class EndingRulesTests
    {
        private static CompletionCounts AllStar3()
        {
            var c = CompletionCounts.Empty;
            foreach (var m in ModelCatalog.Models) c = c.Increment(m.Id, Grade.Star3);
            return c;
        }

        private static string Done(int money, CompletionCounts c = null) => EndingRules.Evaluate(RunEndReason.Completed, money, c ?? CompletionCounts.Empty);

        [Test]
        public void F012_AC01_EmptyHandedThreshold()
        {
            Assert.AreEqual("E-01", Done(20));
            Assert.AreEqual("E-05", Done(21));
            Assert.AreEqual("E-01", EndingRules.Evaluate(RunEndReason.Bankrupt, 0, CompletionCounts.Empty));
        }

        [Test]
        public void F012_AC02_TulipGardenBeforeMaster()
        {
            var c = AllStar3().With(ModelIds.Tulip, Grade.Star1, 12);
            Assert.AreEqual("E-02", Done(700, c));
        }

        [Test]
        public void F012_AC03_MasterBeforeTycoon() => Assert.AreEqual("E-03", Done(700, AllStar3()));

        [Test]
        public void F012_AC04_TycoonThreshold()
        {
            Assert.AreEqual("E-04", Done(600));
            Assert.AreEqual("E-05", Done(599));
        }

        [Test]
        public void F012_AC05_EmptyHandedFirst()
        {
            Assert.AreEqual("E-01", Done(20, CompletionCounts.Empty.With(ModelIds.Tulip, Grade.Star3, 12)));
            Assert.AreEqual("E-01", EndingRules.Evaluate(RunEndReason.Bankrupt, 0, AllStar3()));
        }

        [Test]
        public void F012_AC06_ElevenTulipsAndNoCicada_IsTycoon()
        {
            var c = CompletionCounts.Empty.With(ModelIds.Tulip, Grade.Star3, 11)
                .Increment(ModelIds.Plane, Grade.Star3).Increment(ModelIds.House, Grade.Star3).Increment(ModelIds.Dog, Grade.Star3);
            Assert.AreEqual("E-04", Done(650, c));
        }

        [Test]
        public void F012_AC07_CicadaStar2IsNotMaster()
        {
            var c = CompletionCounts.Empty.Increment(ModelIds.Plane, Grade.Star3).Increment(ModelIds.House, Grade.Star3)
                .Increment(ModelIds.Tulip, Grade.Star3).Increment(ModelIds.Dog, Grade.Star3).Increment(ModelIds.Cicada, Grade.Star2);
            Assert.AreEqual("E-05", Done(500, c));
        }

        [Test]
        public void R018_TulipCountIgnoresGrade()
        {
            var c = CompletionCounts.Empty.With(ModelIds.Tulip, Grade.Star1, 4).With(ModelIds.Tulip, Grade.Star2, 4).With(ModelIds.Tulip, Grade.Star3, 4);
            Assert.AreEqual("E-02", Done(21, c));
            Assert.AreEqual("E-05", Done(21, c.With(ModelIds.Tulip, Grade.Star3, 3)));
        }

        [Test]
        public void R018_EveryResultIsACatalogEnding()
        {
            foreach (var id in new[] { Done(0), Done(21), Done(600), Done(21, AllStar3()) })
                Assert.IsNotNull(EndingCatalog.Find(id), id);
        }

        [Test]
        public void InvalidArguments()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => EndingRules.Evaluate(RunEndReason.None, 100, CompletionCounts.Empty));
            Assert.Throws<ArgumentOutOfRangeException>(() => EndingRules.Evaluate((RunEndReason)7, 100, CompletionCounts.Empty));
            Assert.Throws<ArgumentNullException>(() => EndingRules.Evaluate(RunEndReason.Completed, 100, null));
        }
    }
}
