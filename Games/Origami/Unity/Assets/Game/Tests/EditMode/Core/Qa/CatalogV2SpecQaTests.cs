using System;
using System.Linq;
using NUnit.Framework;
using Origami.Core.Catalog;
using Origami.Core.Folding;
using Origami.Core.Recognition;

namespace Origami.Tests.Qa
{
    // QA ORIGAMI-009: v2 catalogue vs CONTENT_SPECS §1 (model table), §3 / F-007 AC-01 (book layout), §5 (popular days).
    public class CatalogV2SpecQaTests
    {
        // order, id, name, actions, folds, base, page, owned, difficulty, outline area, cover rotation
        static readonly object[][] Table =
        {
            new object[] { 1, "plane", "종이비행기", 6, 5, 6, 0, true, 2, 0.845, 0.0 },
            new object[] { 2, "house", "집", 5, 3, 8, 0, true, 1, 1.500, 0.0 },
            new object[] { 3, "tulip", "튤립", 5, 5, 12, 20, false, 2, 1.488, 45.0 },
            new object[] { 4, "dog", "강아지 얼굴", 6, 4, 18, 50, false, 3, 1.477, 0.0 },
            new object[] { 5, "cicada", "매미", 10, 8, 26, 150, false, 4, 0.690, 45.0 },
        };

        [TestCaseSource(nameof(Table))]
        public void Content1_ModelRow(int order, string id, string name, int actions, int folds, int basePrice, int pagePrice,
            bool owned, int difficulty, double area, double rot)
        {
            var m = ModelCatalog.Find(id);
            Assert.That(m, Is.Not.Null);
            Assert.That(ModelCatalog.Models[order - 1], Is.SameAs(m));
            Assert.That(ModelCatalog.IndexOf(id), Is.EqualTo(order - 1));
            Assert.That(m.Order, Is.EqualTo(order));
            Assert.That(m.DisplayName, Is.EqualTo(name));
            Assert.That(m.ActionCount, Is.EqualTo(actions));
            Assert.That(m.FoldCount, Is.EqualTo(folds));
            Assert.That(m.Steps.Count(s => s.Kind == FoldStepKind.Crease), Is.EqualTo(actions - folds));
            Assert.That(m.StepTexts.Count, Is.EqualTo(actions));
            Assert.That(m.BasePrice, Is.EqualTo(basePrice));
            Assert.That(m.PagePrice, Is.EqualTo(pagePrice));
            Assert.That(m.InitiallyOwned, Is.EqualTo(owned));
            Assert.That(m.Difficulty, Is.EqualTo(difficulty));
            Assert.That(m.CoverRotationDeg, Is.EqualTo(rot));
            Assert.That(Outline.FromState(ModelCatalog.ReferenceState(id)).Area, Is.EqualTo(area).Within(0.01));
            CollectionAssert.AreEqual(new[] { "plane", "house", "tulip", "dog", "cicada" }, ModelCatalog.Recognition.ModelIds);
        }

        [Test]
        public void Catalogue_UnknownIds()
        {
            Assert.That(ModelCatalog.Find("unicorn"), Is.Null);
            Assert.That(ModelCatalog.Find(null), Is.Null);
            Assert.That(ModelCatalog.IndexOf("Plane"), Is.EqualTo(-1), "IDs are lower-case, ordinal");
            Assert.Throws<ArgumentException>(() => ModelCatalog.ReferenceState("unicorn"));
            Assert.Throws<ArgumentOutOfRangeException>(() => ModelCatalog.StateBeforeStep("plane", 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => ModelCatalog.StateBeforeStep("plane", 8));
            Assert.That(ModelCatalog.StateBeforeStep("plane", 1).Facets.Count, Is.EqualTo(1));
        }

        // ---- F-007 AC-01 -------------------------------------------------------------------------------

        [Test]
        public void F007_AC01_PageLayoutAllPages()
        {
            Assert.That(BookLayout.PageCount, Is.EqualTo(44));
            Assert.That(BookLayout.KindOf(0), Is.EqualTo(BookPageKind.Contents));
            Assert.That(BookLayout.KindOf(1), Is.EqualTo(BookPageKind.Symbols));
            Assert.That(BookLayout.ModelAt(0), Is.Null);
            Assert.That(BookLayout.ModelAt(1), Is.Null);
            var ranges = new (string id, int cover, int complete)[] { ("plane", 2, 9), ("house", 10, 16), ("tulip", 17, 23), ("dog", 24, 31), ("cicada", 32, 43) };
            int expectNext = 2;
            foreach (var (id, cover, complete) in ranges)
            {
                Assert.That(cover, Is.EqualTo(expectNext));
                Assert.That(BookLayout.CoverPage(id), Is.EqualTo(cover), id);
                for (int p = cover; p <= complete; p++)
                {
                    Assert.That(BookLayout.ModelAt(p), Is.EqualTo(id), "page " + p);
                    var kind = p == cover ? BookPageKind.Cover : p == complete ? BookPageKind.Complete : BookPageKind.Step;
                    Assert.That(BookLayout.KindOf(p), Is.EqualTo(kind), "page " + p);
                    Assert.That(BookLayout.StepAt(p), Is.EqualTo(kind == BookPageKind.Step ? p - cover : 0), "page " + p);
                }
                Assert.That(complete - cover - 1, Is.EqualTo(ModelCatalog.Find(id).ActionCount));
                expectNext = complete + 1;
            }
            Assert.That(expectNext, Is.EqualTo(44));
            Assert.Throws<ArgumentOutOfRangeException>(() => BookLayout.KindOf(44));
            Assert.Throws<ArgumentOutOfRangeException>(() => BookLayout.KindOf(-1));
            Assert.Throws<ArgumentException>(() => BookLayout.CoverPage("unicorn"));
        }

        [TestCase(-1, 0)]
        [TestCase(0, 0)]
        [TestCase(1, 0)]
        [TestCase(17, 16)]
        [TestCase(20, 20)]
        [TestCase(31, 30)]
        [TestCase(43, 42)]
        [TestCase(99, 42)]
        [TestCase(int.MinValue, 0)]
        [TestCase(int.MaxValue, 42)]
        public void F007_ClampToSpread(int page, int expected) =>
            Assert.That(BookLayout.ClampToSpread(page), Is.EqualTo(expected));

        // ---- CONTENT §5 popular-by-day --------------------------------------------------------------

        [Test]
        public void Content5_PopularByDay()
        {
            var expected = new[] { "plane", "house", "tulip", "house", "dog", "tulip", "cicada" };
            CollectionAssert.AreEqual(expected, ModelCatalog.PopularByDay);
            for (int d = 1; d <= 7; d++) Assert.That(ModelCatalog.PopularModelId(d), Is.EqualTo(expected[d - 1]));
            Assert.Throws<ArgumentOutOfRangeException>(() => ModelCatalog.PopularModelId(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => ModelCatalog.PopularModelId(8));
        }

        // CONTENT §5 price table recomputed from the catalogue base prices (R-015 formula; GAME_RULES GRADE_PCT 150/100/60, popular 150).
        [Test]
        public void Content5_PriceTableFromCatalogueBasePrices()
        {
            var table = new[,]
            {
                { 9, 6, 3, 13, 9, 5 }, { 12, 8, 4, 18, 12, 7 }, { 18, 12, 7, 27, 18, 10 }, { 27, 18, 10, 40, 27, 16 }, { 39, 26, 15, 58, 39, 23 },
            };
            int[] gradePct = { 150, 100, 60 };
            for (int i = 0; i < 5; i++)
            {
                int b = ModelCatalog.Models[i].BasePrice;
                for (int c = 0; c < 6; c++)
                    Assert.That(b * gradePct[c % 3] * (c < 3 ? 100 : 150) / 10000, Is.EqualTo(table[i, c]), $"{ModelCatalog.Models[i].Id} col {c}");
            }
        }
    }
}
