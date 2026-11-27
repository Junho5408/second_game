using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Origami.Core.Book;
using Origami.Core.Catalog;
using Origami.Core.Folding;
using Origami.Core.Game;
using Origami.Core.Geometry;

namespace Origami.Tests.Book
{
    // C-160 BookPages — F-007 AC-02/AC-03 (owned by ORIGAMI-011), CONTENT §2/§3, D-112. Expected values are CONTENT/FEATURE literals.
    public class BookPagesTests
    {
        private const double Eps = 1e-6;     // paper coordinates
        private const double Lit = 1e-4;     // CONTENT 6-digit literals

        private static readonly string[] NewRunOwned = { "plane", "house" };
        private static readonly string[] AllOwned = { "plane", "house", "tulip", "dog", "cicada" };

        private static Vec2 V(double x, double y) => new Vec2(x, y);

        private static void Close(Vec2 expected, Vec2 actual, double tol, string what)
        {
            Assert.That(actual.X, Is.EqualTo(expected.X).Within(tol), what + " x of " + actual);
            Assert.That(actual.Y, Is.EqualTo(expected.Y).Within(tol), what + " y of " + actual);
        }

        private static bool Near(Vec2 a, Vec2 b, double tol) => Math.Abs(a.X - b.X) <= tol && Math.Abs(a.Y - b.Y) <= tol;

        /// <summary>Line end points compared without order (C-160 allows order-free comparison).</summary>
        private static void LineIs(StepDiagram d, Vec2 p, Vec2 q, double tol, string what)
        {
            bool same = Near(d.LineA, p, tol) && Near(d.LineB, q, tol);
            bool swapped = Near(d.LineA, q, tol) && Near(d.LineB, p, tol);
            Assert.IsTrue(same || swapped, what + ": line " + d.LineA + "–" + d.LineB + " expected " + p + "–" + q);
        }

        // ------------------------------------------------------------------------------------------------ F-007 AC-02

        [Test]
        public void F007_AC02_Plane6IsStep4Diagram()
        {
            var page = BookPages.Get(6, NewRunOwned);
            Assert.AreEqual(6, page.Index);
            Assert.AreEqual(BookPageKind.Step, page.Kind);
            Assert.AreEqual("plane", page.ModelId);
            Assert.IsFalse(page.Sealed);
            Assert.AreEqual(4, page.StepNumber);
            Assert.AreEqual(6, page.StepTotal);
            Assert.AreEqual("4 / 6", page.StepLabel);
            Assert.IsNull(page.Cover);
            Assert.IsNull(page.CompleteShape);
            Assert.AreEqual(0, page.Contents.Count);

            var d = page.Diagram;
            Assert.IsNotNull(d);
            // before = plane after step 3: 2 folds + 1 crease (D-103: 3 facets, 1 crease segment)
            Assert.AreEqual(2, d.BeforeFolds);
            Assert.AreEqual(1, d.BeforeCreases);
            Assert.AreEqual(3, d.Before.Facets.Count);
            Assert.AreEqual(1, d.Before.Creases.Count);
            // after = plane after step 4 (D-103: 5 facets)
            Assert.AreEqual(5, d.After.Facets.Count);
            // line (0,1)–(−0.828427,−1), arrow (−1,0) → (0, 1 − √2), valley fold, all layers
            LineIs(d, V(0, 1), V(-0.828427, -1), Lit, "plane 4");
            Assert.AreEqual(FoldStepKind.Fold, d.Step.Kind);
            Close(V(-1, 0), d.Step.Grab, Eps, "grab");
            Close(V(0, -0.4142), d.Step.Drop, Lit, "drop");
            Assert.IsFalse(d.Step.Mountain);
            Assert.AreEqual(0, d.Step.Layers);
            Assert.AreEqual("왼쪽 비스듬한 변을 가운데 자국에 맞춰 접어요. 접는 선은 위 꼭짓점에서 시작해요.", d.Text);
            Assert.AreEqual(SnapKind.Mark, d.Magnet);     // CONTENT §2 plane 4 "M"
            Assert.AreEqual(0, d.Anchors.Count);
        }

        [Test]
        public void F007_AC02_Plane3IsCreaseStep()
        {
            var page = BookPages.Get(3, NewRunOwned);
            Assert.AreEqual(1, page.StepNumber);
            Assert.AreEqual("1 / 6", page.StepLabel);
            Assert.AreEqual(FoldStepKind.Crease, page.Diagram.Step.Kind);
            Assert.AreEqual(0, page.Diagram.BeforeFolds);
            Assert.AreEqual(0, page.Diagram.BeforeCreases);
            Assert.AreSame(PaperState.Initial, page.Diagram.Before);
            LineIs(page.Diagram, V(0, -1), V(0, 1), Eps, "plane 1");
            Assert.AreEqual(
                "왼쪽 변을 오른쪽 변에 맞춰 반으로 접었다가 펴요. 가운데 세로 자국이 생겨요. (Ctrl을 누른 채 놓기)", page.Diagram.Text);
        }

        [Test]
        public void F007_AC02_Tulip21IsTopTwoLayers()
        {
            var page = BookPages.Get(21, AllOwned);
            Assert.AreEqual("tulip", page.ModelId);
            Assert.AreEqual(4, page.StepNumber);
            Assert.AreEqual("4 / 5", page.StepLabel);
            Assert.AreEqual(2, page.Diagram.Step.Layers);
            Assert.AreEqual(FoldStepKind.Fold, page.Diagram.Step.Kind);
            LineIs(page.Diagram, V(0, -1), V(-0.428572, 0), Lit, "tulip 4");
            Assert.AreEqual(SnapKind.None, page.Diagram.Magnet);
        }

        // ------------------------------------------------------------------------------------------------ F-007 AC-03

        [Test]
        public void F007_AC03_NewRunTulipStepIsSealed()
        {
            var page = BookPages.Get(18, NewRunOwned);
            Assert.AreEqual(BookPageKind.Step, page.Kind);
            Assert.AreEqual("tulip", page.ModelId);
            Assert.IsTrue(page.Sealed);
            Assert.IsNull(page.Diagram);
            Assert.AreEqual(1, page.StepNumber);     // the step number stays (assumption: only the diagram is sealed)
            Assert.AreEqual(5, page.StepTotal);
            Assert.AreEqual("1 / 5", page.StepLabel);

            var complete = BookPages.Get(23, NewRunOwned);
            Assert.AreEqual(BookPageKind.Complete, complete.Kind);
            Assert.IsTrue(complete.Sealed);
            Assert.IsNull(complete.CompleteShape);
        }

        [Test]
        public void F007_AC03_NewRunTulipCoverShowsNameSilhouettePriceAndBuy()
        {
            var page = BookPages.Get(17, NewRunOwned);
            Assert.AreEqual(BookPageKind.Cover, page.Kind);
            Assert.IsFalse(page.Sealed);
            Assert.IsNull(page.Diagram);
            Assert.AreEqual(0, page.StepNumber);
            Assert.AreEqual("", page.StepLabel);
            var c = page.Cover;
            Assert.IsNotNull(c);
            Assert.AreEqual("tulip", c.ModelId);
            Assert.AreEqual("튤립", c.Name);
            Assert.IsNotNull(c.Silhouette);
            Assert.AreSame(ModelCatalog.ReferenceState("tulip"), c.Silhouette);
            Assert.AreEqual(20, c.PagePrice);
            Assert.AreEqual(12, c.BasePrice);
            Assert.AreEqual(2, c.Difficulty);
            Assert.AreEqual(5, c.ActionCount);
            Assert.AreEqual(45.0, c.RotationDeg, 1e-12);
            Assert.IsFalse(string.IsNullOrEmpty(c.Techniques));
            Assert.IsFalse(c.Owned);
            Assert.IsTrue(c.Buyable);
        }

        [Test]
        public void F007_AC03_AfterBuyingTulipStepHasDiagram()
        {
            var game = new OrigamiGame();
            Assert.IsTrue(game.Interact(InteractTargets.Bookshelf).Accepted);
            Assert.IsTrue(BookPages.Get(18, game.Snapshot.OwnedModelIds).Sealed);
            Assert.IsTrue(game.BuyPage("tulip").Accepted);

            var page = BookPages.Get(18, game.Snapshot.OwnedModelIds);
            Assert.IsFalse(page.Sealed);
            Assert.IsNotNull(page.Diagram);
            Assert.AreEqual(1, page.Diagram.StepNumberCheck());
            var cover = BookPages.Get(17, game.Snapshot.OwnedModelIds).Cover;
            Assert.IsTrue(cover.Owned);
            Assert.IsFalse(cover.Buyable);
            Assert.AreSame(ModelCatalog.ReferenceState("tulip"), BookPages.Get(23, game.Snapshot.OwnedModelIds).CompleteShape);
        }

        [Test]
        public void F007_AC03_SymbolsPageAlwaysVisible()
        {
            foreach (var owned in new[] { new string[0], NewRunOwned, AllOwned })
            {
                var page = BookPages.Get(1, owned);
                Assert.AreEqual(BookPageKind.Symbols, page.Kind);
                Assert.IsFalse(page.Sealed);
                Assert.IsNull(page.ModelId);
                Assert.IsNull(page.Diagram);
                Assert.IsNull(page.Cover);
                Assert.IsNull(page.CompleteShape);
                Assert.AreEqual(0, page.Contents.Count);
                Assert.AreEqual("", page.StepLabel);
            }
        }

        // ------------------------------------------------------------------------------------------------ contents, covers

        [Test]
        public void ContentsPage_ListsFiveModelsWithOwnershipPriceAndCover()
        {
            var page = BookPages.Get(0, NewRunOwned);
            Assert.AreEqual(BookPageKind.Contents, page.Kind);
            Assert.IsFalse(page.Sealed);
            Assert.IsNull(page.ModelId);
            var expected = new[]
            {
                ("plane", "종이비행기", true, 0, 2), ("house", "집", true, 0, 10), ("tulip", "튤립", false, 20, 17),
                ("dog", "강아지 얼굴", false, 50, 24), ("cicada", "매미", false, 150, 32),
            };
            Assert.AreEqual(5, page.Contents.Count);
            for (int i = 0; i < expected.Length; i++)
            {
                var e = page.Contents[i];
                Assert.AreEqual(expected[i].Item1, e.ModelId);
                Assert.AreEqual(expected[i].Item2, e.Name);
                Assert.AreEqual(expected[i].Item3, e.Owned, e.ModelId);
                Assert.AreEqual(expected[i].Item4, e.PagePrice, e.ModelId);
                Assert.AreEqual(expected[i].Item5, e.CoverPage, e.ModelId);
            }
            Assert.IsTrue(BookPages.Get(0, AllOwned).Contents.All(e => e.Owned));
        }

        [Test]
        public void Covers_MatchContentTable()
        {
            // CONTENT §1: (cover page, name, difficulty, actions, base, page price, rotation)
            var expected = new[]
            {
                (2, "plane", "종이비행기", 2, 6, 6, 0, 0.0), (10, "house", "집", 1, 5, 8, 0, 0.0), (17, "tulip", "튤립", 2, 5, 12, 20, 45.0),
                (24, "dog", "강아지 얼굴", 3, 6, 18, 50, 0.0), (32, "cicada", "매미", 4, 10, 26, 150, 45.0),
            };
            foreach (var (p, id, name, diff, actions, basePrice, pagePrice, rot) in expected)
            {
                var c = BookPages.Get(p, NewRunOwned).Cover;
                Assert.AreEqual(id, c.ModelId);
                Assert.AreEqual(name, c.Name);
                Assert.AreEqual(diff, c.Difficulty, id);
                Assert.AreEqual(actions, c.ActionCount, id);
                Assert.AreEqual(basePrice, c.BasePrice, id);
                Assert.AreEqual(pagePrice, c.PagePrice, id);
                Assert.AreEqual(rot, c.RotationDeg, 1e-12, id);
                bool owned = id == "plane" || id == "house";
                Assert.AreEqual(owned, c.Owned, id);
                Assert.AreEqual(!owned, c.Buyable, id);
                Assert.AreSame(ModelCatalog.ReferenceState(id), c.Silhouette, id);
            }
        }

        [Test]
        public void CompletePages_ShowReferenceStateWhenOwned()
        {
            foreach (var (p, id) in new[] { (9, "plane"), (16, "house"), (23, "tulip"), (31, "dog"), (43, "cicada") })
            {
                var page = BookPages.Get(p, AllOwned);
                Assert.AreEqual(BookPageKind.Complete, page.Kind, id);
                Assert.AreEqual(id, page.ModelId);
                Assert.IsFalse(page.Sealed);
                Assert.AreSame(ModelCatalog.ReferenceState(id), page.CompleteShape, id);
                Assert.AreEqual(0, page.StepNumber);
                Assert.AreEqual(0, page.StepTotal);
                Assert.AreEqual("", page.StepLabel);
                Assert.IsNull(page.Diagram);
                Assert.IsNull(page.Cover);
            }
        }

        // ------------------------------------------------------------------------------------------------ every diagram (CONTENT §2)

        // (model, cover, kinds C/F, facets before each step from D-103, line ends, magnet P/L/M/-)
        private static readonly string[] Kinds = { "CFFFFF", "FCCFF", "FFFFF", "FCCFFF", "FFFCCFFFFF" };
        private static readonly string[] Ids = { "plane", "house", "tulip", "dog", "cicada" };
        private static readonly int[] CoverPages = { 2, 10, 17, 24, 32 };
        private static readonly int[][] FacetsBefore =
        {
            new[] { 1, 1, 2, 3, 5, 7 },
            new[] { 1, 2, 2, 2, 4 },
            new[] { 1, 2, 4, 6, 8 },
            new[] { 1, 2, 2, 2, 4, 6 },
            new[] { 1, 2, 4, 6, 6, 6, 8, 10, 12, 20 },
        };
        private static readonly string[] Magnets = { "PMPMPP", "PPPLL", "PPP-L", "PPP--P", "PPPPP-LPPP" };

        private static readonly double[][][] Lines =
        {
            new[] { L(0, -1, 0, 1), L(-1, 0, 0, 1), L(0, 1, 1, 0), L(-0.828427, -1, 0, 1), L(0, 1, 0.828427, -1), L(0, -1, 0, 1) },
            new[] { L(-1, 0, 1, 0), L(0, -1, 0, 0), L(1, -0.5, -1, -0.5), L(-1, -0.5, 0, 0), L(0, 0, 1, -0.5) },
            new[] { L(-1, 1, 1, -1), L(-1, 0, 0, 0), L(0, 0, 0, -1), L(0, -1, -0.428572, 0), L(0.386473, -0.594203, -1, 0) },
            new[] { L(-1, 1, 1, -1), L(-1, -1, 0, 0), L(0, -1, -1, 0), L(-1, 0.42, -0.033333, 0.033333), L(0.033333, -0.033333, 0.42, -1), L(-0.5, -1, -1, -0.5) },
            new[]
            {
                L(-1, 1, 1, -1), L(-1, 0, 0, 0), L(0, 0, 0, -1), L(-1, -1, 0, 0), L(-0.5, -1, -0.5, 0), L(0, -1, -0.612903, 0),
                L(0.150257, -0.704996, -1, 0), L(-0.38, -1, -1, -0.38), L(-0.94, -0.44, -0.139167, 0.360833), L(0.360833, -0.139167, -0.44, -0.94),
            },
        };

        private static double[] L(double ax, double ay, double bx, double by) => new[] { ax, ay, bx, by };

        [Test]
        public void AllStepDiagrams_MatchContentSection2()
        {
            for (int m = 0; m < Ids.Length; m++)
            {
                string id = Ids[m];
                var model = ModelCatalog.Find(id);
                int n = Kinds[m].Length;
                for (int step = 1; step <= n; step++)
                {
                    string what = id + " step " + step;
                    var page = BookPages.Get(CoverPages[m] + step, AllOwned);
                    Assert.AreEqual(BookPageKind.Step, page.Kind, what);
                    Assert.AreEqual(id, page.ModelId, what);
                    Assert.AreEqual(step, page.StepNumber, what);
                    Assert.AreEqual(n, page.StepTotal, what);
                    Assert.AreEqual(step + " / " + n, page.StepLabel, what);
                    var d = page.Diagram;
                    Assert.IsNotNull(d, what);

                    string prefix = Kinds[m].Substring(0, step - 1);
                    Assert.AreEqual(prefix.Count(c => c == 'F'), d.BeforeFolds, what);
                    Assert.AreEqual(prefix.Count(c => c == 'C'), d.BeforeCreases, what);
                    Assert.AreEqual(Kinds[m][step - 1] == 'C' ? FoldStepKind.Crease : FoldStepKind.Fold, d.Step.Kind, what);
                    Assert.AreEqual(FacetsBefore[m][step - 1], d.Before.Facets.Count, what + " facets before");
                    Assert.AreSame(ModelCatalog.StateBeforeStep(id, step), d.Before, what);
                    Assert.AreSame(ModelCatalog.StateBeforeStep(id, step + 1), d.After, what);
                    Assert.AreSame(model.Steps[step - 1], d.Step, what);
                    Assert.AreEqual(model.StepTexts[step - 1], d.Text, what);
                    Assert.IsFalse(string.IsNullOrEmpty(d.Text), what);

                    var l = Lines[m][step - 1];
                    LineIs(d, V(l[0], l[1]), V(l[2], l[3]), Lit, what);

                    char mag = Magnets[m][step - 1];
                    var kind = mag == 'P' ? SnapKind.Point : mag == 'L' ? SnapKind.Line : mag == 'M' ? SnapKind.Mark : SnapKind.None;
                    Assert.AreEqual(kind, d.Magnet, what + " magnet");
                    Assert.AreEqual(mag == 'P' ? 1 : mag == 'L' ? 2 : 0, d.Anchors.Count, what + " anchors");
                    // an exact P input is not moved by the magnet (R-021), so the anchor is the drop point
                    if (mag == 'P') Close(d.Step.Drop, d.Anchors[0], Lit, what + " anchor");
                }
                Assert.AreSame(model.Steps[n - 1], BookPages.Get(CoverPages[m] + n, AllOwned).Diagram.Step);
                Assert.AreSame(ModelCatalog.ReferenceState(id), BookPages.Get(CoverPages[m] + n, AllOwned).Diagram.After, id);
            }
        }

        [Test]
        public void LineMagnetAnchors_AreTheTwoLandmarksNamedByTheBookText()
        {
            // house 4/5: the roof line passes the top end of the vertical crease (0,0) and the side end of the horizontal crease.
            // tulip 5: the upper-left corner (−1,0) and where the first petal line meets the diagonal (−0.3,−0.3).
            // cicada 7: (−1,0) and where the first wing line meets the diagonal crease (−0.38,−0.38).
            AnchorsAre(14, V(-1, -0.5), V(0, 0));
            AnchorsAre(15, V(0, 0), V(1, -0.5));
            AnchorsAre(22, V(-1, 0), V(-0.3, -0.3));
            AnchorsAre(39, V(-1, 0), V(-0.38, -0.38));
        }

        private static void AnchorsAre(int page, Vec2 p, Vec2 q)
        {
            var d = BookPages.Get(page, AllOwned).Diagram;
            Assert.AreEqual(SnapKind.Line, d.Magnet, "page " + page);
            Assert.AreEqual(2, d.Anchors.Count);
            bool ok = (Near(d.Anchors[0], p, Lit) && Near(d.Anchors[1], q, Lit)) || (Near(d.Anchors[0], q, Lit) && Near(d.Anchors[1], p, Lit));
            Assert.IsTrue(ok, "page " + page + ": " + d.Anchors[0] + ", " + d.Anchors[1]);
        }

        // ------------------------------------------------------------------------------------------------ every page, arguments

        [Test]
        public void EveryPage_KindModelAndSealFollowLayoutAndOwnership()
        {
            for (int p = 0; p < 44; p++)
            {
                var page = BookPages.Get(p, NewRunOwned);
                Assert.AreEqual(p, page.Index);
                Assert.AreEqual(BookLayout.KindOf(p), page.Kind, "page " + p);
                Assert.AreEqual(BookLayout.ModelAt(p), page.ModelId, "page " + p);
                bool owned = page.ModelId == "plane" || page.ModelId == "house";
                bool sealable = page.Kind == BookPageKind.Step || page.Kind == BookPageKind.Complete;
                Assert.AreEqual(sealable && !owned, page.Sealed, "page " + p);
                Assert.AreEqual(page.Kind == BookPageKind.Step && owned, page.Diagram != null, "page " + p);
                Assert.AreEqual(page.Kind == BookPageKind.Cover, page.Cover != null, "page " + p);
                Assert.AreEqual(page.Kind == BookPageKind.Complete && owned, page.CompleteShape != null, "page " + p);
                Assert.AreEqual(p == 0 ? 5 : 0, page.Contents.Count, "page " + p);
                Assert.IsNotNull(page.StepLabel);
            }
        }

        [Test]
        public void NoPagesOwned_SealsEveryModelStepButNotCoversOrFixedPages()
        {
            var none = new List<string>();
            Assert.IsTrue(BookPages.Get(3, none).Sealed);
            Assert.IsTrue(BookPages.Get(11, none).Sealed);
            Assert.IsFalse(BookPages.Get(2, none).Sealed);
            Assert.IsTrue(BookPages.Get(2, none).Cover.Buyable);
            Assert.IsFalse(BookPages.Get(0, none).Sealed);
        }

        [Test]
        public void OwnedList_IgnoresNullUnknownAndDuplicateIdsAndIsOrdinal()
        {
            var owned = new[] { null, "unicorn", "", "Tulip", "tulip", "tulip" };
            Assert.IsFalse(BookPages.Get(18, owned).Sealed);
            Assert.IsTrue(BookPages.Get(3, owned).Sealed, "plane not in the list");
            Assert.IsTrue(BookPages.Get(25, owned).Sealed, "dog not in the list");
        }

        [Test]
        public void Get_IsDeterministic()
        {
            var a = BookPages.Get(39, AllOwned);
            var b = BookPages.Get(39, AllOwned);
            Assert.AreEqual(a.Diagram.LineA, b.Diagram.LineA);
            Assert.AreEqual(a.Diagram.LineB, b.Diagram.LineB);
            Assert.AreEqual(a.Diagram.Magnet, b.Diagram.Magnet);
            CollectionAssert.AreEqual(a.Diagram.Anchors, b.Diagram.Anchors);
            Assert.AreSame(a.Diagram.Before, b.Diagram.Before);
        }

        [Test]
        public void Lists_AreReadOnly()
        {
            var contents = BookPages.Get(0, AllOwned).Contents;
            Assert.Throws<NotSupportedException>(() => ((IList<ContentsEntry>)contents).Add(null));
            var anchors = BookPages.Get(5, AllOwned).Diagram.Anchors;
            Assert.Throws<NotSupportedException>(() => ((IList<Vec2>)anchors).Add(default));
        }

        [TestCase(-1)]
        [TestCase(44)]
        [TestCase(int.MinValue)]
        [TestCase(int.MaxValue)]
        public void Get_PageOutOfRangeThrows(int page)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => BookPages.Get(page, AllOwned));
        }

        [Test]
        public void Get_NullOwnedThrows()
        {
            Assert.Throws<ArgumentNullException>(() => BookPages.Get(0, null));
            Assert.Throws<ArgumentNullException>(() => BookPages.Get(6, null));
        }
    }

    internal static class StepDiagramTestExtensions
    {
        /// <summary>1-based step number of the diagram's step inside its model (diagram ↔ catalogue wiring).</summary>
        public static int StepNumberCheck(this StepDiagram d)
        {
            foreach (var m in ModelCatalog.Models)
                for (int i = 0; i < m.Steps.Count; i++)
                    if (ReferenceEquals(m.Steps[i], d.Step)) return i + 1;
            return 0;
        }
    }
}
