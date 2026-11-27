using System.Linq;
using NUnit.Framework;
using Origami.Core.Content;

namespace Origami.Tests.Contracts
{
    // ORIGAMI-002: the code-defined catalog must match CONTENT_SPECS.md exactly (DATA_STRUCTURE.md D-001..D-003).
    public class CatalogContractTests
    {
        private const FoldAction U = FoldAction.U, D = FoldAction.D, L = FoldAction.L, R = FoldAction.R, F = FoldAction.F;

        [Test]
        public void Models_AreFiveInSpecOrder()
        {
            CollectionAssert.AreEqual(
                new[] { ModelIds.Plane, ModelIds.Boat, ModelIds.Frog, ModelIds.Crane, ModelIds.Dragon },
                OrigamiCatalog.Models.Select(m => m.Id).ToArray());
            CollectionAssert.AreEqual(new[] { 1, 2, 3, 4, 5 }, OrigamiCatalog.Models.Select(m => m.Order).ToArray());
            CollectionAssert.AreEqual(new[] { "plane", "boat", "frog", "crane", "dragon" },
                new[] { ModelIds.Plane, ModelIds.Boat, ModelIds.Frog, ModelIds.Crane, ModelIds.Dragon });
        }

        [TestCase("plane", "종이비행기", 1, 4, null)]
        [TestCase("boat", "종이배", 1, 6, null)]
        [TestCase("frog", "개구리", 2, 10, "boat")]
        [TestCase("crane", "학", 3, 18, "frog")]
        [TestCase("dragon", "용", 6, 40, "crane")]
        public void Model_CostsPricesAndUnlockChain(string id, string name, int paper, int basePrice, string prereq)
        {
            var m = OrigamiCatalog.FindModel(id);
            Assert.IsNotNull(m);
            Assert.AreEqual(name, m.DisplayName);
            Assert.AreEqual(paper, m.PaperCost);
            Assert.AreEqual(basePrice, m.BasePrice);
            Assert.AreEqual(prereq, m.UnlockPrerequisiteId);
            Assert.AreEqual(prereq == null, m.IsInitiallyUnlocked);
        }

        [Test]
        public void Model_StepSequencesMatchSpec()
        {
            CollectionAssert.AreEqual(new[] { U, L, R }, OrigamiCatalog.FindModel("plane").Steps);
            CollectionAssert.AreEqual(new[] { D, U, F, U }, OrigamiCatalog.FindModel("boat").Steps);
            CollectionAssert.AreEqual(new[] { L, R, F, D, D, U }, OrigamiCatalog.FindModel("frog").Steps);
            CollectionAssert.AreEqual(new[] { U, L, F, R, D, F, L, U }, OrigamiCatalog.FindModel("crane").Steps);
            CollectionAssert.AreEqual(new[] { R, R, U, F, L, D, F, U, L, D }, OrigamiCatalog.FindModel("dragon").Steps);
            CollectionAssert.AreEqual(new[] { 3, 4, 6, 8, 10 }, OrigamiCatalog.Models.Select(m => m.StepCount).ToArray());
        }

        [Test]
        public void Model_StepsCannotBeMutatedThroughTheList()
        {
            var steps = OrigamiCatalog.FindModel("plane").Steps;
            Assert.IsTrue(steps is System.Collections.Generic.ICollection<FoldAction> c && c.IsReadOnly);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("unicorn")]
        [TestCase("Plane")]
        [TestCase(" plane")]
        public void FindModel_UnknownNullOrEmpty_ReturnsNull(string id)
        {
            Assert.IsNull(OrigamiCatalog.FindModel(id));
            Assert.AreEqual(-1, OrigamiCatalog.IndexOf(id));
        }

        [Test]
        public void IndexOf_IsZeroBasedCatalogOrder()
        {
            Assert.AreEqual(0, OrigamiCatalog.IndexOf("plane"));
            Assert.AreEqual(4, OrigamiCatalog.IndexOf("dragon"));
        }

        [Test]
        public void PopularByDay_MatchesSpecTable()
        {
            CollectionAssert.AreEqual(
                new[] { "plane", "boat", "frog", "boat", "crane", "frog", "dragon" },
                Enumerable.Range(1, 7).Select(OrigamiCatalog.PopularModelId).ToArray());
            Assert.AreEqual(7, OrigamiCatalog.PopularByDay.Count);
        }

        [TestCase(0)]
        [TestCase(8)]
        [TestCase(-1)]
        public void PopularModelId_OutsideOneToSeven_IsNull(int day)
        {
            Assert.IsNull(OrigamiCatalog.PopularModelId(day));
        }

        [Test]
        public void Endings_AreFiveInPriorityOrder()
        {
            CollectionAssert.AreEqual(
                new[] { "E-01", "E-02", "E-03", "E-04", "E-05" },
                OrigamiCatalog.Endings.Select(e => e.Id).ToArray());
            CollectionAssert.AreEqual(new[] { 1, 2, 3, 4, 5 }, OrigamiCatalog.Endings.Select(e => e.Priority).ToArray());
            CollectionAssert.AreEqual(
                new[] { "빈손", "학의 소원", "종이 재벌", "종이접기 명인", "동네 종이접기 가게" },
                OrigamiCatalog.Endings.Select(e => e.DisplayName).ToArray());
            Assert.IsTrue(OrigamiCatalog.Endings.All(e => !string.IsNullOrEmpty(e.Text)));
            Assert.AreEqual(EndingIds.EmptyHanded, "E-01");
            Assert.AreEqual(EndingIds.CraneWish, "E-02");
            Assert.AreEqual(EndingIds.Tycoon, "E-03");
            Assert.AreEqual(EndingIds.Master, "E-04");
            Assert.AreEqual(EndingIds.LocalShop, "E-05");
        }

        [Test]
        public void FindEnding_KnownAndUnknown()
        {
            Assert.AreEqual("종이 재벌", OrigamiCatalog.FindEnding("E-03").DisplayName);
            Assert.IsNull(OrigamiCatalog.FindEnding("E-06"));
            Assert.IsNull(OrigamiCatalog.FindEnding(null));
        }
    }
}
