using System;
using NUnit.Framework;
using Origami.Core.Geometry;
using Origami.Core.Room;

namespace Origami.Tests.Room
{
    /// <summary>
    /// ORIGAMI-014: F-002 numbers from <see cref="RoomMotion.Step"/> alone (C-180, D-130). Tolerances follow FEATURE_SPECS
    /// "공통 표기": world position ±0.05 m, movement ±0.15 m (the Core result is exact, so the tests use tight bounds where the
    /// expected value is exact).
    /// </summary>
    public class RoomMotionTests
    {
        private const double Pos = 1e-9;

        private static Vec2 V(double x, double z) => new Vec2(x, z);

        /// <summary>Holds <paramref name="move"/> for <paramref name="seconds"/> in frames of <paramref name="dt"/> (like the Runtime).</summary>
        private static Vec2 Hold(Vec2 start, Vec2 move, double seconds, double dt = 0.02)
        {
            var p = start;
            int frames = (int)Math.Round(seconds / dt);
            for (int i = 0; i < frames; i++) p = RoomMotion.Step(p, move, dt);
            return p;
        }

        [Test]
        public void C180_StartEast1Second_XIs1Point4()
        {
            var p = RoomMotion.Step(RoomLayout.StartPosition, V(1, 0), 1.0);
            Assert.AreEqual(1.4, p.X, Pos);
            Assert.AreEqual(0.0, p.Y, Pos);
        }

        [Test]
        public void F002_AC02_D1Second_XPlus3_ZKept()
        {
            var p = Hold(RoomLayout.StartPosition, V(1, 0), 1.0);
            Assert.AreEqual(-1.6 + 3.0, p.X, 0.15);
            Assert.AreEqual(0.0, p.Y, 0.05);
        }

        [Test]
        public void F002_AC03_DiagonalIsNormalised_1Point5MetresIn0Point5Seconds()
        {
            var start = RoomLayout.StartPosition;
            var p = Hold(start, V(1, 1), 0.5);
            Assert.AreEqual(1.5, Vec2.Distance(start, p), 0.1);
            Assert.AreEqual(1.5, Vec2.Distance(start, RoomMotion.Step(start, V(1, 1), 0.5)), 1e-9, "exact in one call");
        }

        [Test]
        public void F002_AC04_West5Seconds_StopsAtTheWestWall()
        {
            var p = Hold(RoomLayout.StartPosition, V(-1, 0), 5.0);
            Assert.That(p.X, Is.InRange(-3.75, -3.65));
            Assert.AreEqual(-3.7, p.X, Pos);
            Assert.AreEqual(0.0, p.Y, 0.05);
        }

        [Test]
        public void F002_AC04b_WestIntoTheBed_StopsAtMinus2Point2()
        {
            var p = Hold(V(-1.6, 2.0), V(-1, 0), 2.0);
            Assert.AreEqual(-2.2, p.X, Pos);
            Assert.AreEqual(2.0, p.Y, Pos);
            Assert.AreEqual(-2.2, RoomMotion.Step(V(-1.6, 2.0), V(-1, 0), 2.0).X, Pos, "one call");
        }

        [Test]
        public void F002_AC04b_NorthIntoTheBookshelf_StopsAt2Point25()
        {
            var p = Hold(V(0.0, 1.0), V(0, 1), 2.0);
            Assert.AreEqual(2.25, p.Y, Pos);
            Assert.AreEqual(0.0, p.X, Pos);
        }

        [Test]
        public void Walls_AllFourSides()
        {
            Assert.AreEqual(3.7, Hold(V(0, 0), V(1, 0), 5).X, Pos, "east");
            Assert.AreEqual(-2.7, Hold(V(0, 0), V(0, -1), 5).Y, Pos, "south");
            Assert.AreEqual(2.7, Hold(V(-1.6, -1), V(0, 1), 5).Y, Pos, "north (x -1.6 passes no furniture)");
        }

        [Test]
        public void Obstacles_EachBlockingFurnitureStopsTheCharacter()
        {
            Assert.AreEqual(1.5, Hold(V(0, 1.6), V(1, 0), 3).X, Pos, "table west face 1.8 − 0.3");
            Assert.AreEqual(0.85, Hold(V(2.5, 0.2), V(0, 1), 2).Y, Pos, "table south face 1.15 − 0.3 (the chair does not block)");
            Assert.AreEqual(-1.7, Hold(V(2.5, 0), V(0, -1), 2).Y, Pos, "counter north face −2.0 + 0.3");
            Assert.AreEqual(3.1, Hold(V(2.0, 0.6), V(1, 0), 2).X, Pos, "trash west face 3.4 − 0.3");
        }

        [Test]
        public void Sliding_AlongAnObstacleKeepsTheFreeAxis()
        {
            // Pressing north-east against the bookshelf's south face: z stops at 2.25, x keeps moving.
            var p = Hold(V(-0.5, 2.0), V(1, 1), 0.5);
            Assert.AreEqual(2.25, p.Y, Pos);
            Assert.Greater(p.X, -0.5 + 0.9);
        }

        [Test]
        public void ZeroOrInvalidInput_ReturnsThePosition()
        {
            var s = RoomLayout.StartPosition;
            Assert.AreEqual(s, RoomMotion.Step(s, V(1, 0), 0));
            Assert.AreEqual(s, RoomMotion.Step(s, V(1, 0), -1));
            Assert.AreEqual(s, RoomMotion.Step(s, V(1, 0), double.NaN));
            Assert.AreEqual(s, RoomMotion.Step(s, V(0, 0), 1));
            Assert.AreEqual(s, RoomMotion.Step(s, V(1e-7, 0), 1), "length < 1e-6");
            Assert.AreEqual(s, RoomMotion.Step(s, V(double.NaN, double.NaN), 1));
        }

        [Test]
        public void MoveComponents_AreClampedToUnitLength()
        {
            var s = RoomLayout.StartPosition;
            Assert.AreEqual(RoomMotion.Step(s, V(1, 0), 0.5), RoomMotion.Step(s, V(5, 0), 0.5));
            var half = RoomMotion.Step(s, V(0.5, 0), 0.5);
            Assert.AreEqual(-1.6 + 0.75, half.X, Pos, "partial input keeps its length");
        }

        [Test]
        public void HugeDt_StaysInsideTheRoomAndTerminates()
        {
            var p = RoomMotion.Step(RoomLayout.StartPosition, V(1, 1), double.PositiveInfinity);
            Assert.That(p.X, Is.InRange(-3.7, 3.7));
            Assert.That(p.Y, Is.InRange(-2.7, 2.7));
            var q = RoomMotion.Step(RoomLayout.StartPosition, V(-1, 0), 1e12);
            Assert.AreEqual(-3.7, q.X, Pos);
        }

        [Test]
        public void Deterministic_SameInputSameBits()
        {
            var a = Hold(RoomLayout.StartPosition, V(0.7, -0.3), 3.3, 0.016);
            var b = Hold(RoomLayout.StartPosition, V(0.7, -0.3), 3.3, 0.016);
            Assert.AreEqual(a, b);
        }

        [Test]
        public void NeverEntersAnExpandedObstacle_RandomWalk()
        {
            // Deterministic pseudo-random walk (LCG, no System.Random): the centre never ends inside an obstacle grown by r.
            var p = RoomLayout.StartPosition;
            uint seed = 12345;
            double r = RoomLayout.CharacterRadius;
            for (int i = 0; i < 4000; i++)
            {
                seed = seed * 1664525u + 1013904223u;
                double mx = ((seed >> 8) % 3) - 1.0;
                double mz = ((seed >> 16) % 3) - 1.0;
                p = RoomMotion.Step(p, V(mx, mz), 0.05 + (seed % 7) * 0.01);
                Assert.That(p.X, Is.InRange(RoomLayout.MinX + r - 1e-9, RoomLayout.MaxX - r + 1e-9));
                Assert.That(p.Y, Is.InRange(RoomLayout.MinZ + r - 1e-9, RoomLayout.MaxZ - r + 1e-9));
                foreach (var b in RoomLayout.Obstacles)
                {
                    bool inside = p.X > b.MinX - r + 1e-9 && p.X < b.MaxX + r - 1e-9 && p.Y > b.MinZ - r + 1e-9 && p.Y < b.MaxZ + r - 1e-9;
                    Assert.IsFalse(inside, "inside obstacle at step " + i + ": " + p);
                }
            }
        }
    }
}
