using System;
using NUnit.Framework;
using Origami.Core.Geometry;
using Origami.Core.Recognition;

namespace Origami.Tests.Recognition
{
    // C-110 Grading / D4 — F-009 AC-04 (owned by ORIGAMI-009).
    public class GradingTests
    {
        [TestCase(0.97, Grade.Star3)]
        [TestCase(0.969, Grade.Star2)]
        [TestCase(0.93, Grade.Star2)]
        [TestCase(0.929, Grade.Star1)]
        [TestCase(0.85, Grade.Star1)]
        [TestCase(0.849, Grade.None)]
        [TestCase(1.0, Grade.Star3)]
        [TestCase(0.0, Grade.None)]
        public void F009_AC04_GradeBoundaries(double score, Grade expected)
        {
            Assert.AreEqual(expected, Grading.FromScore(score));
        }

        [Test]
        public void F009_AC04_PercentIsFloorWithEpsilon()
        {
            Assert.AreEqual(97, Grading.Percent(0.97));
            Assert.AreEqual(96, Grading.Percent(0.969));
            Assert.AreEqual(93, Grading.Percent(0.93));
            Assert.AreEqual(92, Grading.Percent(0.929));
            Assert.AreEqual(85, Grading.Percent(0.85));
            Assert.AreEqual(84, Grading.Percent(0.849));
            Assert.AreEqual(100, Grading.Percent(1.0));
            Assert.AreEqual(57, Grading.Percent(0.57));   // 0.57 * 100 = 56.99999999999999
            Assert.AreEqual(0, Grading.Percent(0));
        }

        [Test]
        public void FromPercent_Thresholds()
        {
            Assert.AreEqual(97, Grading.Grade3Min);
            Assert.AreEqual(93, Grading.Grade2Min);
            Assert.AreEqual(85, Grading.MatchMin);
            Assert.AreEqual(Grade.Star3, Grading.FromPercent(97));
            Assert.AreEqual(Grade.Star2, Grading.FromPercent(96));
            Assert.AreEqual(Grade.Star2, Grading.FromPercent(93));
            Assert.AreEqual(Grade.Star1, Grading.FromPercent(92));
            Assert.AreEqual(Grade.Star1, Grading.FromPercent(85));
            Assert.AreEqual(Grade.None, Grading.FromPercent(84));
            Assert.AreEqual(Grade.None, Grading.FromPercent(-5));
        }

        [Test]
        public void D4_AllEightMaps()
        {
            var p = new Vec2(2, 3);
            var expected = new[]
            {
                new Vec2(2, 3), new Vec2(-3, 2), new Vec2(-2, -3), new Vec2(3, -2),
                new Vec2(-2, 3), new Vec2(-3, -2), new Vec2(2, -3), new Vec2(3, 2),
            };
            Assert.AreEqual(8, D4.Count);
            for (int g = 0; g < D4.Count; g++) Assert.AreEqual(expected[g], D4.Apply(g, p), "g" + g);
            Assert.Throws<ArgumentOutOfRangeException>(() => D4.Apply(8, p));
            Assert.Throws<ArgumentOutOfRangeException>(() => D4.Apply(-1, p));
        }
    }
}
