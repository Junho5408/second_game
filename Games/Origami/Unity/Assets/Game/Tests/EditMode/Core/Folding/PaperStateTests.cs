using System;
using System.Collections.Generic;
using NUnit.Framework;
using Origami.Core.Folding;
using Origami.Core.Geometry;
using static Origami.Tests.Folding.FoldTestUtil;

namespace Origami.Tests.Folding
{
    // C-101 Vec2, C-102 Facet / CreaseSegment / PaperState.
    public class PaperStateTests
    {
        private static readonly Vec2[] Square = { V(-1, -1), V(1, -1), V(1, 1), V(-1, 1) };

        [Test]
        public void Initial_IsTheUnfoldedWhiteSheet()
        {
            var s = PaperState.Initial;
            Assert.AreSame(s, PaperState.Initial);
            Assert.AreEqual(1, s.Facets.Count);
            CollectionAssert.AreEqual(Square, s.Facets[0].World);
            CollectionAssert.AreEqual(Square, s.Facets[0].Paper);
            Assert.AreEqual(0, s.Facets[0].Layer);
            Assert.IsFalse(s.Facets[0].FrontUp);
            Assert.AreEqual(0, s.Creases.Count);
            Assert.AreEqual(0, s.MaxLayer());
            Assert.AreEqual(4.0, s.TotalArea());
            Assert.AreEqual(0.0, s.FlipAxisX());
        }

        [Test]
        public void Constructors_CopyAndValidate()
        {
            var list = new List<Vec2>(Square);
            var f = new Facet(list, list, 3, true);
            list[0] = V(9, 9);
            Assert.AreEqual(V(-1, -1), f.World[0], "facet copies its lists");
            Assert.AreEqual(3, f.Layer);
            Assert.IsTrue(f.FrontUp);
            Assert.AreEqual(4.0, f.SignedArea());
            Assert.AreEqual(4.0, f.Area);
            var cw = new Facet(new[] { V(-1, 1), V(1, 1), V(1, -1), V(-1, -1) }, Square, 0, false);
            Assert.AreEqual(-4.0, cw.SignedArea());
            Assert.AreEqual(4.0, cw.Area);

            Assert.Throws<ArgumentNullException>(() => new Facet(null, Square, 0, false));
            Assert.Throws<ArgumentNullException>(() => new Facet(Square, null, 0, false));
            Assert.Throws<ArgumentException>(() => new Facet(new[] { V(0, 0), V(1, 0) }, new[] { V(0, 0), V(1, 0) }, 0, false));
            Assert.Throws<ArgumentException>(() => new Facet(Square, new[] { V(0, 0), V(1, 0), V(1, 1) }, 0, false));

            var facets = new List<Facet> { f };
            var creases = new List<CreaseSegment> { new CreaseSegment(V(0, 0), V(1, 0)) };
            var s = new PaperState(facets, creases);
            facets.Clear();
            creases.Clear();
            Assert.AreEqual(1, s.Facets.Count);
            Assert.AreEqual(1, s.Creases.Count);
            Assert.AreEqual(0, new PaperState(new[] { f }, null).Creases.Count);
            Assert.Throws<ArgumentNullException>(() => new PaperState(null, null));
            Assert.Throws<ArgumentException>(() => new PaperState(new Facet[0], null));
            Assert.Throws<ArgumentException>(() => new PaperState(new Facet[] { null }, null));
        }

        [Test]
        public void ReadOnlyLists_CannotBeModified()
        {
            var s = FoldPlanner.PlanCrease(PaperState.Initial, V(-1, 1), V(1, 1), 0).Result;
            Assert.Throws<NotSupportedException>(() => ((IList<Facet>)s.Facets).Add(s.Facets[0]));
            Assert.Throws<NotSupportedException>(() => ((IList<CreaseSegment>)s.Creases).Clear());
            Assert.Throws<NotSupportedException>(() => ((IList<Vec2>)s.Facets[0].World)[0] = V(5, 5));
        }

        [Test]
        public void Facet_ContainsStrictlyAndPaperToWorld()
        {
            var f = PaperState.Initial.Facets[0];
            Assert.IsTrue(f.Contains(V(0, 0)));
            Assert.IsTrue(f.Contains(V(1, 1)), "border included");
            Assert.IsTrue(f.Contains(V(1 + 2e-7, 0)), "Cross within 1e-6 (edge length 2)");
            Assert.IsFalse(f.Contains(V(1 + 1e-6, 0)));
            Assert.IsTrue(f.StrictlyContains(V(0.5, 0.5)));
            Assert.IsFalse(f.StrictlyContains(V(1, 0.5)), "border is not strictly inside");
            Assert.IsFalse(f.StrictlyContains(V(1 - 5e-7, 0.5)));

            var moved = FoldPlanner.PlanFold(PaperState.Initial, V(-1, 1), V(-1, -1), false, 0).Moved[0];
            AreClose(V(0.25, -0.75), moved.PaperToWorld(V(0.25, 0.75)), 1e-12);
            AreClose(V(-1, 0), moved.PaperToWorld(V(-1, 0)), 1e-12);
        }

        [Test]
        public void StackAndTopFacet()
        {
            var s = FoldPlanner.PlanFold(PaperState.Initial, V(-1, 1), V(-1, -1), false, 0).Result;
            Assert.AreEqual(2, s.StackCountAt(V(0, -0.5)));
            Assert.AreEqual(2, s.StackCountAt(V(-1, -1)));
            Assert.AreEqual(0, s.StackCountAt(V(0, 0.5)));
            Assert.AreEqual(1, s.TopFacetAt(V(0, -0.5)), "moved half is on top");
            Assert.AreEqual(-1, s.TopFacetAt(V(0, 0.5)));
            Assert.AreEqual(1, s.MaxLayer());
        }

        [Test]
        public void TrySnapGrab_CornerEdgeInteriorOutside()
        {
            var s = PaperState.Initial;
            Assert.IsTrue(s.TrySnapGrab(V(-0.9, 0.95), 0.16, 0.08, out var g, out bool corner));
            Assert.IsTrue(corner);
            Assert.AreEqual(V(-1, 1), g);

            Assert.IsTrue(s.TrySnapGrab(V(0.5, 1.05), 0.16, 0.08, out g, out corner));
            Assert.IsFalse(corner);
            AreClose(V(0.5, 1), g, 1e-12);

            Assert.IsTrue(s.TrySnapGrab(V(0.2, 0.3), 0.16, 0.08, out g, out corner));
            Assert.IsFalse(corner);
            Assert.AreEqual(V(0.2, 0.3), g);

            Assert.IsFalse(s.TrySnapGrab(V(3, 3), 0.16, 0.08, out g, out corner));
            Assert.IsFalse(corner);
        }

        [Test]
        public void CreaseSegment_ValueSemantics()
        {
            var a = new CreaseSegment(V(0, -1), V(0, 1));
            Assert.AreEqual(2.0, a.Length);
            Assert.AreEqual(a, new CreaseSegment(V(0, -1), V(0, 1)));
            Assert.AreNotEqual(a, new CreaseSegment(V(0, 1), V(0, -1)));
            Assert.AreEqual(a.GetHashCode(), new CreaseSegment(V(0, -1), V(0, 1)).GetHashCode());
        }

        [Test]
        public void Vec2_Basics()
        {
            var a = V(3, 4);
            Assert.AreEqual(5.0, a.Length);
            Assert.AreEqual(25.0, a.LengthSq);
            Assert.AreEqual(V(0.6, 0.8), a.Normalized);
            Assert.AreEqual(V(0, 0), V(0, 0).Normalized);
            Assert.AreEqual(V(-4, 3), a.Perp);
            Assert.AreEqual(V(4, 6), a + V(1, 2));
            Assert.AreEqual(V(2, 2), a - V(1, 2));
            Assert.AreEqual(V(-3, -4), -a);
            Assert.AreEqual(V(6, 8), a * 2);
            Assert.AreEqual(V(6, 8), 2 * a);
            Assert.AreEqual(11.0, Vec2.Dot(a, V(1, 2)));
            Assert.AreEqual(2.0, Vec2.Cross(a, V(1, 2)));
            Assert.AreEqual(5.0, Vec2.Distance(V(0, 0), a));
            Assert.AreEqual(V(1.5, 2), Vec2.Lerp(V(0, 0), a, 0.5));
            Assert.AreEqual(V(1, 0), Vec2.ClosestOnSegment(V(1, 5), V(0, 0), V(2, 0)));
            Assert.AreEqual(V(2, 0), Vec2.ClosestOnSegment(V(7, 5), V(0, 0), V(2, 0)));
            Assert.AreEqual(V(0, 0), Vec2.ClosestOnSegment(V(7, 5), V(0, 0), V(0, 0)));
            Assert.IsTrue(a.Equals((object)V(3, 4)));
            Assert.IsFalse(a.Equals(V(3, 4.0000001)));
            Assert.AreEqual(V(3, 4).GetHashCode(), a.GetHashCode());
            Assert.AreEqual("(1.235, -2)", V(1.23456, -2).ToString());
        }
    }
}
