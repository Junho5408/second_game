using NUnit.Framework;
using Sandbox.Core.Scoring;

namespace Sandbox.Tests.Scoring
{
    // SANDBOX-003: F-001 acceptance criteria AC-01..AC-22 against C-001 ScoreCounter. Engine-free.
    // Query-time convention (FEATURE_SPECS): unless stated, state is queried at now = last valid press time.
    public class ScoreCounterTests
    {
        private static ScoreCounter PressAll(params double[] times)
        {
            var c = new ScoreCounter();
            foreach (var t in times) c.Press(t);
            return c;
        }

        // 0.0, 0.5, 1.0, ... (count presses, 0.5 s apart)
        private static double[] Evenly(int count)
        {
            var times = new double[count];
            for (int i = 0; i < count; i++) times[i] = i * 0.5;
            return times;
        }

        private static double Last(double[] times) => times[times.Length - 1];

        [Test]
        public void AC01_NewCounter_IsScore0Combo0Multiplier1()
        {
            var c = new ScoreCounter();
            Assert.AreEqual(new ScoreSnapshot(0, 0, 1), c.GetState(0.0));
        }

        [Test]
        public void AC02_OnePressAt0_IsScore1Combo1Multiplier1()
        {
            var c = new ScoreCounter();
            Assert.IsTrue(c.Press(0.0));
            Assert.AreEqual(new ScoreSnapshot(1, 1, 1), c.GetState(0.0));
        }

        [Test]
        public void AC03_PressesAt0_05_10_GiveScore3Combo3()
        {
            var c = PressAll(0.0, 0.5, 1.0);
            Assert.AreEqual(new ScoreSnapshot(3, 3, 1), c.GetState(1.0));
        }

        [Test]
        public void AC04_GapOfExactly1Second_KeepsCombo()
        {
            var c = PressAll(0.0, 1.0);
            Assert.AreEqual(new ScoreSnapshot(2, 2, 1), c.GetState(1.0));
        }

        [Test]
        public void AC05_GapOver1Second_BreaksCombo()
        {
            var c = PressAll(0.0, 1.001);
            Assert.AreEqual(new ScoreSnapshot(2, 1, 1), c.GetState(1.001));
        }

        [Test]
        public void AC06_FifthPress_AddsTwoPoints_Total6Multiplier2()
        {
            var c = PressAll(0.0, 0.5, 1.0, 1.5);
            Assert.AreEqual(4, c.GetState(1.5).Score);
            c.Press(2.0);
            Assert.AreEqual(new ScoreSnapshot(6, 5, 2), c.GetState(2.0));
        }

        [Test]
        public void AC07_TenPresses_Total17Multiplier3()
        {
            var times = Evenly(10);
            Assert.AreEqual(new ScoreSnapshot(17, 10, 3), PressAll(times).GetState(Last(times)));
        }

        [Test]
        public void AC08_TwentyPresses_Total48Multiplier4()
        {
            var times = Evenly(20);
            Assert.AreEqual(new ScoreSnapshot(48, 20, 4), PressAll(times).GetState(Last(times)));
        }

        [Test]
        public void AC09_TwentyFivePresses_Total68MultiplierStaysAt4()
        {
            var times = Evenly(25);
            Assert.AreEqual(new ScoreSnapshot(68, 25, 4), PressAll(times).GetState(Last(times)));
        }

        [Test]
        public void AC10_Combo7_ThenPressAfter1_5s_ResetsComboAndAddsOnePoint()
        {
            var times = Evenly(7); // 0.0..3.0, combo 7, score 4*1 + 3*2 = 10
            var c = PressAll(times);
            Assert.AreEqual(new ScoreSnapshot(10, 7, 2), c.GetState(3.0));
            Assert.IsTrue(c.Press(4.5));
            Assert.AreEqual(new ScoreSnapshot(11, 1, 1), c.GetState(4.5));
        }

        [Test]
        public void AC11_ComboExpiresOnlyAfterMoreThan1Second()
        {
            var c = PressAll(0.0);
            Assert.AreEqual(new ScoreSnapshot(1, 1, 1), c.GetState(1.0));
            Assert.AreEqual(new ScoreSnapshot(1, 0, 1), c.GetState(1.5));
        }

        [Test]
        public void AC12_BackwardsPress_IsIgnored_ThenNextPressContinuesCombo()
        {
            var c = new ScoreCounter();
            Assert.IsTrue(c.Press(2.0));
            Assert.IsFalse(c.Press(1.0));
            Assert.AreEqual(new ScoreSnapshot(1, 1, 1), c.GetState(2.0));
            Assert.IsTrue(c.Press(2.5));
            Assert.AreEqual(new ScoreSnapshot(2, 2, 1), c.GetState(2.5));
        }

        [Test]
        public void AC13_TwoPressesAtSameTime_CountTwiceAndKeepCombo()
        {
            var c = new ScoreCounter();
            Assert.IsTrue(c.Press(0.0));
            Assert.IsTrue(c.Press(0.0));
            Assert.AreEqual(new ScoreSnapshot(2, 2, 1), c.GetState(0.0));
        }

        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        [TestCase(double.NegativeInfinity)]
        public void AC14_NonFinitePress_ChangesNothing(double bad)
        {
            var c = PressAll(0.0, 0.5);
            Assert.IsFalse(c.Press(bad));
            Assert.AreEqual(new ScoreSnapshot(2, 2, 1), c.GetState(0.5));
        }

        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        [TestCase(double.NegativeInfinity)]
        public void AC14_N3_NonFinitePress_OnNewCounter_ChangesNothing(double bad)
        {
            var c = new ScoreCounter();
            Assert.DoesNotThrow(() => Assert.IsFalse(c.Press(bad)));
            Assert.AreEqual(ScoreSnapshot.Initial, c.GetState(0.0));
            // No phantom t_prev: next press is a first press at any finite time.
            Assert.IsTrue(c.Press(-100.0));
            Assert.AreEqual(new ScoreSnapshot(1, 1, 1), c.GetState(-100.0));
        }

        [Test]
        public void AC15_ResetFromScore17Combo10_ReturnsToInitial()
        {
            var times = Evenly(10);
            var c = PressAll(times);
            Assert.AreEqual(new ScoreSnapshot(17, 10, 3), c.GetState(Last(times)));
            c.Reset();
            Assert.AreEqual(new ScoreSnapshot(0, 0, 1), c.GetState(Last(times)));
        }

        [Test]
        public void AC16_PressAfterReset_IsFirstPressEvenIfEarlier()
        {
            var c = PressAll(5.0);
            c.Reset();
            Assert.IsTrue(c.Press(1.0));
            Assert.AreEqual(new ScoreSnapshot(1, 1, 1), c.GetState(1.0));
        }

        [Test]
        public void AC17_ResetOnInitialState_DoesNotThrowAndStaysInitial()
        {
            var c = new ScoreCounter();
            Assert.DoesNotThrow(() => c.Reset());
            Assert.DoesNotThrow(() => c.Reset());
            Assert.AreEqual(ScoreSnapshot.Initial, c.GetState(0.0));
        }

        [Test]
        public void AC18_ReplayingSameSequence_GivesSameResult()
        {
            double[] times = { 0.0, 0.3, 0.3, 1.2, 2.5, 2.0, double.NaN, 3.0, 3.9, 4.9, 6.0, 6.1 };
            var a = PressAll(times);
            var b = PressAll(times);
            Assert.AreEqual(a.GetState(6.1), b.GetState(6.1));
            Assert.AreEqual(a.GetState(8.0), b.GetState(8.0));
        }

        [Test]
        public void AC19_QueryEarlierThanLastPress_IsNotExpired()
        {
            var c = PressAll(2.0);
            Assert.AreEqual(new ScoreSnapshot(1, 1, 1), c.GetState(1.0));
        }

        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        [TestCase(double.NegativeInfinity)]
        public void AC20_NonFiniteQuery_ReportsStateAfterLastValidPress(double bad)
        {
            var c = PressAll(Evenly(5));
            ScoreSnapshot s = default;
            Assert.DoesNotThrow(() => s = c.GetState(bad));
            Assert.AreEqual(new ScoreSnapshot(6, 5, 2), s);
        }

        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        [TestCase(double.NegativeInfinity)]
        public void AC20_N3_NonFiniteQuery_OnNewCounter_ReportsInitial(double bad)
        {
            var c = new ScoreCounter();
            ScoreSnapshot s = default;
            Assert.DoesNotThrow(() => s = c.GetState(bad));
            Assert.AreEqual(new ScoreSnapshot(0, 0, 1), s);
        }

        [Test]
        public void AC20_QueryDoesNotChangeState()
        {
            var c = PressAll(Evenly(5));
            c.GetState(double.NaN);
            c.GetState(100.0);
            c.GetState(double.PositiveInfinity);
            Assert.AreEqual(new ScoreSnapshot(6, 5, 2), c.GetState(2.0));
            Assert.IsTrue(c.Press(2.5));
            Assert.AreEqual(new ScoreSnapshot(8, 6, 2), c.GetState(2.5));
        }

        [Test]
        public void AC21_NegativeFiniteTimes_AreValid()
        {
            var c = new ScoreCounter();
            Assert.IsTrue(c.Press(-1.0));
            Assert.AreEqual(new ScoreSnapshot(1, 1, 1), c.GetState(-1.0));
            Assert.IsTrue(c.Press(-0.5));
            Assert.AreEqual(new ScoreSnapshot(2, 2, 1), c.GetState(-0.5));
        }

        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        [TestCase(double.NegativeInfinity)]
        public void AC22_InvalidPressBetweenValidOnes_DoesNotMoveTPrev(double bad)
        {
            var c = PressAll(0.0, bad, 0.5);
            Assert.AreEqual(new ScoreSnapshot(2, 2, 1), c.GetState(0.5));
        }
    }
}
