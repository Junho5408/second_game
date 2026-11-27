using System;
using System.Collections.Generic;
using NUnit.Framework;
using Sandbox.Core.Scoring;

namespace Sandbox.Tests.Scoring
{
    // SANDBOX-003 QA: exploratory / abnormal scenarios for C-001 ScoreCounter beyond AC-01..AC-22.
    // Expected values come from GAME_RULES.md (R-001..R-006) and C-001, computed independently of ScoreRules.
    public class ScoreCounterQaTests
    {
        // Total score after n consecutive in-window presses, from the GAME_RULES table:
        // 1..4 x1, 5..9 x2, 10..19 x3, 20+ x4.
        private static int ExpectedTotal(int n)
        {
            if (n <= 4) return n;
            if (n <= 9) return 4 + 2 * (n - 4);
            if (n <= 19) return 14 + 3 * (n - 9);
            return 44 + 4 * (n - 19);
        }

        private static int ExpectedMultiplier(int combo)
        {
            if (combo >= 20) return 4;
            if (combo >= 10) return 3;
            if (combo >= 5) return 2;
            return 1;
        }

        private static void AssertInvariant(ScoreSnapshot s)
        {
            Assert.GreaterOrEqual(s.Score, 0, s.ToString());
            Assert.GreaterOrEqual(s.Combo, 0, s.ToString());
            Assert.That(s.Multiplier, Is.InRange(1, 4), s.ToString());
            Assert.AreEqual(s.Combo == 0 ? 1 : ExpectedMultiplier(s.Combo), s.Multiplier, s.ToString());
        }

        [Test]
        public void LongRun_EveryPressFrom1To60_DeltaAndTotalFollowTable()
        {
            var c = new ScoreCounter();
            int prev = 0;
            for (int n = 1; n <= 60; n++)
            {
                double t = n * 0.25;
                Assert.IsTrue(c.Press(t), "press " + n);
                var s = c.GetState(t);
                Assert.AreEqual(new ScoreSnapshot(ExpectedTotal(n), n, ExpectedMultiplier(n)), s, "press " + n);
                Assert.AreEqual(ExpectedMultiplier(n), s.Score - prev, "delta of press " + n);
                prev = s.Score;
            }
        }

        [Test]
        public void ChainOfExactOneSecondGaps_NeverBreaks()
        {
            var c = new ScoreCounter();
            for (int i = 0; i < 25; i++) Assert.IsTrue(c.Press(i)); // gaps exactly 1.0
            Assert.AreEqual(new ScoreSnapshot(68, 25, 4), c.GetState(24.0));
            Assert.AreEqual(new ScoreSnapshot(68, 25, 4), c.GetState(25.0)); // exactly 1.0 later: not expired
            Assert.AreEqual(new ScoreSnapshot(68, 0, 1), c.GetState(25.25));
        }

        [TestCase(4)]
        [TestCase(5)]
        [TestCase(9)]
        [TestCase(10)]
        [TestCase(19)]
        [TestCase(20)]
        [TestCase(30)]
        public void BreakAtEachTierBoundary_RestartsAtX1_AndRebuilds(int combo)
        {
            var c = new ScoreCounter();
            for (int i = 0; i < combo; i++) c.Press(i * 0.5);
            double last = (combo - 1) * 0.5;
            int before = ExpectedTotal(combo);
            Assert.AreEqual(new ScoreSnapshot(before, combo, ExpectedMultiplier(combo)), c.GetState(last));

            // Break (gap 1.5), then build a second full chain of 20: the multiplier ladder restarts from x1.
            double t = last + 1.5;
            for (int n = 1; n <= 20; n++, t += 0.5)
            {
                Assert.IsTrue(c.Press(t));
                Assert.AreEqual(new ScoreSnapshot(before + ExpectedTotal(n), n, ExpectedMultiplier(n)), c.GetState(t));
            }
        }

        [Test]
        public void HundredPressesAtIdenticalTimestamp_AllCount_ComboClimbsToCap()
        {
            var c = new ScoreCounter();
            for (int i = 0; i < 100; i++) Assert.IsTrue(c.Press(3.0));
            Assert.AreEqual(new ScoreSnapshot(ExpectedTotal(100), 100, 4), c.GetState(3.0));
            Assert.AreEqual(368, c.GetState(3.0).Score);
        }

        [Test]
        public void AlternatingResetAndPress_EachPressIsAFirstPress()
        {
            var c = new ScoreCounter();
            for (int i = 0; i < 50; i++)
            {
                // Times jump around (backwards, same, far apart): after Reset all must be accepted as first presses.
                double t = (i % 2 == 0 ? -1.0 : 1.0) * i * 7.0;
                Assert.IsTrue(c.Press(t), "press " + i);
                Assert.AreEqual(new ScoreSnapshot(1, 1, 1), c.GetState(t), "press " + i);
                c.Reset();
                Assert.AreEqual(ScoreSnapshot.Initial, c.GetState(t));
                Assert.AreEqual(ScoreSnapshot.Initial, c.GetState(double.MaxValue));
            }
        }

        [Test]
        public void ResetMidCombo_DoesNotCarryComboOrScore()
        {
            var c = new ScoreCounter();
            for (int i = 0; i < 12; i++) c.Press(i * 0.5); // combo 12, x3
            c.Reset();
            for (int n = 0; n < 5; n++) c.Press(5.5 + n * 0.25); // 5.5..6.5, within window of pre-reset press 5.5
            Assert.AreEqual(new ScoreSnapshot(ExpectedTotal(5), 5, 2), c.GetState(6.5));
        }

        [Test]
        public void QueriesFarInFutureAndPast_DoNotThrowAndFollowR004()
        {
            var c = new ScoreCounter();
            for (int i = 0; i < 5; i++) c.Press(i * 0.5); // (6,5,2) at t=2.0
            Assert.AreEqual(new ScoreSnapshot(6, 0, 1), c.GetState(1e300));
            Assert.AreEqual(new ScoreSnapshot(6, 0, 1), c.GetState(double.MaxValue));
            Assert.AreEqual(new ScoreSnapshot(6, 5, 2), c.GetState(-double.MaxValue)); // now < t_prev: not expired
            Assert.AreEqual(new ScoreSnapshot(6, 5, 2), c.GetState(double.PositiveInfinity));
            // Queries changed nothing.
            Assert.IsTrue(c.Press(2.5));
            Assert.AreEqual(new ScoreSnapshot(8, 6, 2), c.GetState(2.5));
        }

        [Test]
        public void ExtremeFiniteTimes_AreValid_AndOverflowingGapBreaksCombo()
        {
            var c = new ScoreCounter();
            Assert.IsTrue(c.Press(-double.MaxValue));
            Assert.IsTrue(c.Press(double.MaxValue)); // gap overflows to +Inf -> > 1.0 -> break
            Assert.AreEqual(new ScoreSnapshot(2, 1, 1), c.GetState(double.MaxValue));
            Assert.IsTrue(c.Press(double.MaxValue)); // same time
            Assert.AreEqual(new ScoreSnapshot(3, 2, 1), c.GetState(double.MaxValue));
            Assert.IsFalse(c.Press(double.PositiveInfinity));
            Assert.AreEqual(new ScoreSnapshot(3, 2, 1), c.GetState(double.PositiveInfinity));
        }

        [Test]
        public void LargeButPreciseTimes_KeepCombo()
        {
            // ~31.7 million years in seconds; 0.5 steps are still exactly representable.
            const double baseT = 1e15;
            var c = new ScoreCounter();
            for (int i = 0; i < 10; i++) Assert.IsTrue(c.Press(baseT + i * 0.5));
            Assert.AreEqual(new ScoreSnapshot(17, 10, 3), c.GetState(baseT + 4.5));
            Assert.AreEqual(new ScoreSnapshot(17, 0, 1), c.GetState(baseT + 6.0));
        }

        [Test]
        public void NegativeZeroAfterZero_IsSameTime_NotBackwards()
        {
            var c = new ScoreCounter();
            Assert.IsTrue(c.Press(0.0));
            Assert.IsTrue(c.Press(-0.0));
            Assert.AreEqual(new ScoreSnapshot(2, 2, 1), c.GetState(0.0));
        }

        [Test]
        public void TinyBackwardsStep_IsRejected()
        {
            var c = new ScoreCounter();
            Assert.IsTrue(c.Press(1.0));
            Assert.IsFalse(c.Press(1.0 - 1e-15));
            Assert.AreEqual(new ScoreSnapshot(1, 1, 1), c.GetState(1.0));
        }

        [Test]
        public void BackwardsPressAfterExpiry_StillIgnored_ThenForwardPressBreaks()
        {
            var c = new ScoreCounter();
            for (int i = 0; i < 6; i++) c.Press(i * 0.5); // t_prev 2.5, combo 6
            Assert.AreEqual(new ScoreSnapshot(8, 0, 1), c.GetState(10.0));
            Assert.IsFalse(c.Press(2.0));
            Assert.AreEqual(new ScoreSnapshot(8, 6, 2), c.GetState(2.5));
            Assert.IsTrue(c.Press(10.0));
            Assert.AreEqual(new ScoreSnapshot(9, 1, 1), c.GetState(10.0));
        }

        [Test]
        public void ExpiryQueryAndNextPress_AgreeOnTheBoundary_ForDecimalTimes()
        {
            // W-1 / DECISIONS 2026-11-27: no epsilon, so 1.2 -> 2.2 may break. Whatever the double gap says,
            // GetState(t + 1.0) expiring must coincide with Press(t + 1.0) breaking the combo.
            for (int a = -200; a <= 200; a++)
            {
                double t = a / 10.0;
                double u = t + 1.0;
                var c = new ScoreCounter();
                c.Press(t);
                bool expired = c.GetState(u).Combo == 0;
                Assert.IsTrue(c.Press(u));
                bool broke = c.GetState(u).Combo == 1;
                Assert.AreEqual(expired, broke, "t=" + t.ToString("R"));
            }
        }

        // ---- randomized differential test against an independent model written from GAME_RULES R-001..R-005 ----

        private sealed class Model
        {
            public int Score;
            public int Combo;
            public double? Last;

            public bool Press(double t)
            {
                if (double.IsNaN(t) || double.IsInfinity(t)) return false;
                if (Last.HasValue && t < Last.Value) return false;
                Combo = Last.HasValue && t - Last.Value <= 1.0 ? Combo + 1 : 1;
                Score += ExpectedMultiplier(Combo);
                Last = t;
                return true;
            }

            public ScoreSnapshot Query(double now)
            {
                if (!Last.HasValue) return new ScoreSnapshot(0, 0, 1);
                bool finite = !double.IsNaN(now) && !double.IsInfinity(now);
                if (finite && now - Last.Value > 1.0) return new ScoreSnapshot(Score, 0, 1);
                return new ScoreSnapshot(Score, Combo, ExpectedMultiplier(Combo));
            }

            public void Reset()
            {
                Score = 0;
                Combo = 0;
                Last = null;
            }
        }

        private static readonly double[] Gaps = { 0.0, 0.0, 0.25, 0.5, 0.5, 0.75, 1.0, 1.0, 1.25, 2.0, -0.5, -0.25 };
        private static readonly double[] Specials = { double.NaN, double.PositiveInfinity, double.NegativeInfinity };

        [TestCase(1)]
        [TestCase(7)]
        [TestCase(42)]
        [TestCase(2026)]
        public void RandomSequences_MatchIndependentModel_AndKeepInvariants(int seed)
        {
            var rng = new Random(seed);
            var c = new ScoreCounter();
            var m = new Model();
            double clock = 0.0;
            int lastScore = 0;
            var log = new List<string>();

            for (int step = 0; step < 5000; step++)
            {
                int op = rng.Next(100);
                if (op < 70)
                {
                    double t = rng.Next(20) == 0 ? Specials[rng.Next(Specials.Length)] : clock + Gaps[rng.Next(Gaps.Length)];
                    if (!double.IsNaN(t) && !double.IsInfinity(t)) clock = Math.Max(clock, t);
                    log.Add("P" + t.ToString("R"));
                    Assert.AreEqual(m.Press(t), c.Press(t), string.Join(" ", log));
                }
                else if (op < 75)
                {
                    log.Add("R");
                    c.Reset();
                    m.Reset();
                    if (rng.Next(2) == 0) clock -= 10.0; // later presses earlier than pre-reset ones
                    lastScore = 0;
                }
                else
                {
                    double now = rng.Next(10) == 0 ? Specials[rng.Next(Specials.Length)] : clock + Gaps[rng.Next(Gaps.Length)] * 2;
                    log.Add("Q" + now.ToString("R"));
                    var s = c.GetState(now);
                    Assert.AreEqual(m.Query(now), s, string.Join(" ", log));
                    AssertInvariant(s);
                    Assert.GreaterOrEqual(s.Score, lastScore, "score decreased without reset");
                    lastScore = s.Score;
                }
                if (log.Count > 40) log.RemoveAt(0);
            }
        }
    }
}
