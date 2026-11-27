using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Origami.Core.Folding;
using Origami.Core.Geometry;

namespace Origami.Tests.Qa
{
    /// <summary>
    /// QA ORIGAMI-009: an independent transcription of CONTENT_SPECS.md §2 (v2 book sequences), typed by QA
    /// from the spec tables (not copied from ModelCatalog). Each row = action, layers, diagram line ends, magnet letter.
    /// </summary>
    internal static class V2SpecBook
    {
        public static readonly double K = 1 - Math.Sqrt(2);          // CONTENT §2 k
        public static readonly double X0 = 2 * Math.Tan(Math.PI / 8); // CONTENT §2 x0

        internal sealed class Row
        {
            public FoldStepKind Kind;
            public Vec2 Grab, Drop, LineA, LineB;
            public bool Mountain;
            public int Layers;
            public char Magnet;   // 'P', 'L', 'M', '-'

            public FoldStep ToStep() =>
                Kind == FoldStepKind.Crease ? FoldStep.Crease(Grab, Drop, Layers) : FoldStep.Fold(Grab, Drop, Mountain, Layers);
        }

        static Vec2 V(double x, double y) => new Vec2(x, y);

        static Row F(Vec2 g, Vec2 d, int layers, Vec2 a, Vec2 b, char m, bool mountain = false) =>
            new Row { Kind = FoldStepKind.Fold, Grab = g, Drop = d, Layers = layers, LineA = a, LineB = b, Magnet = m, Mountain = mountain };

        static Row C(Vec2 g, Vec2 d, int layers, Vec2 a, Vec2 b, char m) =>
            new Row { Kind = FoldStepKind.Crease, Grab = g, Drop = d, Layers = layers, LineA = a, LineB = b, Magnet = m };

        public static readonly string[] Ids = { "plane", "house", "tulip", "dog", "cicada" };

        public static IReadOnlyList<Row> Book(string id)
        {
            switch (id)
            {
                case "plane":
                    return new[]
                    {
                        C(V(-1, 1), V(1, 1), 0, V(0, -1), V(0, 1), 'P'),
                        F(V(-1, 1), V(0, 0), 0, V(-1, 0), V(0, 1), 'M'),
                        F(V(1, 1), V(0, 0), 0, V(0, 1), V(1, 0), 'P'),
                        F(V(-1, 0), V(0, K), 0, V(-X0, -1), V(0, 1), 'M'),
                        F(V(1, 0), V(0, K), 0, V(0, 1), V(X0, -1), 'P'),
                        F(V(-X0, -1), V(X0, -1), 0, V(0, -1), V(0, 1), 'P'),
                    };
                case "house":
                    return new[]
                    {
                        F(V(-1, 1), V(-1, -1), 0, V(-1, 0), V(1, 0), 'P'),
                        C(V(-1, 0), V(1, 0), 0, V(0, -1), V(0, 0), 'P'),
                        C(V(-1, -1), V(-1, 0), 0, V(-1, -0.5), V(1, -0.5), 'P'),
                        F(V(-1, 0), V(-0.6, -0.8), 0, V(-1, -0.5), V(0, 0), 'L'),
                        F(V(1, 0), V(0.6, -0.8), 0, V(0, 0), V(1, -0.5), 'L'),
                    };
                case "tulip":
                    return new[]
                    {
                        F(V(1, 1), V(-1, -1), 0, V(-1, 1), V(1, -1), 'P'),
                        F(V(-1, 1), V(-1, -1), 0, V(-1, 0), V(0, 0), 'P'),
                        F(V(1, -1), V(-1, -1), 0, V(0, 0), V(0, -1), 'P'),
                        F(V(-1, -1), V(0.689655, -0.275862), 2, V(0, -1), V(-0.428572, 0), '-'),
                        F(V(-1, -1), V(-0.275862, 0.689655), 2, V(0.386473, -0.594203), V(-1, 0), 'L'),
                    };
                case "dog":
                    return new[]
                    {
                        F(V(1, 1), V(-1, -1), 0, V(-1, 1), V(1, -1), 'P'),
                        C(V(-1, 1), V(1, -1), 0, V(-1, -1), V(0, 0), 'P'),
                        C(V(-1, -1), V(0, 0), 0, V(0, -1), V(-1, 0), 'P'),
                        F(V(-1, 1), V(-1.4, 0.0), 0, V(-1, 0.42), V(-0.033333, 0.033333), '-'),
                        F(V(1, -1), V(0.0, -1.4), 0, V(0.033333, -0.033333), V(0.42, -1), '-'),
                        F(V(-1, -1), V(-0.5, -0.5), 0, V(-0.5, -1), V(-1, -0.5), 'P'),
                    };
                case "cicada":
                    return new[]
                    {
                        F(V(1, 1), V(-1, -1), 0, V(-1, 1), V(1, -1), 'P'),
                        F(V(-1, 1), V(-1, -1), 0, V(-1, 0), V(0, 0), 'P'),
                        F(V(1, -1), V(-1, -1), 0, V(0, 0), V(0, -1), 'P'),
                        C(V(-1, 0), V(0, -1), 0, V(-1, -1), V(0, 0), 'P'),
                        C(V(-1, 0), V(0, 0), 0, V(-0.5, -1), V(-0.5, 0), 'P'),
                        F(V(-1, -1), V(0.453858, -0.108926), 2, V(0, -1), V(-0.612903, 0), '-'),
                        F(V(-1, -1), V(-0.108926, 0.453858), 2, V(0.150257, -0.704996), V(-1, 0), 'L'),
                        F(V(-1, -1), V(-0.38, -0.38), 0, V(-0.38, -1), V(-1, -0.38), 'P'),
                        F(V(-1, 0), V(-0.5, -0.5), 0, V(-0.94, -0.44), V(-0.139167, 0.360833), 'P', mountain: true),
                        F(V(0, -1), V(-0.5, -0.5), 0, V(0.360833, -0.139167), V(-0.44, -0.94), 'P', mountain: true),
                    };
                default:
                    throw new ArgumentException(id);
            }
        }

        public static List<FoldStep> Steps(string id) => Book(id).Select(r => r.ToStep()).ToList();

        /// <summary>DATA_STRUCTURE D-103 (facets, crease segments, folds) after each action.</summary>
        public static (int facets, int segs, int folds)[] D103(string id)
        {
            switch (id)
            {
                case "plane": return new[] { (1, 1, 0), (2, 1, 1), (3, 1, 2), (5, 1, 3), (7, 1, 4), (8, 1, 5) };
                case "house": return new[] { (2, 0, 1), (2, 2, 1), (2, 4, 1), (4, 4, 2), (6, 4, 3) };
                case "tulip": return new[] { (2, 0, 1), (4, 0, 2), (6, 0, 3), (8, 0, 4), (10, 0, 5) };
                case "dog": return new[] { (2, 0, 1), (2, 2, 1), (2, 4, 1), (4, 4, 2), (6, 4, 3), (8, 4, 4) };
                case "cicada":
                    return new[] { (2, 0, 1), (4, 0, 2), (6, 0, 3), (6, 4, 3), (6, 10, 3), (8, 10, 4), (10, 10, 5), (12, 10, 6), (20, 10, 7), (28, 10, 8) };
                default: throw new ArgumentException(id);
            }
        }

        /// <summary>Replays steps through a FoldSession and fails if any step is rejected.</summary>
        public static FoldSession Play(IEnumerable<FoldStep> steps)
        {
            var s = new FoldSession();
            int i = 0;
            foreach (var st in steps)
            {
                i++;
                if (st.Kind == FoldStepKind.Flip) { s.Flip(); continue; }
                var plan = st.Kind == FoldStepKind.Crease
                    ? s.Crease(st.Grab, st.Drop, st.Layers)
                    : s.Fold(st.Grab, st.Drop, st.Mountain, st.Layers);
                Assert.That(plan.Valid, Is.True, $"step {i} rejected: {plan.Error}");
            }
            return s;
        }

        public static FoldStep Fold(double gx, double gy, double dx, double dy, bool mountain = false, int layers = 0) =>
            FoldStep.Fold(new Vec2(gx, gy), new Vec2(dx, dy), mountain, layers);

        public static void AssertSameState(PaperState expected, PaperState actual, double tol, string msg)
        {
            Assert.That(actual.Facets.Count, Is.EqualTo(expected.Facets.Count), msg + " facet count");
            for (int i = 0; i < expected.Facets.Count; i++)
            {
                var e = expected.Facets[i];
                var a = actual.Facets[i];
                Assert.That(a.Layer, Is.EqualTo(e.Layer), $"{msg} facet {i} layer");
                Assert.That(a.FrontUp, Is.EqualTo(e.FrontUp), $"{msg} facet {i} frontUp");
                Assert.That(a.World.Count, Is.EqualTo(e.World.Count), $"{msg} facet {i} vertex count");
                for (int k = 0; k < e.World.Count; k++)
                {
                    Assert.That(a.World[k].X, Is.EqualTo(e.World[k].X).Within(tol), $"{msg} facet {i} v{k} x");
                    Assert.That(a.World[k].Y, Is.EqualTo(e.World[k].Y).Within(tol), $"{msg} facet {i} v{k} y");
                }
            }
            Assert.That(actual.Creases.Count, Is.EqualTo(expected.Creases.Count), msg + " crease count");
        }
    }
}
