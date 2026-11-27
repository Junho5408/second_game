using System;
using System.Collections.Generic;
using NUnit.Framework;
using Origami.Core.Catalog;
using Origami.Core.Geometry;
using Origami.Core.Room;

namespace Origami.Tests.Qa
{
    /// <summary>
    /// QA ORIGAMI-014: black-box checks of the workshop room (CONTENT §6, F-002, F-003, R-004, C-180) that the developer
    /// tests do not cover: the room is walkable to every target, layout consistency, the exact 1.2 m boundary for every
    /// target (with floating-point noise), and the tie order between two other targets. Expected values are typed from the spec.
    /// </summary>
    [TestFixture]
    public class RoomSpecQaTests
    {
        private const double Dt = 0.02;   // one frame as in the PlayMode tests
        private static Vec2 V(double x, double z) => new Vec2(x, z);

        private static bool InsideExpanded(Vec2 p, Box o, double r) =>
            p.X > o.MinX - r && p.X < o.MaxX + r && p.Y > o.MinZ - r && p.Y < o.MaxZ + r;

        private static bool Free(Vec2 p)
        {
            double r = RoomLayout.CharacterRadius;
            if (p.X < -4 + r - 1e-9 || p.X > 4 - r + 1e-9 || p.Y < -3 + r - 1e-9 || p.Y > 3 - r + 1e-9) return false;
            foreach (var o in RoomLayout.Obstacles)
                if (InsideExpanded(p, o, r)) return false;
            return true;
        }

        /// <summary>Walks greedily toward <paramref name="goal"/> with Step only until <paramref name="target"/> is the E candidate.</summary>
        private static (bool reached, double seconds, Vec2 at) WalkUntilPrompt(Vec2 from, Vec2 goal, string target)
        {
            var p = from;
            for (int frame = 0; frame < 1000; frame++)   // 20 s
            {
                if (Interaction.Nearest(p) == target) return (true, frame * Dt, p);
                var d = goal - p;
                if (d.Length < 1e-9) break;
                p = RoomMotion.Step(p, d.Normalized, Dt);
                Assert.IsTrue(Free(p), $"walking to {target}: {p} is inside a wall or an obstacle");
            }
            return (Interaction.Nearest(p) == target, 20, p);
        }

        [Test]
        public void CONTENT6_StartDistances_Bed2Point04_Bookshelf2Point48()
        {
            var s = RoomLayout.StartPosition;
            Assert.AreEqual(2.04, Vec2.Distance(s, RoomLayout.InteractPoint(InteractTargets.Bed)), 0.005);
            Assert.AreEqual(2.48, Vec2.Distance(s, RoomLayout.InteractPoint(InteractTargets.Bookshelf)), 0.005);
            foreach (var t in InteractTargets.All)
                Assert.Greater(Vec2.Distance(s, RoomLayout.InteractPoint(t)), 1.2, t);
        }

        [Test]
        public void F002_EveryTargetIsReachableOnFootFromTheStart_AndFromStandingUp()
        {
            foreach (var from in new[] { RoomLayout.StartPosition, RoomLayout.StandUpPosition })
                foreach (var t in InteractTargets.All)
                {
                    if (from.Equals(RoomLayout.StandUpPosition) && t == InteractTargets.Table) continue;   // already in range
                    var (reached, seconds, at) = WalkUntilPrompt(from, RoomLayout.InteractPoint(t), t);
                    Assert.IsTrue(reached, $"{t} from {from}: stuck at {at}");
                    // At 3 m/s the room (8 × 6 m) is crossed in a few seconds; the prompt must appear within 1.2 m.
                    Assert.Less(seconds, 4.0, $"{t} from {from}");
                    Assert.LessOrEqual(Vec2.Distance(at, RoomLayout.InteractPoint(t)), 1.2 + 1e-9, t);
                }
        }

        [Test]
        public void CONTENT6_SpawnStandAndInteractPointsAreFreeFloor()
        {
            Assert.IsTrue(Free(RoomLayout.StartPosition), "start");
            Assert.IsTrue(Free(RoomLayout.StandUpPosition), "stand-up position");
            foreach (var t in InteractTargets.All)
                Assert.IsTrue(Free(RoomLayout.InteractPoint(t)), t);
            // Stand-up position: 0.6 m south of the chair interaction point (F-003 edge case).
            var chairPoint = RoomLayout.InteractPoint(InteractTargets.Table);
            Assert.AreEqual(chairPoint.X, RoomLayout.StandUpPosition.X, 1e-9);
            Assert.AreEqual(chairPoint.Y - 0.6, RoomLayout.StandUpPosition.Y, 1e-9);
            Assert.AreEqual(chairPoint.X, RoomLayout.ChairPosition.X, 1e-9);
            Assert.AreEqual(chairPoint.Y, RoomLayout.ChairPosition.Y, 1e-9);
        }

        [Test]
        public void CONTENT6_ObstaclesAreTheBlockingFurnitureFootprints_InsideTheRoom()
        {
            var blocking = new List<FurnitureSpec>();
            foreach (var f in RoomLayout.Furniture)
                if (f.Blocks) blocking.Add(f);
            Assert.AreEqual(blocking.Count, RoomLayout.Obstacles.Count);
            for (int i = 0; i < blocking.Count; i++)
            {
                var f = blocking[i];
                var o = RoomLayout.Obstacles[i];
                Assert.AreEqual(f.Center.X - f.Width / 2, o.MinX, 1e-9, f.Id);
                Assert.AreEqual(f.Center.X + f.Width / 2, o.MaxX, 1e-9, f.Id);
                Assert.AreEqual(f.Center.Y - f.Depth / 2, o.MinZ, 1e-9, f.Id);
                Assert.AreEqual(f.Center.Y + f.Depth / 2, o.MaxZ, 1e-9, f.Id);
                Assert.GreaterOrEqual(o.MinX, -4 - 1e-9, f.Id);
                Assert.LessOrEqual(o.MaxX, 4 + 1e-9, f.Id);
                Assert.GreaterOrEqual(o.MinZ, -3 - 1e-9, f.Id);
                Assert.LessOrEqual(o.MaxZ, 3 + 1e-9, f.Id);
            }
            // The chair is sat on, not walked into (the seated character is moved onto it).
            foreach (var f in RoomLayout.Furniture)
                if (f.Id == "chair") Assert.IsFalse(f.Blocks);
        }

        [Test]
        public void R004_ExactRange1Point2IsInside_ForEveryTarget_1Point21IsNot()
        {
            // Directions chosen so that no other target is within 1.2 m.
            var cases = new (string target, Vec2 dir)[]
            {
                (InteractTargets.Table, V(1, 0)),       // 2.5 + 1.2 = 3.7 → |3.7 − 2.5| = 1.2000000000000002 in doubles
                (InteractTargets.Bookshelf, V(0, -1)),
                (InteractTargets.Counter, V(-1, 0)),
                (InteractTargets.Bed, V(0, -1)),
            };
            foreach (var (t, dir) in cases)
            {
                var p = RoomLayout.InteractPoint(t);
                Assert.AreEqual(t, Interaction.Nearest(p + dir * 1.2), $"{t} at 1.2 m");
                Assert.AreEqual(t, Interaction.Nearest(p + dir * 1.1), $"{t} at 1.1 m");
                Assert.IsNull(Interaction.Nearest(p + dir * 1.21), $"{t} at 1.21 m");
                Assert.IsNull(Interaction.Nearest(p + dir * 1.3), $"{t} at 1.3 m");
            }
        }

        [Test]
        public void R004_TieBetweenBookshelfAndBed_GoesToBookshelf()
        {
            // Midpoint of (0, 1.9) and (−2, 2): 1.00125 m from both → bookshelf comes before bed.
            var mid = V(-1.0, 1.95);
            Assert.AreEqual(Vec2.Distance(mid, RoomLayout.InteractPoint(InteractTargets.Bookshelf)),
                            Vec2.Distance(mid, RoomLayout.InteractPoint(InteractTargets.Bed)), 1e-12);
            Assert.AreEqual(InteractTargets.Bookshelf, Interaction.Nearest(mid));
            Assert.AreEqual(InteractTargets.Bed, Interaction.Nearest(V(-1.05, 1.95)));
            Assert.AreEqual(InteractTargets.Bookshelf, Interaction.Nearest(V(-0.95, 1.95)));
        }

        [Test]
        public void F002_SpeedIs3MetresPerSecondInEveryDirection_InOpenFloor()
        {
            var centre = V(0.5, -0.5);   // open floor: ≥ 0.6 m from every expanded obstacle after 0.2 s
            for (int k = 0; k < 16; k++)
            {
                double a = k * Math.PI / 8;
                var dir = V(Math.Cos(a), Math.Sin(a));
                var p = centre;
                for (int i = 0; i < 10; i++) p = RoomMotion.Step(p, dir, Dt);
                Assert.AreEqual(0.6, Vec2.Distance(centre, p), 1e-9, $"angle {k}/16");
            }
            // Frame size must not change the distance (1 s in one call = 50 frames).
            var one = RoomMotion.Step(centre, V(1, 0), 1.0);
            var p50 = centre;
            for (int i = 0; i < 50; i++) p50 = RoomMotion.Step(p50, V(1, 0), Dt);
            Assert.AreEqual(one.X, p50.X, 1e-9);
            Assert.AreEqual(3.0, one.X - centre.X, 1e-9);
        }
    }
}
