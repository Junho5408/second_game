using System;
using NUnit.Framework;
using Origami.Core.Catalog;

namespace Origami.Tests.Catalog
{
    // C-120 BookLayout — F-007 AC-01 (owned by ORIGAMI-009), D-112, R-010.
    public class BookLayoutTests
    {
        [Test]
        public void F007_AC01_PageLayout()
        {
            Assert.AreEqual(44, BookLayout.PageCount);
            Assert.AreEqual(0, BookLayout.ContentsPage);
            Assert.AreEqual(1, BookLayout.SymbolsPage);
            Assert.AreEqual(BookPageKind.Contents, BookLayout.KindOf(0));
            Assert.AreEqual(BookPageKind.Symbols, BookLayout.KindOf(1));
            Assert.IsNull(BookLayout.ModelAt(0));
            Assert.IsNull(BookLayout.ModelAt(1));
            Assert.AreEqual(0, BookLayout.StepAt(0));
            Assert.AreEqual(0, BookLayout.StepAt(1));

            var ranges = new[] { ("plane", 2, 9), ("house", 10, 16), ("tulip", 17, 23), ("dog", 24, 31), ("cicada", 32, 43) };
            foreach (var (id, cover, complete) in ranges)
            {
                Assert.AreEqual(cover, BookLayout.CoverPage(id), id);
                Assert.AreEqual(BookPageKind.Cover, BookLayout.KindOf(cover), id);
                Assert.AreEqual(0, BookLayout.StepAt(cover), id);
                Assert.AreEqual(BookPageKind.Complete, BookLayout.KindOf(complete), id);
                Assert.AreEqual(0, BookLayout.StepAt(complete), id);
                int n = ModelCatalog.Find(id).ActionCount;
                Assert.AreEqual(complete, cover + n + 1, id);
                for (int p = cover; p <= complete; p++) Assert.AreEqual(id, BookLayout.ModelAt(p), id + " page " + p);
                for (int step = 1; step <= n; step++)
                {
                    Assert.AreEqual(BookPageKind.Step, BookLayout.KindOf(cover + step), id + " step " + step);
                    Assert.AreEqual(step, BookLayout.StepAt(cover + step), id + " step " + step);
                }
            }
            // CONTENT §3 / F-007 AC-02: plane 6 = step 4, tulip 21 = step 4
            Assert.AreEqual(4, BookLayout.StepAt(6));
            Assert.AreEqual("tulip", BookLayout.ModelAt(21));
            Assert.AreEqual(4, BookLayout.StepAt(21));
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
        public void ClampToSpread(int page, int expected)
        {
            Assert.AreEqual(expected, BookLayout.ClampToSpread(page));
        }

        [Test]
        public void ArgumentChecks()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => BookLayout.KindOf(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => BookLayout.KindOf(44));
            Assert.Throws<ArgumentOutOfRangeException>(() => BookLayout.ModelAt(44));
            Assert.Throws<ArgumentOutOfRangeException>(() => BookLayout.StepAt(-1));
            Assert.Throws<ArgumentException>(() => BookLayout.CoverPage("unicorn"));
            Assert.Throws<ArgumentException>(() => BookLayout.CoverPage(null));
        }
    }
}
