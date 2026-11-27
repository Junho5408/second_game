using System;
using NUnit.Framework;
using Origami.Core.Content;
using Origami.Core.Rules;

namespace Origami.Tests.Rules
{
    // ORIGAMI-003: C-006 Grading (R-009, F-003 grade rules).
    public class GradingTests
    {
        [TestCase(0, 0, 0)]
        [TestCase(1, 0, 2)]
        [TestCase(0, 1, 1)]
        [TestCase(2, 0, 4)]
        [TestCase(1, 1, 3)]
        [TestCase(0, 3, 3)]
        [TestCase(2, 5, 9)]
        public void R009_Penalty_IsMistakesTimesTwoPlusHints(int mistakes, int hints, int expected)
        {
            Assert.AreEqual(expected, Grading.Penalty(mistakes, hints));
        }

        [Test]
        public void R009_Penalty_NegativeInputs_Throw()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => Grading.Penalty(-1, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => Grading.Penalty(0, -1));
        }

        [TestCase(0, Grade.Star3)]
        [TestCase(1, Grade.Star2)]
        [TestCase(2, Grade.Star2)]
        [TestCase(3, Grade.Star1)]
        [TestCase(4, Grade.Star1)]
        [TestCase(100, Grade.Star1)]
        public void R009_GradeFor_Thresholds(int penalty, Grade expected)
        {
            Assert.AreEqual(expected, Grading.GradeFor(penalty));
        }

        [Test]
        public void R009_GradeFor_NegativePenalty_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => Grading.GradeFor(-1));
        }

        [TestCase(Grade.Star3, 150)]
        [TestCase(Grade.Star2, 100)]
        [TestCase(Grade.Star1, 60)]
        public void R011_GradePct(Grade grade, int expected)
        {
            Assert.AreEqual(expected, Grading.GradePct(grade));
        }

        [Test]
        public void R011_GradePct_InvalidGrade_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => Grading.GradePct((Grade)0));
            Assert.Throws<ArgumentOutOfRangeException>(() => Grading.GradePct((Grade)4));
        }
    }
}
