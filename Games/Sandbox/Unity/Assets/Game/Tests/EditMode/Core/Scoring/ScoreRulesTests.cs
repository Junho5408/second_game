using NUnit.Framework;
using Sandbox.Core.Scoring;

namespace Sandbox.Tests.Scoring
{
    // SANDBOX-003: C-002 ScoreRules.MultiplierForCombo (GAME_RULES.md multiplier table). Engine-free.
    public class ScoreRulesTests
    {
        [TestCase(int.MinValue, 1)]
        [TestCase(-1, 1)]
        [TestCase(0, 1)]
        [TestCase(1, 1)]
        [TestCase(4, 1)]
        [TestCase(5, 2)]
        [TestCase(9, 2)]
        [TestCase(10, 3)]
        [TestCase(19, 3)]
        [TestCase(20, 4)]
        [TestCase(21, 4)]
        [TestCase(int.MaxValue, 4)]
        public void MultiplierForCombo_FollowsTable(int combo, int expected)
        {
            Assert.AreEqual(expected, ScoreRules.MultiplierForCombo(combo));
        }

        [Test]
        public void MultiplierForCombo_NeverExceedsMaxMultiplier()
        {
            for (int combo = -5; combo <= 100; combo++)
            {
                int m = ScoreRules.MultiplierForCombo(combo);
                Assert.That(m, Is.InRange(1, ScoreRules.MaxMultiplier), "combo " + combo);
            }
        }
    }
}
