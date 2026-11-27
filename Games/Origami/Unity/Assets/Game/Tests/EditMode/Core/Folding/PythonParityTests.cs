using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Origami.Core.Folding;
using Origami.Core.Geometry;
using static Origami.Tests.Folding.FoldTestUtil;

namespace Origami.Tests.Folding
{
    // C-102..C-107 "참조 구현 동등성": the C# engine reproduces balance_check.py on the reference sequences, on random
    // fold/crease/flip sequences (incl. top-N layers, mountain folds and rejected folds) and on the magnets.
    // Expected values: PythonParityData (generated from balance_check.py, see header there).
    public class PythonParityTests
    {
        private const double Tol = 1e-8;   // data has 10 decimals

        private static List<string> Lines() =>
            PythonParityData.Text.Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0).ToList();

        private static FoldStep Action(string line)
        {
            var t = Tokens(line.Substring(2));
            int i = 0;
            return ParseAction(t, ref i);
        }

        private static void RunSequences(string header)
        {
            var lines = Lines();
            int sequences = 0, checkedStates = 0, rejected = 0;
            PaperState st = null;
            string ctx = null;
            for (int li = 0; li < lines.Count; li++)
            {
                var line = lines[li];
                if (line.StartsWith("REF ", StringComparison.Ordinal) || line.StartsWith("RND ", StringComparison.Ordinal))
                {
                    st = line.StartsWith(header, StringComparison.Ordinal) ? PaperState.Initial : null;
                    ctx = line;
                    if (st != null) sequences++;
                    continue;
                }
                if (line.StartsWith("SNAPSTATE", StringComparison.Ordinal)) st = null;
                if (st == null) continue;
                if (line.StartsWith("S ", StringComparison.Ordinal) && li > 0 && !lines[li - 1].StartsWith("A ", StringComparison.Ordinal))
                {
                    AssertStateEquals(line.Substring(2), st, Tol, ctx + " initial");
                    continue;
                }
                if (!line.StartsWith("A ", StringComparison.Ordinal)) continue;
                var step = Action(line);
                var expected = lines[++li];
                string c = ctx + " / " + line;
                if (step.Kind == FoldStepKind.Flip)
                {
                    st = FoldPlanner.Flip(st);
                }
                else
                {
                    var plan = Plan(st, step);
                    if (expected.StartsWith("E ", StringComparison.Ordinal))
                    {
                        Assert.IsFalse(plan.Valid, c);
                        Assert.AreEqual(expected.Substring(2), plan.Error.ToString(), c);
                        Assert.IsNull(plan.Result, c);
                        rejected++;
                        continue;
                    }
                    Assert.IsTrue(plan.Valid, c + " → " + plan.Error);
                    st = plan.Result;
                }
                AssertStateEquals(expected.Substring(2), st, Tol, c);
                checkedStates++;
            }
            Assert.Greater(sequences, 0);
            TestContext.WriteLine($"{header}: {sequences} sequences, {checkedStates} states, {rejected} rejections");
        }

        [Test]
        public void ReferenceSequences_MatchPythonStepByStep() => RunSequences("REF ");

        [Test]
        public void RandomSequences_MatchPythonIncludingRejections() => RunSequences("RND ");

        private sealed class SnapCase
        {
            public PaperState State;
            public string Context;
            public string Landmarks;
            public string Marks;
            public List<string> Snaps = new List<string>();
            public List<string> LinesOnPaper = new List<string>();
        }

        private static List<SnapCase> SnapCases()
        {
            var cases = new List<SnapCase>();
            SnapCase cur = null;
            foreach (var line in Lines())
            {
                if (line.StartsWith("SNAPSTATE", StringComparison.Ordinal))
                {
                    var st = PaperState.Initial;
                    var rest = line.Substring("SNAPSTATE".Length).Trim();
                    if (rest.Length > 0)
                    {
                        foreach (var a in rest.Split('|'))
                        {
                            var t = Tokens(a);
                            int i = 0;
                            st = FoldPlanner.Apply(st, ParseAction(t, ref i));
                        }
                    }
                    cur = new SnapCase { State = st, Context = line };
                    cases.Add(cur);
                }
                else if (cur != null && line.StartsWith("LM", StringComparison.Ordinal)) cur.Landmarks = line.Substring(2).Trim();
                else if (cur != null && line.StartsWith("MP", StringComparison.Ordinal)) cur.Marks = line.Substring(2).Trim();
                else if (cur != null && line.StartsWith("SN ", StringComparison.Ordinal)) cur.Snaps.Add(line.Substring(3));
                else if (cur != null && line.StartsWith("LN ", StringComparison.Ordinal)) cur.LinesOnPaper.Add(line.Substring(3));
            }
            return cases;
        }

        private static double[][] Groups(string text) =>
            text.Length == 0 ? new double[0][] : text.Split(';').Select(g => Tokens(g).Select(Parse).ToArray()).ToArray();

        [Test]
        public void Landmarks_MatchPython()
        {
            foreach (var sc in SnapCases())
            {
                var expected = Groups(sc.Landmarks);
                var actual = DropSnap.Landmarks(sc.State);
                Assert.AreEqual(expected.Length, actual.Count, sc.Context);
                for (int i = 0; i < expected.Length; i++) AreClose(V(expected[i][0], expected[i][1]), actual[i], Tol, sc.Context + " lm " + i);
            }
        }

        [Test]
        public void MarkPieces_MatchPython()
        {
            int total = 0;
            foreach (var sc in SnapCases())
            {
                var expected = Groups(sc.Marks);
                var actual = DropSnap.MarkPieces(sc.State);
                Assert.AreEqual(expected.Length, actual.Count, sc.Context);
                for (int i = 0; i < expected.Length; i++)
                {
                    AreClose(V(expected[i][0], expected[i][1]), actual[i].A, Tol, sc.Context + " mark A " + i);
                    AreClose(V(expected[i][2], expected[i][3]), actual[i].B, Tol, sc.Context + " mark B " + i);
                    Assert.AreEqual((int)expected[i][4], actual[i].FacetIndex, sc.Context + " mark facet " + i);
                }
                total += actual.Count;
            }
            Assert.Greater(total, 0);
        }

        [Test]
        public void Snap_MatchesPython_KindAndPoint()
        {
            var kinds = new Dictionary<string, SnapKind>
            {
                ["P"] = SnapKind.Point, ["L"] = SnapKind.Line, ["M"] = SnapKind.Mark, ["-"] = SnapKind.None,
            };
            var seen = new HashSet<SnapKind>();
            int n = 0;
            foreach (var sc in SnapCases())
            {
                foreach (var s in sc.Snaps)
                {
                    var t = Tokens(s);
                    var g = V(Parse(t[0]), Parse(t[1]));
                    var d = V(Parse(t[2]), Parse(t[3]));
                    var r = DropSnap.Snap(sc.State, g, d);
                    string c = sc.Context + " / SN " + s;
                    Assert.AreEqual(kinds[t[4]], r.Kind, c);
                    AreClose(V(Parse(t[5]), Parse(t[6])), r.Point, Tol, c);
                    seen.Add(r.Kind);
                    n++;
                }
            }
            CollectionAssert.IsSupersetOf(seen, new[] { SnapKind.None, SnapKind.Point, SnapKind.Line, SnapKind.Mark });
            Assert.Greater(n, 300);
        }

        [Test]
        public void LineOnPaper_MatchesUnroundedPython()
        {
            foreach (var sc in SnapCases())
            {
                for (int i = 0; i < sc.Snaps.Count; i++)
                {
                    var t = Tokens(sc.Snaps[i]);
                    var g = V(Parse(t[0]), Parse(t[1]));
                    var d = V(Parse(t[2]), Parse(t[3]));
                    var exp = sc.LinesOnPaper[i];
                    bool ok = LineOnPaper.TryGet(sc.State, g, d, out Vec2 a, out Vec2 b);
                    string c = sc.Context + " / LN " + sc.Snaps[i];
                    if (exp == "none")
                    {
                        Assert.IsFalse(ok, c);
                        continue;
                    }
                    Assert.IsTrue(ok, c);
                    var e = Tokens(exp).Select(Parse).ToArray();
                    AreClose(V(e[0], e[1]), a, Tol, c + " a");
                    AreClose(V(e[2], e[3]), b, Tol, c + " b");
                }
            }
        }
    }
}
