using System.Collections;
using NUnit.Framework;
using Sandbox.Core.Scoring;
using Sandbox.Runtime.Gameplay;
using Sandbox.Runtime.UI;
using UnityEngine;
using UnityEngine.TestTools;

namespace Sandbox.Tests.PlayMode.Scoring
{
    /// <summary>
    /// SANDBOX-004 / C-004: input → Core → Current → on-screen text.
    /// Synchronous tests assert right after SubmitPress/ProcessFrame (R-1), before any Update can
    /// overwrite Current with GetState(real time); they use exactly representable times (0.5 steps, W-1).
    /// Frame-based tests (BUG-SANDBOX-004-1): a UnityTest coroutine resumes after <c>yield return null</c>
    /// in the next frame AFTER Update and BEFORE LateUpdate. So right after one yield, Current is this
    /// frame's GetState(t) while ScoreView.Text still comes from an earlier LateUpdate. Frame tests therefore
    /// yield at least twice (so a full Update → LateUpdate has run in between) and only assert states that
    /// are the same for every real time ≥ t0: either no press at all (Initial), or presses placed far in the
    /// past (t0 - 10), whose combo is already expired in every later frame whatever the frame duration.
    /// Assumes no real keyboard/mouse input during the run (always true in batchmode).
    /// </summary>
    public class ScoreCounterControllerTests
    {
        private GameObject _go;

        [TearDown]
        public void TearDown()
        {
            if (_go != null) Object.Destroy(_go);
            _go = null;
        }

        private ScoreCounterController NewController()
        {
            _go = new GameObject("TestScoreCounter");
            return _go.AddComponent<ScoreCounterController>();
        }

        [Test]
        public void Current_StartsAtInitial()
        {
            var c = NewController();
            Assert.AreEqual(ScoreSnapshot.Initial, c.Current);
        }

        [Test]
        public void SubmitPress_FirstPress_ScoresOne()
        {
            var c = NewController();
            c.SubmitPress(10.0);
            Assert.AreEqual(new ScoreSnapshot(1, 1, 1), c.Current);
        }

        [Test]
        public void SubmitPress_GapExactlyOneSecond_KeepsCombo()
        {
            var c = NewController();
            c.SubmitPress(10.0);
            c.SubmitPress(11.0);
            Assert.AreEqual(new ScoreSnapshot(2, 2, 1), c.Current);
        }

        [Test]
        public void SubmitPress_GapOverOneSecond_RestartsCombo()
        {
            var c = NewController();
            c.SubmitPress(10.0);
            c.SubmitPress(11.0);
            c.SubmitPress(12.5);
            Assert.AreEqual(new ScoreSnapshot(3, 1, 1), c.Current);
        }

        [Test]
        public void SubmitPress_FifthPressInCombo_UsesMultiplierTwo()
        {
            var c = NewController();
            for (int i = 0; i < 5; i++) c.SubmitPress(10.0 + 0.5 * i);
            // 1+1+1+1 then combo 5 → x2 → +2
            Assert.AreEqual(new ScoreSnapshot(6, 5, 2), c.Current);
        }

        [Test]
        public void SubmitPress_InvalidTimes_AreIgnored()
        {
            var c = NewController();
            c.SubmitPress(double.NaN);
            Assert.AreEqual(ScoreSnapshot.Initial, c.Current);

            c.SubmitPress(10.0);
            c.SubmitPress(9.5); // time going backwards
            Assert.AreEqual(new ScoreSnapshot(1, 1, 1), c.Current);

            c.SubmitPress(double.PositiveInfinity);
            Assert.AreEqual(new ScoreSnapshot(1, 1, 1), c.Current);
        }

        [Test]
        public void SubmitReset_ReturnsToInitial_AndNextPressIsFirst()
        {
            var c = NewController();
            c.SubmitPress(10.0);
            c.SubmitPress(10.5);
            c.SubmitReset();
            Assert.AreEqual(ScoreSnapshot.Initial, c.Current);

            c.SubmitPress(1.0); // earlier than before the reset: still a valid first press
            Assert.AreEqual(new ScoreSnapshot(1, 1, 1), c.Current);
        }

        [Test]
        public void ProcessFrame_SpaceAndClickSameFrame_CountAsTwoPresses()
        {
            var c = NewController();
            c.ProcessFrame(10.0, resetDown: false, spaceDown: true, clickDown: true);
            Assert.AreEqual(new ScoreSnapshot(2, 2, 1), c.Current);
        }

        [Test]
        public void ProcessFrame_ResetAndPressSameFrame_ResetFirst()
        {
            var c = NewController();
            c.ProcessFrame(10.0, false, true, false);
            c.ProcessFrame(10.5, false, true, false);
            c.ProcessFrame(11.0, resetDown: true, spaceDown: true, clickDown: false);
            Assert.AreEqual(new ScoreSnapshot(1, 1, 1), c.Current);
        }

        [Test]
        public void ProcessFrame_NoInput_ShowsComboExpiry()
        {
            var c = NewController();
            c.ProcessFrame(10.0, false, true, false);
            c.ProcessFrame(11.0, false, false, false);
            Assert.AreEqual(new ScoreSnapshot(1, 1, 1), c.Current, "gap exactly 1.0 is not expired");
            c.ProcessFrame(11.5, false, false, false);
            Assert.AreEqual(new ScoreSnapshot(1, 0, 1), c.Current, "gap 1.5 expires combo, score kept");
        }

        [Test]
        public void Format_ShowsScoreComboAndMultiplier()
        {
            Assert.AreEqual("Score: 6\nCombo: 5\nx2", ScoreView.Format(new ScoreSnapshot(6, 5, 2)));
            Assert.AreEqual("Score: 0\nCombo: 0\nx1", ScoreView.Format(ScoreSnapshot.Initial));
        }

        [UnityTest]
        public IEnumerator View_ShowsCurrentAfterFrame()
        {
            var c = NewController();
            var view = _go.AddComponent<ScoreView>();
            view.Bind(c);
            Assert.AreEqual(ScoreView.Format(ScoreSnapshot.Initial), view.Text);

            // Presses 10 s in the past: every later frame has real time t >= t0, so t - (t0 - 10) > 1.0 and
            // the controller's Update always yields (3,0,1), independent of frame duration (R-004 expiry).
            double past = Time.unscaledTimeAsDouble - 10.0;
            c.SubmitPress(past);
            c.SubmitPress(past);
            c.SubmitPress(past);
            Assert.AreEqual(new ScoreSnapshot(3, 3, 1), c.Current);
            Assert.AreEqual("Score: 3\nCombo: 3\nx1", ScoreView.Format(c.Current));

            yield return null; // frame 1: resumes after Update (Current = (3,0,1)), before LateUpdate
            yield return null; // frame 1 LateUpdate refreshed Text; frame 2 Update ran again

            Assert.AreEqual(new ScoreSnapshot(3, 0, 1), c.Current, "combo expired by Update, score kept");
            // Differs from both the Bind-time Initial text and the pre-frame (3,3,1) text, so it can only
            // come from a LateUpdate refresh that ran after a controller Update.
            Assert.AreEqual("Score: 3\nCombo: 0\nx1", view.Text);
        }

        [UnityTest]
        public IEnumerator RuntimeInstance_IsCreatedOnce_WithView()
        {
            yield return null;

            var go = GameObject.Find(ScoreCounterController.ObjectName);
            Assert.IsNotNull(go, "runtime score counter object missing");
            var controller = go.GetComponent<ScoreCounterController>();
            Assert.IsNotNull(controller);
            Assert.AreSame(controller, ScoreCounterController.Instance);
            Assert.AreSame(controller, ScoreCounterController.EnsureInstance(), "EnsureInstance must not duplicate");

            var view = go.GetComponent<ScoreView>();
            Assert.IsNotNull(view, "ScoreView missing on runtime object");
            Assert.AreSame(controller, view.Source);

            // Nothing presses on the runtime instance (no input in batchmode), so Current stays Initial in
            // every frame; after a second yield a full Update → LateUpdate has run, and Text is exact.
            yield return null;
            Assert.AreEqual(ScoreSnapshot.Initial, controller.Current);
            Assert.AreEqual(ScoreView.Format(ScoreSnapshot.Initial), view.Text);
        }
    }
}
