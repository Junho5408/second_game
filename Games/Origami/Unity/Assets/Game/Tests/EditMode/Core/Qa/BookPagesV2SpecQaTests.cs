using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Origami.Core.Book;
using Origami.Core.Catalog;
using Origami.Core.Folding;
using Origami.Core.Game;
using Origami.Core.Geometry;

namespace Origami.Tests.Qa
{
    /// <summary>
    /// QA ORIGAMI-011: black-box checks of C-160 <see cref="BookPages"/> against R-010, F-007 AC-01~03, CONTENT §1~§3 and
    /// DATA_STRUCTURE D-103/D-110/D-112. Expected values are typed by QA from the specs (<see cref="V2SpecBook"/> for §2 and the
    /// literal tables below), not taken from ModelCatalog.
    /// </summary>
    public class BookPagesV2SpecQaTests
    {
        // ---------------------------------------------------------------------------------------- spec tables (QA transcription)

        // CONTENT §1 / D-110: name, base price, page price, initially owned, difficulty, action count, cover rotation.
        private static readonly (string id, string name, int basePrice, int pagePrice, bool initial, int difficulty, int actions, double rot)[] Models =
        {
            ("plane", "종이비행기", 6, 0, true, 2, 6, 0),
            ("house", "집", 8, 0, true, 1, 5, 0),
            ("tulip", "튤립", 12, 20, false, 2, 5, 45),
            ("dog", "강아지 얼굴", 18, 50, false, 3, 6, 0),
            ("cicada", "매미", 26, 150, false, 4, 10, 45),
        };

        // D-112 cover pages.
        private static readonly Dictionary<string, int> Covers = new Dictionary<string, int>
        {
            ["plane"] = 2, ["house"] = 10, ["tulip"] = 17, ["dog"] = 24, ["cicada"] = 32,
        };

        // CONTENT §2 "책 설명" column, verbatim.
        private static readonly Dictionary<string, string[]> StepTexts = new Dictionary<string, string[]>
        {
            ["plane"] = new[]
            {
                "왼쪽 변을 오른쪽 변에 맞춰 반으로 접었다가 펴요. 가운데 세로 자국이 생겨요. (Ctrl을 누른 채 놓기)",
                "왼쪽 위 모서리를 가운데 자국 위, 종이 한가운데로 접어요.",
                "오른쪽 위 모서리도 같은 점(왼쪽 모서리 끝)으로 접어요.",
                "왼쪽 비스듬한 변을 가운데 자국에 맞춰 접어요. 접는 선은 위 꼭짓점에서 시작해요.",
                "오른쪽도 똑같이. 모서리를 왼쪽 모서리 끝에 맞춰요.",
                "가운데 자국을 따라 반으로 접어요.",
            },
            ["house"] = new[]
            {
                "위쪽 변을 아래쪽 변에 맞춰 반으로 접어요. 가로로 긴 직사각형이 돼요.",
                "왼쪽 변을 오른쪽 변에 맞춰 접었다 펴서 가운데 세로 자국을 만들어요(두 겹 함께).",
                "아래쪽 변을 위쪽 변에 맞춰 접었다 펴서 가로 자국을 만들어요.",
                "왼쪽 위 모서리를 접어 내려요. 접는 선이 위쪽 변의 세로 자국 끝과 왼쪽 변의 가로 자국 끝을 지나요.",
                "오른쪽도 똑같이. 지붕 완성!",
            },
            ["tulip"] = new[]
            {
                "대각선으로 반 접어 삼각형을 만들어요.",
                "왼쪽 위 꼭짓점을 직각 꼭짓점에 맞춰 접어요.",
                "오른쪽 꼭짓점도 같은 점으로. 작은 정사각형(다이아몬드 기본형)이 돼요.",
                "맨 위 덮개(2겹)만 집어서 끝을 오른쪽 바깥으로 접어 꽃잎을 만들어요. 접는 선은 오른쪽 아래 꼭짓점에서 가운데 대각선 위 (−0.3,−0.3)(꼭짓점 C에서 대각선을 따라 9.9cm)을 지나요. 아래 바닥은 그대로!",
                "남은 덮개(2겹)도 똑같이. 접는 선은 왼쪽 위 꼭짓점과, 첫 꽃잎 접는 선이 대각선과 만난 점을 지나요.",
            },
            ["dog"] = new[]
            {
                "대각선으로 반 접어 삼각형을 만들어요. 긴 변이 강아지 머리 윗선이에요.",
                "삼각형을 반으로 접었다 펴서 가운데 자국을 만들어요.",
                "아래 꼭짓점을 긴 변 가운데에 맞춰 접었다 펴서 가로 자국을 만들어요.",
                "왼쪽 꼭짓점을 바깥 아래로 접어 귀를 만들어요. 접는 선은 짧은 변 위 꼭짓점에서 5.8cm 지점부터 긴 변 가운데 바로 옆(0.5cm)까지.",
                "오른쪽 귀도 똑같이.",
                "아래 꼭짓점을 두 자국이 만나는 점까지 접어 올려 턱을 만들어요.",
            },
            ["cicada"] = new[]
            {
                "대각선으로 반 접어 삼각형.",
                "왼쪽 위 꼭짓점을 직각 꼭짓점 C에 맞춰요.",
                "오른쪽 꼭짓점도 C로. 다이아몬드 기본형.",
                "다이아몬드를 대각선으로 반 접었다 펴서 몸통 가운데 자국을 만들어요.",
                "왼쪽 변을 오른쪽 변에 맞춰 접었다 펴서 세로 자국을 만들어요. 두 자국이 가운데 (−0.5,−0.5)에서 만나요(9·10단계 기준점).",
                "맨 위 덮개(2겹)만 집어 끝 C를 오른쪽 바깥으로 접어 날개를 만들어요. 접는 선은 오른쪽 아래 꼭짓점에서 대각선 자국 위 (−0.38,−0.38)(C에서 대각선을 따라 8.8cm)을 지나요. 아래 바닥은 그대로!",
                "남은 덮개(2겹)로 반대쪽 날개. 접는 선은 왼쪽 위 꼭짓점과, 첫 날개 선이 대각선 자국과 만난 점을 지나요.",
                "머리: 바닥에 남은 C 끝을 두 날개 선이 만난 점까지 접어 올려요.",
                "왼쪽 옆 꼭짓점을 뒤로 접어 두 자국이 만나는 가운데 점에 맞춰요.",
                "오른쪽 옆도 뒤로 같은 점에 맞춰요. 매미 완성!",
            },
        };

        private const double Tol = 1e-6;

        private static readonly string[] Buyable = { "tulip", "dog", "cicada" };

        /// <summary>All 8 ownership combinations of the buyable models (plane/house always owned in a run, R-002).</summary>
        private static IEnumerable<string[]> OwnershipCombos()
        {
            for (int mask = 0; mask < 8; mask++)
            {
                var owned = new List<string> { "plane", "house" };
                for (int b = 0; b < 3; b++)
                    if ((mask & (1 << b)) != 0) owned.Add(Buyable[b]);
                yield return owned.ToArray();
            }
        }

        /// <summary>Independent expected layout (D-112): (kind, model, step) for page p.</summary>
        private static (BookPageKind kind, string model, int step) Expected(int p)
        {
            if (p == 0) return (BookPageKind.Contents, null, 0);
            if (p == 1) return (BookPageKind.Symbols, null, 0);
            foreach (var m in Models)
            {
                int c = Covers[m.id];
                if (p == c) return (BookPageKind.Cover, m.id, 0);
                if (p > c && p <= c + m.actions) return (BookPageKind.Step, m.id, p - c);
                if (p == c + m.actions + 1) return (BookPageKind.Complete, m.id, 0);
            }
            throw new InvalidOperationException("page " + p);
        }

        private static PaperState StateAfter(string id, int actions) => V2SpecBook.Play(V2SpecBook.Steps(id).Take(actions)).State;

        private static void SameSegmentUnordered(Vec2 a, Vec2 b, Vec2 ea, Vec2 eb, double tol, string ctx)
        {
            bool direct = Near(a, ea, tol) && Near(b, eb, tol);
            bool swapped = Near(a, eb, tol) && Near(b, ea, tol);
            Assert.That(direct || swapped, Is.True, $"{ctx}: line ({a.X},{a.Y})–({b.X},{b.Y}) expected ({ea.X},{ea.Y})–({eb.X},{eb.Y})");
        }

        private static bool Near(Vec2 a, Vec2 b, double tol) => Math.Abs(a.X - b.X) <= tol && Math.Abs(a.Y - b.Y) <= tol;

        private static double DistToLine(Vec2 p, Vec2 a, Vec2 b)
        {
            double dx = b.X - a.X, dy = b.Y - a.Y;
            return Math.Abs((p.X - a.X) * dy - (p.Y - a.Y) * dx) / Math.Sqrt(dx * dx + dy * dy);
        }

        private static SnapKind MagnetKind(char c)
        {
            switch (c)
            {
                case 'P': return SnapKind.Point;
                case 'L': return SnapKind.Line;
                case 'M': return SnapKind.Mark;
                default: return SnapKind.None;
            }
        }

        // ------------------------------------------------------------------------------------------------ tests

        [Test]
        public void AllPages_AllOwnershipCombos_MatchLayoutSealingAndPayloads()
        {
            int checkedPages = 0;
            foreach (var owned in OwnershipCombos())
            {
                var ownedSet = new HashSet<string>(owned);
                string combo = string.Join(",", owned);
                for (int p = 0; p < 44; p++)
                {
                    var e = Expected(p);
                    var page = BookPages.Get(p, owned);
                    string ctx = $"[{combo}] page {p}";
                    Assert.That(page, Is.Not.Null, ctx);
                    Assert.That(page.Index, Is.EqualTo(p), ctx);
                    Assert.That(page.Kind, Is.EqualTo(e.kind), ctx + " kind");
                    Assert.That(page.ModelId, Is.EqualTo(e.model), ctx + " model");
                    bool isOwned = e.model != null && ownedSet.Contains(e.model);
                    bool sealedExpected = (e.kind == BookPageKind.Step || e.kind == BookPageKind.Complete) && !isOwned;
                    Assert.That(page.Sealed, Is.EqualTo(sealedExpected), ctx + " sealed");

                    // Step fields
                    if (e.kind == BookPageKind.Step)
                    {
                        int total = Models.First(m => m.id == e.model).actions;
                        Assert.That(page.StepNumber, Is.EqualTo(e.step), ctx + " step number");
                        Assert.That(page.StepTotal, Is.EqualTo(total), ctx + " step total");
                        Assert.That(page.StepLabel, Is.EqualTo(e.step + " / " + total), ctx + " step label");
                        if (isOwned) Assert.That(page.Diagram, Is.Not.Null, ctx + " diagram");
                        else Assert.That(page.Diagram, Is.Null, ctx + " sealed page must have no diagram");
                    }
                    else
                    {
                        Assert.That(page.StepNumber, Is.EqualTo(0), ctx);
                        Assert.That(page.StepTotal, Is.EqualTo(0), ctx);
                        Assert.That(page.StepLabel, Is.EqualTo(""), ctx);
                        Assert.That(page.Diagram, Is.Null, ctx);
                    }

                    // Cover (never sealed)
                    if (e.kind == BookPageKind.Cover)
                    {
                        var m = Models.First(x => x.id == e.model);
                        var c = page.Cover;
                        Assert.That(c, Is.Not.Null, ctx + " cover");
                        Assert.That(c.ModelId, Is.EqualTo(m.id), ctx);
                        Assert.That(c.Name, Is.EqualTo(m.name), ctx + " name");
                        Assert.That(c.BasePrice, Is.EqualTo(m.basePrice), ctx + " base price");
                        Assert.That(c.PagePrice, Is.EqualTo(m.pagePrice), ctx + " page price");
                        Assert.That(c.Difficulty, Is.EqualTo(m.difficulty), ctx + " difficulty");
                        Assert.That(c.ActionCount, Is.EqualTo(m.actions), ctx + " action count");
                        Assert.That(c.RotationDeg, Is.EqualTo(m.rot).Within(1e-9), ctx + " rotation");
                        Assert.That(c.Techniques, Is.Not.Null.And.Not.Empty, ctx + " techniques");
                        Assert.That(c.Owned, Is.EqualTo(isOwned), ctx + " owned");
                        Assert.That(c.Buyable, Is.EqualTo(!isOwned), ctx + " buyable");
                        Assert.That(c.Silhouette, Is.Not.Null, ctx + " silhouette (shown even when not owned)");
                        V2SpecBook.AssertSameState(StateAfter(m.id, m.actions), c.Silhouette, Tol, ctx + " silhouette");
                    }
                    else Assert.That(page.Cover, Is.Null, ctx);

                    // Complete
                    if (e.kind == BookPageKind.Complete && isOwned)
                    {
                        int n = Models.First(x => x.id == e.model).actions;
                        Assert.That(page.CompleteShape, Is.Not.Null, ctx);
                        V2SpecBook.AssertSameState(StateAfter(e.model, n), page.CompleteShape, Tol, ctx + " complete shape");
                    }
                    else Assert.That(page.CompleteShape, Is.Null, ctx + " complete shape");

                    // Contents
                    if (e.kind == BookPageKind.Contents)
                    {
                        Assert.That(page.Contents.Count, Is.EqualTo(5), ctx);
                        for (int i = 0; i < 5; i++)
                        {
                            var ce = page.Contents[i];
                            var m = Models[i];
                            Assert.That(ce.ModelId, Is.EqualTo(m.id), ctx + " contents order");
                            Assert.That(ce.Name, Is.EqualTo(m.name), ctx);
                            Assert.That(ce.Owned, Is.EqualTo(ownedSet.Contains(m.id)), ctx + " contents owned " + m.id);
                            Assert.That(ce.PagePrice, Is.EqualTo(m.pagePrice), ctx);
                            Assert.That(ce.CoverPage, Is.EqualTo(Covers[m.id]), ctx);
                        }
                    }
                    else Assert.That(page.Contents, Is.Not.Null.And.Empty, ctx + " contents");
                    checkedPages++;
                }
            }
            Assert.That(checkedPages, Is.EqualTo(8 * 44));
        }

        [Test]
        public void EveryStepDiagram_MatchesContentSection2_AndD103()
        {
            var all = V2SpecBook.Ids;
            foreach (var id in all)
            {
                var rows = V2SpecBook.Book(id);
                var d103 = V2SpecBook.D103(id);
                int cover = Covers[id];
                for (int n = 1; n <= rows.Count; n++)
                {
                    string ctx = $"{id} step {n}";
                    var page = BookPages.Get(cover + n, all);
                    var d = page.Diagram;
                    Assert.That(d, Is.Not.Null, ctx);
                    var row = rows[n - 1];

                    Assert.That(d.Step.Kind, Is.EqualTo(row.Kind), ctx + " kind");
                    Assert.That(Near(d.Step.Grab, row.Grab, Tol), Is.True, ctx + " grab");
                    Assert.That(Near(d.Step.Drop, row.Drop, Tol), Is.True, ctx + " drop");
                    Assert.That(d.Step.Layers, Is.EqualTo(row.Layers), ctx + " layers");
                    if (row.Kind == FoldStepKind.Fold) Assert.That(d.Step.Mountain, Is.EqualTo(row.Mountain), ctx + " mountain");

                    int folds = rows.Take(n - 1).Count(r => r.Kind == FoldStepKind.Fold);
                    int creases = rows.Take(n - 1).Count(r => r.Kind == FoldStepKind.Crease);
                    Assert.That(d.BeforeFolds, Is.EqualTo(folds), ctx + " before folds");
                    Assert.That(d.BeforeCreases, Is.EqualTo(creases), ctx + " before creases");

                    V2SpecBook.AssertSameState(StateAfter(id, n - 1), d.Before, Tol, ctx + " before");
                    V2SpecBook.AssertSameState(StateAfter(id, n), d.After, Tol, ctx + " after");
                    int beforeFacets = n == 1 ? 1 : d103[n - 2].facets;
                    Assert.That(d.Before.Facets.Count, Is.EqualTo(beforeFacets), ctx + " D-103 facets before");
                    Assert.That(d.After.Facets.Count, Is.EqualTo(d103[n - 1].facets), ctx + " D-103 facets after");
                    int beforeSegs = n == 1 ? 0 : d103[n - 2].segs;
                    Assert.That(d.Before.Creases.Count, Is.EqualTo(beforeSegs), ctx + " D-103 crease segments before (자국 포함)");

                    // CONTENT §2 "선" values are 6-decimal script output → 1e-4 (D-111).
                    SameSegmentUnordered(d.LineA, d.LineB, row.LineA, row.LineB, 1e-4, ctx);

                    Assert.That(d.Magnet, Is.EqualTo(MagnetKind(row.Magnet)), ctx + " magnet " + row.Magnet);
                    int anchors = row.Magnet == 'P' ? 1 : row.Magnet == 'L' ? 2 : 0;
                    Assert.That(d.Anchors.Count, Is.EqualTo(anchors), ctx + " anchors (CONTENT §3 small circles for P/L)");
                    if (row.Magnet == 'P')
                        Assert.That(Near(d.Anchors[0], row.Drop, 1e-5), Is.True, ctx + " P anchor = the drop reference point");
                    if (row.Magnet == 'L')
                        foreach (var a in d.Anchors)
                            Assert.That(DistToLine(a, row.LineA, row.LineB), Is.LessThan(1e-4), ctx + " L anchor on the fold line");

                    Assert.That(d.Text, Is.EqualTo(StepTexts[id][n - 1]), ctx + " text (CONTENT §2 verbatim)");
                }
            }
        }

        [Test]
        public void F007_AC02_Page6_Plane4_ExactValues()
        {
            var p = BookPages.Get(6, new[] { "plane", "house" });
            Assert.That(p.Kind, Is.EqualTo(BookPageKind.Step));
            Assert.That(p.ModelId, Is.EqualTo("plane"));
            Assert.That(p.StepLabel, Is.EqualTo("4 / 6"));
            var d = p.Diagram;
            Assert.That(d.BeforeFolds, Is.EqualTo(2));
            Assert.That(d.BeforeCreases, Is.EqualTo(1));
            SameSegmentUnordered(d.LineA, d.LineB, new Vec2(0, 1), new Vec2(-0.8284, -1), 1e-4, "page 6 line");
            Assert.That(d.Step.Grab.X, Is.EqualTo(-1).Within(Tol));
            Assert.That(d.Step.Grab.Y, Is.EqualTo(0).Within(Tol));
            Assert.That(d.Step.Drop.X, Is.EqualTo(0).Within(Tol));
            Assert.That(d.Step.Drop.Y, Is.EqualTo(-0.4142).Within(1e-4));
            Assert.That(d.Step.Kind, Is.EqualTo(FoldStepKind.Fold));
            Assert.That(d.Step.Mountain, Is.False, "valley");
            Assert.That(d.Step.Layers, Is.EqualTo(0), "all layers");
            Assert.That(d.Text, Is.EqualTo("왼쪽 비스듬한 변을 가운데 자국에 맞춰 접어요. 접는 선은 위 꼭짓점에서 시작해요."));

            Assert.That(BookPages.Get(3, new[] { "plane" }).Diagram.Step.Kind, Is.EqualTo(FoldStepKind.Crease), "page 3 crease");
            Assert.That(BookPages.Get(21, new[] { "tulip" }).Diagram.Step.Layers, Is.EqualTo(2), "page 21 top 2 layers");
        }

        [Test]
        public void F007_AC03_SealingFollowsTheGame_NewRunThenBuyTulip()
        {
            var g = new OrigamiGame();
            var p18 = BookPages.Get(18, g.Snapshot.OwnedModelIds);
            Assert.That(p18.Sealed, Is.True);
            Assert.That(p18.Diagram, Is.Null);
            var cover = BookPages.Get(17, g.Snapshot.OwnedModelIds).Cover;
            Assert.That(cover.Name, Is.EqualTo("튤립"));
            Assert.That(cover.Silhouette, Is.Not.Null);
            Assert.That(cover.PagePrice, Is.EqualTo(20));
            Assert.That(cover.Buyable, Is.True);
            Assert.That(BookPages.Get(1, g.Snapshot.OwnedModelIds).Kind, Is.EqualTo(BookPageKind.Symbols));
            Assert.That(BookPages.Get(1, g.Snapshot.OwnedModelIds).Sealed, Is.False);
            Assert.That(BookPages.Get(1, new string[0]).Sealed, Is.False, "symbols page always visible");
            Assert.That(BookPages.Get(0, new string[0]).Sealed, Is.False, "contents page always visible");

            Assert.That(g.Interact("bookshelf").Accepted, Is.True);
            Assert.That(g.BuyPage("tulip").Accepted, Is.True);
            var after = BookPages.Get(18, g.Snapshot.OwnedModelIds);
            Assert.That(after.Sealed, Is.False);
            Assert.That(after.Diagram, Is.Not.Null);
            Assert.That(BookPages.Get(17, g.Snapshot.OwnedModelIds).Cover.Buyable, Is.False);
            Assert.That(BookPages.Get(23, g.Snapshot.OwnedModelIds).CompleteShape, Is.Not.Null);
            Assert.That(BookPages.Get(0, g.Snapshot.OwnedModelIds).Contents[2].Owned, Is.True);
        }

        [Test]
        public void OwnedInput_NoiseAndOrderAreIgnored_AndGetIsPure()
        {
            var clean = new[] { "plane", "house", "dog" };
            var noisy = new[] { "dog", null, "unicorn", "", "DOG", "house", "dog", "plane" };
            for (int p = 0; p < 44; p++)
            {
                var a = BookPages.Get(p, clean);
                var b = BookPages.Get(p, noisy);
                Assert.That(b.Sealed, Is.EqualTo(a.Sealed), "page " + p);
                Assert.That(b.Diagram == null, Is.EqualTo(a.Diagram == null), "page " + p);
                Assert.That(b.Cover?.Owned, Is.EqualTo(a.Cover?.Owned), "page " + p);
                if (p == 0) Assert.That(b.Contents.Select(c => c.Owned), Is.EqualTo(a.Contents.Select(c => c.Owned)));
                var again = BookPages.Get(p, clean);
                Assert.That(again.StepLabel, Is.EqualTo(a.StepLabel));
                if (a.Diagram != null)
                {
                    Assert.That(again.Diagram.Text, Is.EqualTo(a.Diagram.Text));
                    Assert.That(Near(again.Diagram.LineA, a.Diagram.LineA, 0), Is.True, "deterministic line");
                    V2SpecBook.AssertSameState(a.Diagram.Before, again.Diagram.Before, 0, "deterministic before " + p);
                }
            }
            // "DOG" is not "dog" (Ordinal): with only noise, dog stays sealed.
            Assert.That(BookPages.Get(25, new[] { "DOG", "Dog" }).Sealed, Is.True);
        }

        [Test]
        public void Get_RangeAndNullArguments()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => BookPages.Get(-1, new string[0]));
            Assert.Throws<ArgumentOutOfRangeException>(() => BookPages.Get(44, new string[0]));
            Assert.Throws<ArgumentOutOfRangeException>(() => BookPages.Get(int.MinValue, new string[0]));
            Assert.Throws<ArgumentOutOfRangeException>(() => BookPages.Get(int.MaxValue, new string[0]));
            Assert.Throws<ArgumentNullException>(() => BookPages.Get(0, null));
            Assert.DoesNotThrow(() => BookPages.Get(43, new string[0]));
        }

        [Test]
        public void ReturnedLists_AreReadOnly()
        {
            var p0 = BookPages.Get(0, new[] { "plane" });
            Assert.That(p0.Contents, Is.InstanceOf<System.Collections.Generic.IList<ContentsEntry>>());
            Assert.That(((IList<ContentsEntry>)p0.Contents).IsReadOnly, Is.True);
            var d = BookPages.Get(3, new[] { "plane" }).Diagram;
            if (d.Anchors is IList<Vec2> list) Assert.That(list.IsReadOnly, Is.True, "anchors read-only");
        }
    }
}
