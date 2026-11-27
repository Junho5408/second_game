using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using NUnit.Framework;
using Origami.Core.Folding;
using Origami.Core.Geometry;

namespace Origami.Tests.Folding
{
    /// <summary>Shared helpers for the ORIGAMI-009 folding tests.</summary>
    internal static class FoldTestUtil
    {
        public const double Eps = 1e-6;   // FEATURE_SPECS common notation: paper coordinates / areas

        public static Vec2 V(double x, double y) => new Vec2(x, y);

        public static readonly Vec2 C = new Vec2(-1, -1);

        public static double Parse(string s) => double.Parse(s, NumberStyles.Float, CultureInfo.InvariantCulture);

        public static void AreClose(Vec2 expected, Vec2 actual, double tol, string message = null)
        {
            Assert.That(actual.X, Is.EqualTo(expected.X).Within(tol), (message ?? "") + " x of " + actual);
            Assert.That(actual.Y, Is.EqualTo(expected.Y).Within(tol), (message ?? "") + " y of " + actual);
        }

        /// <summary>Area of the union of all facet world polygons (polygon clipping-free check through the outline).</summary>
        public static double OutlineArea(PaperState s) => Origami.Core.Recognition.Outline.FromState(s).Area;

        public static double SumArea(IEnumerable<Facet> facets) => facets.Sum(f => f.Area);

        /// <summary>Applies Python-style action tokens (F/C/X) and returns the state.</summary>
        public static FoldStep ParseAction(string[] t, ref int i)
        {
            string k = t[i++];
            if (k == "X") return FoldStep.Flip();
            var g = V(Parse(t[i++]), Parse(t[i++]));
            var d = V(Parse(t[i++]), Parse(t[i++]));
            if (k == "F")
            {
                bool m = t[i++] == "1";
                int l = int.Parse(t[i++], CultureInfo.InvariantCulture);
                return FoldStep.Fold(g, d, m, l);
            }
            if (k == "C")
            {
                int l = int.Parse(t[i++], CultureInfo.InvariantCulture);
                return FoldStep.Crease(g, d, l);
            }
            throw new FormatException("action " + k);
        }

        /// <summary>Plans one step without throwing (Flip always valid).</summary>
        public static FoldPlan Plan(PaperState s, FoldStep step) =>
            step.Kind == FoldStepKind.Fold ? FoldPlanner.PlanFold(s, step.Grab, step.Drop, step.Mountain, step.Layers)
            : FoldPlanner.PlanCrease(s, step.Grab, step.Drop, step.Layers);

        /// <summary>Compares a state with a Python "S" record (facets in order, layers, sides, vertices, creases).</summary>
        public static void AssertStateEquals(string record, PaperState s, double tol, string context)
        {
            var parts = record.Split('#');
            var facetText = parts[0].Trim();
            var creaseText = parts.Length > 1 ? parts[1].Trim() : "";
            var facets = facetText.Length == 0 ? new string[0] : facetText.Split(';');
            Assert.AreEqual(facets.Length, s.Facets.Count, context + ": facet count");
            for (int fi = 0; fi < facets.Length; fi++)
            {
                var halves = facets[fi].Split('/');
                var a = halves[0].Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                var b = halves[1].Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                var f = s.Facets[fi];
                string fc = context + ": facet " + fi;
                Assert.AreEqual(int.Parse(a[0], CultureInfo.InvariantCulture), f.Layer, fc + " layer");
                Assert.AreEqual(a[1] == "1", f.FrontUp, fc + " front");
                int n = (a.Length - 2) / 2;
                Assert.AreEqual(n, f.World.Count, fc + " vertex count");
                for (int k = 0; k < n; k++)
                {
                    AreClose(V(Parse(a[2 + 2 * k]), Parse(a[3 + 2 * k])), f.World[k], tol, fc + " world " + k);
                    AreClose(V(Parse(b[2 * k]), Parse(b[2 * k + 1])), f.Paper[k], tol, fc + " paper " + k);
                }
            }
            var creases = creaseText.Length == 0 ? new string[0] : creaseText.Split(';');
            Assert.AreEqual(creases.Length, s.Creases.Count, context + ": crease count");
            for (int ci = 0; ci < creases.Length; ci++)
            {
                var c = creases[ci].Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).Select(Parse).ToArray();
                AreClose(V(c[0], c[1]), s.Creases[ci].A, tol, context + ": crease " + ci + " A");
                AreClose(V(c[2], c[3]), s.Creases[ci].B, tol, context + ": crease " + ci + " B");
            }
        }

        public static string[] Tokens(string line) => line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
    }
}
