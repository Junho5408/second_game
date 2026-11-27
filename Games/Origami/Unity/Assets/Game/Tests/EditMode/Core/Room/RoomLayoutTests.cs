using System;
using System.Linq;
using NUnit.Framework;
using Origami.Core;
using Origami.Core.Catalog;
using Origami.Core.Geometry;
using Origami.Core.Room;

namespace Origami.Tests.Room
{
    /// <summary>ORIGAMI-014: room layout values (D-130 = CONTENT §6) and interaction candidates (R-004, F-002 AC-01, F-003 AC-05).</summary>
    public class RoomLayoutTests
    {
        private const double Eps = 1e-9;

        private static Vec2 V(double x, double z) => new Vec2(x, z);

        [Test]
        public void D130_RoomAndCharacterConstants()
        {
            Assert.AreEqual(-4.0, RoomLayout.MinX);
            Assert.AreEqual(4.0, RoomLayout.MaxX);
            Assert.AreEqual(-3.0, RoomLayout.MinZ);
            Assert.AreEqual(3.0, RoomLayout.MaxZ);
            Assert.AreEqual(2.5, RoomLayout.WallHeight);
            Assert.AreEqual(0.3, RoomLayout.CharacterRadius);
            Assert.AreEqual(3.0, RoomLayout.WalkSpeed);
            Assert.AreEqual(1.2, RoomLayout.InteractRange);
            Assert.AreEqual(1.6, RoomLayout.CharacterHeight);
            Assert.AreEqual(0.05, RoomMotion.MaxSubstep);
        }

        [Test]
        public void D130_Positions()
        {
            Assert.AreEqual(V(-1.6, 0.0), RoomLayout.StartPosition);
            Assert.AreEqual(V(2.5, 0.8), RoomLayout.ChairPosition);
            Assert.AreEqual(V(2.5, 0.2), RoomLayout.StandUpPosition);
        }

        [Test]
        public void D130_InteractPoints()
        {
            Assert.AreEqual(V(2.5, 0.8), RoomLayout.InteractPoint(InteractTargets.Table));
            Assert.AreEqual(V(0.0, 1.9), RoomLayout.InteractPoint(InteractTargets.Bookshelf));
            Assert.AreEqual(V(2.5, -1.5), RoomLayout.InteractPoint(InteractTargets.Counter));
            Assert.AreEqual(V(-2.0, 2.0), RoomLayout.InteractPoint(InteractTargets.Bed));
            Assert.Throws<ArgumentException>(() => RoomLayout.InteractPoint("trash"));
            Assert.Throws<ArgumentException>(() => RoomLayout.InteractPoint("Table"), "ordinal");
            Assert.Catch<ArgumentException>(() => RoomLayout.InteractPoint(null));
        }

        [Test]
        public void CONTENT6_InteractPointsAreAtLeast1Point5Apart()
        {
            var pts = InteractTargets.All.Select(RoomLayout.InteractPoint).ToArray();
            for (int i = 0; i < pts.Length; i++)
                for (int j = i + 1; j < pts.Length; j++)
                    Assert.GreaterOrEqual(Vec2.Distance(pts[i], pts[j]), 1.5);
        }

        [Test]
        public void D130_FurnitureTable()
        {
            var f = RoomLayout.Furniture;
            CollectionAssert.AreEqual(new[] { "table", "chair", "bookshelf", "counter", "bed", "trash", "window", "door" }, f.Select(x => x.Id).ToArray());
            void Check(string id, double cx, double cz, double w, double d, double h, bool blocks)
            {
                var s = f.Single(x => x.Id == id);
                Assert.AreEqual(cx, s.Center.X, Eps, id);
                Assert.AreEqual(cz, s.Center.Y, Eps, id);
                Assert.AreEqual(w, s.Width, Eps, id);
                Assert.AreEqual(d, s.Depth, Eps, id);
                Assert.AreEqual(h, s.Height, Eps, id);
                Assert.AreEqual(blocks, s.Blocks, id);
            }
            Check("table", 2.5, 1.6, 1.4, 0.9, 0.75, true);
            Check("chair", 2.5, 0.8, 0.5, 0.5, 0.45, false);
            Check("bookshelf", 0.0, 2.75, 1.6, 0.4, 2.0, true);
            Check("counter", 2.5, -2.3, 2.0, 0.6, 1.0, true);
            Check("bed", -3.0, 2.0, 1.0, 2.0, 0.5, true);
            Check("trash", 3.6, 0.6, 0.4, 0.4, 0.5, true);
            Assert.IsFalse(f.Single(x => x.Id == "window").Blocks);
            Assert.IsFalse(f.Single(x => x.Id == "door").Blocks);
            Assert.AreEqual(-1.8, f.Single(x => x.Id == "window").Center.X, Eps);
            Assert.AreEqual(V(-3.0, -3.0), f.Single(x => x.Id == "door").Center);
        }

        [Test]
        public void D130_ObstacleBoxes()
        {
            var o = RoomLayout.Obstacles;
            Assert.AreEqual(5, o.Count);
            void Box(int i, double a, double b, double c, double d)
            {
                Assert.AreEqual(a, o[i].MinX, Eps);
                Assert.AreEqual(b, o[i].MinZ, Eps);
                Assert.AreEqual(c, o[i].MaxX, Eps);
                Assert.AreEqual(d, o[i].MaxZ, Eps);
            }
            Box(0, 1.8, 1.15, 3.2, 2.05);    // table
            Box(1, -0.8, 2.55, 0.8, 2.95);   // bookshelf
            Box(2, 1.5, -2.6, 3.5, -2.0);    // counter
            Box(3, -3.5, 1.0, -2.5, 3.0);    // bed
            Box(4, 3.4, 0.4, 3.8, 0.8);      // trash
        }

        [Test]
        public void Lists_AreReadOnly()
        {
            Assert.Throws<NotSupportedException>(() => ((System.Collections.Generic.IList<FurnitureSpec>)RoomLayout.Furniture).Add(null));
            Assert.Throws<NotSupportedException>(() => ((System.Collections.Generic.IList<Box>)RoomLayout.Obstacles).Clear());
        }

        [Test]
        public void F002_AC01_StartHasNoInteractCandidate()
        {
            Assert.IsNull(Interaction.Nearest(RoomLayout.StartPosition));
            Assert.AreEqual(2.04, Vec2.Distance(RoomLayout.StartPosition, RoomLayout.InteractPoint(InteractTargets.Bed)), 0.01);
        }

        [Test]
        public void F003_AC05_Table1Point1IsInRange_1Point3IsNot()
        {
            var chair = RoomLayout.InteractPoint(InteractTargets.Table);
            Assert.AreEqual(InteractTargets.Table, Interaction.Nearest(chair + V(0, -1.1)));
            Assert.AreEqual(InteractTargets.Table, Interaction.Nearest(chair + V(-1.1, 0)));
            Assert.IsNull(Interaction.Nearest(chair + V(-1.3, 0)), "1.3 m west of the chair point (nothing else near)");
            Assert.AreEqual(InteractTargets.Table, Interaction.Nearest(chair + V(-1.2, 0)), "1.2 is inside (≤)");
        }

        [Test]
        public void R004_EachTargetAtItsPoint()
        {
            foreach (var t in InteractTargets.All)
                Assert.AreEqual(t, Interaction.Nearest(RoomLayout.InteractPoint(t)));
            Assert.AreEqual(InteractTargets.Table, Interaction.Nearest(RoomLayout.StandUpPosition), "standing up keeps the table in range");
        }

        [Test]
        public void R004_NearerWins_TieGoesToCatalogueOrder()
        {
            var table = RoomLayout.InteractPoint(InteractTargets.Table);     // (2.5, 0.8)
            var counter = RoomLayout.InteractPoint(InteractTargets.Counter); // (2.5, -1.5)
            // Midpoint z = -0.35: 1.15 from both → table first.
            Assert.AreEqual(InteractTargets.Table, Interaction.Nearest(V(2.5, -0.35)));
            Assert.AreEqual(InteractTargets.Counter, Interaction.Nearest(V(2.5, -0.4)));
            Assert.AreEqual(InteractTargets.Table, Interaction.Nearest(V(2.5, -0.3)));
            Assert.AreEqual(1.15, Vec2.Distance(V(2.5, -0.35), table), 1e-9);
            Assert.AreEqual(1.15, Vec2.Distance(V(2.5, -0.35), counter), 1e-9);
        }

        [Test]
        public void GameInfo_IsVersion2()
        {
            // GameInfo.cs is in this task's scope (ARCHITECTURE §10: value only → "2.0.0").
            Assert.AreEqual("2.0.0", GameInfo.Version);
            Assert.AreEqual("Origami", GameInfo.Name);
        }
    }
}
