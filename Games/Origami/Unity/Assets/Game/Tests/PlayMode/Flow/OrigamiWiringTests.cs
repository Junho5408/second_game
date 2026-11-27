using System.Collections.Generic;
using NUnit.Framework;
using Origami.Core.Content;
using Origami.Core.Flow;
using Origami.Core.Run;
using Origami.Runtime.Gameplay;
using Origami.Runtime.UI;
using UnityEngine;

namespace Origami.Tests.PlayMode.Flow
{
    /// <summary>
    /// ORIGAMI-005 / C-030·C-031: keys → ProcessFrame → OrigamiFlow, buttons → the same Flow methods,
    /// Flow → OrigamiView.Lines. All tests are synchronous [Test]s on freshly added (non-runtime) controllers:
    /// they call ProcessFrame / button Invoke / Refresh directly and never wait for frames, so they do not depend
    /// on frame timing. A controller's own Update only feeds ReadKeys(), which is None in batchmode (no real input).
    /// </summary>
    public class OrigamiWiringTests
    {
        private const string AbandonQuestion = "정말 포기할까요? (Enter: 포기한다 / Esc: 계속 접기)";

        private readonly List<GameObject> _objects = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _objects)
            {
                if (go != null) Object.Destroy(go);
            }
            _objects.Clear();
        }

        private OrigamiController NewController()
        {
            var go = new GameObject("TestOrigami" + _objects.Count);
            _objects.Add(go);
            return go.AddComponent<OrigamiController>();
        }

        private OrigamiView NewView(OrigamiController controller)
        {
            var view = controller.gameObject.AddComponent<OrigamiView>();
            view.Bind(controller);
            return view;
        }

        private static OrigamiButton FindButton(OrigamiFlow flow, string label)
        {
            foreach (var b in OrigamiButtons.For(flow))
            {
                if (b.Label == label) return b;
            }
            Assert.Fail("button '" + label + "' missing on screen " + flow.Screen);
            return null;
        }

        private static void Press(OrigamiFlow flow, string label)
        {
            Assert.IsTrue(FindButton(flow, label).Invoke(), "button '" + label + "' was ignored");
        }

        /// <summary>Everything the player can observe: screen + all screen lines (state is fully reflected there).</summary>
        private static string Observable(OrigamiFlow flow)
        {
            return flow.Screen + "\n" + string.Join("\n", FlowTexts.ScreenLines(flow));
        }

        private static string Label(FoldAction a) => OrigamiButtons.ActionLabel(a);

        private static void StartPlane(OrigamiController c)
        {
            Assert.IsTrue(c.Flow.SelectModel(ModelIds.Plane));
            Assert.IsTrue(c.Flow.StartSelected());
            Assert.AreEqual(FlowScreen.Folding, c.Flow.Screen);
        }

        // ---- F-007 AC-01 ----

        [Test]
        public void F007_AC01_NewController_ViewShowsDay1Money20Attempts5PopularPlane()
        {
            var c = NewController();
            var view = NewView(c);

            Assert.AreSame(c, view.Source);
            Assert.AreEqual(FlowScreen.Workbench, c.Flow.Screen);
            Assert.GreaterOrEqual(view.Lines.Count, 2);
            Assert.AreEqual("일차 1/7   돈 20원   시도 5/5", view.Lines[0]);
            Assert.AreEqual("오늘의 인기 작품: 종이비행기", view.Lines[1]);
        }

        [Test]
        public void View_AddedToControllerObject_BindsItselfInAwake()
        {
            var c = NewController();
            var view = c.gameObject.AddComponent<OrigamiView>(); // no Bind call

            Assert.AreSame(c, view.Source);
            Assert.AreEqual("일차 1/7   돈 20원   시도 5/5", view.Lines[0]);
        }

        [Test]
        public void View_Unbound_HasNoLines_AndRefreshKeepsThem()
        {
            var go = new GameObject("TestOrigamiViewOnly");
            _objects.Add(go);
            var view = go.AddComponent<OrigamiView>();

            Assert.IsNull(view.Source);
            Assert.AreEqual(0, view.Lines.Count);
            view.Refresh();
            Assert.AreEqual(0, view.Lines.Count);
        }

        [Test]
        public void View_Refresh_FollowsFlowScreen()
        {
            var c = NewController();
            var view = NewView(c);

            c.Flow.SelectModel(ModelIds.Plane);
            view.Refresh();
            Assert.AreEqual("종이비행기 도안", view.Lines[0]);
            Assert.AreEqual("↑ ← →", view.Lines[1]);

            c.Flow.StartSelected();
            view.Refresh();
            Assert.AreEqual("종이비행기 접는 중", view.Lines[0]);
            Assert.AreEqual("진행 0/3", view.Lines[1]);
        }

        // ---- F-007 AC-05: button = key ----

        [Test]
        public void F007_AC05_EachActionKeyAndButton_GiveSameResult()
        {
            var pairs = new[]
            {
                (FlowKeys.FoldUp, FoldAction.U),
                (FlowKeys.FoldDown, FoldAction.D),
                (FlowKeys.FoldLeft, FoldAction.L),
                (FlowKeys.FoldRight, FoldAction.R),
                (FlowKeys.Flip, FoldAction.F),
            };
            foreach (var (key, action) in pairs)
            {
                var byKey = NewController();
                var byButton = NewController();
                StartPlane(byKey);
                StartPlane(byButton);

                Assert.IsTrue(byKey.ProcessFrame(key), key + " ignored on Folding");
                Press(byButton.Flow, Label(action));

                Assert.AreEqual(Observable(byKey.Flow), Observable(byButton.Flow), "key " + key + " vs button " + Label(action));
                var fold = byKey.Flow.Snapshot.Fold;
                // plane = U, L, R: U is the correct first step (AC-03), every other action is a mistake.
                if (action == FoldAction.U)
                {
                    Assert.AreEqual(1, fold.Progress);
                    Assert.AreEqual(0, fold.Mistakes);
                }
                else
                {
                    Assert.AreEqual(0, fold.Progress);
                    Assert.AreEqual(1, fold.Mistakes);
                }
            }
        }

        [Test]
        public void F007_AC05_FullFoldByKeysAndByButtons_SameResultAtEveryStep()
        {
            var byKey = NewController();
            var byButton = NewController();
            StartPlane(byKey);
            StartPlane(byButton);

            var steps = new[]
            {
                (FlowKeys.FoldDown, Label(FoldAction.D)),   // mistake
                (FlowKeys.Hint, OrigamiButtons.HintLabel),   // reveals U
                (FlowKeys.FoldUp, Label(FoldAction.U)),
                (FlowKeys.FoldLeft, Label(FoldAction.L)),
                (FlowKeys.FoldRight, Label(FoldAction.R)),   // completes → Notice
                (FlowKeys.Enter, OrigamiButtons.AcknowledgeLabel), // Notice → Workbench
            };
            foreach (var (key, label) in steps)
            {
                Assert.IsTrue(byKey.ProcessFrame(key), key + " ignored");
                Press(byButton.Flow, label);
                Assert.AreEqual(Observable(byKey.Flow), Observable(byButton.Flow), "after " + key + " / " + label);
            }

            Assert.AreEqual(FlowScreen.Workbench, byKey.Flow.Screen);
            Assert.AreEqual(1, byKey.Flow.Snapshot.TodayPieces.Count);
            Assert.AreEqual(ModelIds.Plane, byKey.Flow.Snapshot.TodayPieces[0].ModelId);
        }

        [Test]
        public void F007_AC05_AbandonByKeysAndByButtons_SameResult()
        {
            var byKey = NewController();
            var byButton = NewController();
            StartPlane(byKey);
            StartPlane(byButton);

            var steps = new[]
            {
                (FlowKeys.Escape, OrigamiButtons.AbandonLabel),
                (FlowKeys.Escape, OrigamiButtons.CancelAbandonLabel),
                (FlowKeys.Escape, OrigamiButtons.AbandonLabel),
                (FlowKeys.Enter, OrigamiButtons.ConfirmAbandonLabel),
            };
            foreach (var (key, label) in steps)
            {
                Assert.IsTrue(byKey.ProcessFrame(key), key + " ignored");
                Press(byButton.Flow, label);
                Assert.AreEqual(Observable(byKey.Flow), Observable(byButton.Flow), "after " + key + " / " + label);
            }
            Assert.AreEqual(FlowScreen.Notice, byButton.Flow.Screen);
        }

        [Test]
        public void F007_AC05_PreviewEnterAndEscape_MatchStartAndCloseButtons()
        {
            var byKey = NewController();
            var byButton = NewController();
            byKey.Flow.SelectModel(ModelIds.Boat);
            byButton.Flow.SelectModel(ModelIds.Boat);

            Assert.IsTrue(byKey.ProcessFrame(FlowKeys.Escape));
            Press(byButton.Flow, OrigamiButtons.CloseLabel);
            Assert.AreEqual(Observable(byKey.Flow), Observable(byButton.Flow));
            Assert.AreEqual(FlowScreen.Workbench, byKey.Flow.Screen);

            Press(byKey.Flow, "종이배");
            Press(byButton.Flow, "종이배");
            Assert.IsTrue(byKey.ProcessFrame(FlowKeys.Enter));
            Press(byButton.Flow, OrigamiButtons.StartLabel(1));
            Assert.AreEqual(Observable(byKey.Flow), Observable(byButton.Flow));
            Assert.AreEqual(FlowScreen.Folding, byKey.Flow.Screen);
        }

        [Test]
        public void Buttons_PerScreen_MatchContractLabels()
        {
            var c = NewController();
            var flow = c.Flow;

            CollectionAssert.AreEqual(
                new[] { "종이비행기", "종이배", "개구리", "학", "용", "하루 마치기" },
                Labels(flow));

            flow.SelectModel(ModelIds.Plane);
            CollectionAssert.AreEqual(new[] { "접기 시작 (1원)", "닫기" }, Labels(flow));

            flow.StartSelected();
            CollectionAssert.AreEqual(
                new[] { "↑ 위로 접기", "↓ 아래로 접기", "← 왼쪽으로 접기", "→ 오른쪽으로 접기", "F 뒤집기", "힌트", "포기" },
                Labels(flow));

            flow.RequestAbandon();
            CollectionAssert.AreEqual(new[] { "포기한다", "계속 접기" }, Labels(flow));

            flow.ConfirmAbandon();
            CollectionAssert.AreEqual(new[] { "확인" }, Labels(flow));
        }

        [Test]
        public void StaleButton_AfterScreenChange_IsIgnored()
        {
            var c = NewController();
            StartPlane(c);
            var abandon = FindButton(c.Flow, OrigamiButtons.AbandonLabel);
            var foldUp = FindButton(c.Flow, Label(FoldAction.U));

            Assert.IsTrue(abandon.Invoke());
            string before = Observable(c.Flow);
            Assert.IsFalse(foldUp.Invoke(), "fold button from the folding screen must be ignored on the confirm screen");
            Assert.AreEqual(before, Observable(c.Flow));
        }

        [Test]
        public void LockedModelButton_ShowsUnlockCondition_NoPreview()
        {
            var c = NewController();
            var view = NewView(c);

            Press(c.Flow, "개구리");
            view.Refresh();

            Assert.AreEqual(FlowScreen.Workbench, c.Flow.Screen);
            Assert.AreEqual("종이배 3개 완성 시 (0/3)", c.Flow.Message);
            Assert.AreEqual("종이배 3개 완성 시 (0/3)", view.Lines[view.Lines.Count - 1]);
        }

        private static string[] Labels(OrigamiFlow flow)
        {
            var buttons = OrigamiButtons.For(flow);
            var labels = new string[buttons.Count];
            for (int i = 0; i < labels.Length; i++) labels[i] = buttons[i].Label;
            return labels;
        }

        // ---- F-007 AC-08: Esc confirmation ----

        [Test]
        public void F007_AC08_EscConfirm_CancelConfirm_ThenEnterAbandons()
        {
            var c = NewController();
            var view = NewView(c);
            StartPlane(c);
            var s0 = c.Flow.Snapshot;
            Assert.AreEqual(0, s0.Fold.Progress);
            Assert.AreEqual(0, s0.FailureCount);
            Assert.AreEqual(4, s0.AttemptsLeft);

            // Esc → confirmation, state unchanged.
            Assert.IsTrue(c.ProcessFrame(FlowKeys.Escape));
            Assert.AreEqual(FlowScreen.AbandonConfirm, c.Flow.Screen);
            AssertUnchanged(s0, c.Flow);
            view.Refresh();
            Assert.AreEqual(AbandonQuestion, view.Lines[view.Lines.Count - 1]);

            // ↑ while the confirmation is shown: ignored, progress/mistakes unchanged.
            string confirmScreen = Observable(c.Flow);
            Assert.IsFalse(c.ProcessFrame(FlowKeys.FoldUp));
            Assert.IsFalse(c.ProcessFrame(FlowKeys.Flip));
            Assert.IsFalse(c.ProcessFrame(FlowKeys.Hint));
            Assert.AreEqual(FlowScreen.AbandonConfirm, c.Flow.Screen);
            Assert.AreEqual(confirmScreen, Observable(c.Flow));
            AssertUnchanged(s0, c.Flow);

            // Esc again → back to folding, still folding, unchanged.
            Assert.IsTrue(c.ProcessFrame(FlowKeys.Escape));
            Assert.AreEqual(FlowScreen.Folding, c.Flow.Screen);
            Assert.IsTrue(c.Flow.Snapshot.IsFolding);
            AssertUnchanged(s0, c.Flow);
            view.Refresh();
            CollectionAssert.DoesNotContain(view.Lines, AbandonQuestion);

            // Esc, Enter → Abandon (F-003 AC-11: not folding, failures 1, money 19, attempts 4).
            Assert.IsTrue(c.ProcessFrame(FlowKeys.Escape));
            Assert.IsTrue(c.ProcessFrame(FlowKeys.Enter));
            var s1 = c.Flow.Snapshot;
            Assert.AreEqual(FlowScreen.Notice, c.Flow.Screen);
            Assert.IsFalse(s1.IsFolding);
            Assert.AreEqual(1, s1.FailureCount);
            Assert.AreEqual(19, s1.Money);
            Assert.AreEqual(4, s1.AttemptsLeft);
            view.Refresh();
            Assert.AreEqual("접기를 포기했다", view.Lines[0]);
        }

        [Test]
        public void F007_AC08_EscAndEnterSameFrame_OnlyOpensConfirmation()
        {
            var c = NewController();
            StartPlane(c);
            var s0 = c.Flow.Snapshot;

            Assert.IsTrue(c.ProcessFrame(FlowKeys.Escape | FlowKeys.Enter));
            Assert.AreEqual(FlowScreen.AbandonConfirm, c.Flow.Screen);
            AssertUnchanged(s0, c.Flow);
        }

        [Test]
        public void MultiKeyFrame_TopKeyIgnoredOnScreen_DoesNothing()
        {
            // A-4 (intended): Enter outranks FoldUp; Enter means nothing on Folding, so the frame does nothing.
            var c = NewController();
            StartPlane(c);
            string before = Observable(c.Flow);

            Assert.IsFalse(c.ProcessFrame(FlowKeys.Enter | FlowKeys.FoldUp));
            Assert.AreEqual(before, Observable(c.Flow));
            Assert.AreEqual(0, c.Flow.Snapshot.Fold.Progress);
        }

        [Test]
        public void FoldKeysOutsideFolding_HaveNoEffect()
        {
            var c = NewController();
            string before = Observable(c.Flow);
            foreach (var key in new[] { FlowKeys.FoldUp, FlowKeys.FoldDown, FlowKeys.FoldLeft, FlowKeys.FoldRight, FlowKeys.Flip, FlowKeys.Hint, FlowKeys.Escape, FlowKeys.Enter, FlowKeys.None })
            {
                Assert.IsFalse(c.ProcessFrame(key), key + " must be ignored on Workbench");
                Assert.AreEqual(before, Observable(c.Flow));
            }
        }

        [Test]
        public void ReadKeys_WithoutRealInput_IsNone()
        {
            // Batchmode test runs have no keyboard input; this mainly checks legacy Input polling does not throw.
            Assert.AreEqual(FlowKeys.None, OrigamiController.ReadKeys());
        }

        private static void AssertUnchanged(RunSnapshot before, OrigamiFlow flow)
        {
            var now = flow.Snapshot;
            Assert.IsTrue(now.IsFolding, "fold must still be in progress");
            Assert.AreEqual(before.Fold.Progress, now.Fold.Progress, "progress");
            Assert.AreEqual(before.Fold.Mistakes, now.Fold.Mistakes, "mistakes");
            Assert.AreEqual(before.Fold.Hints, now.Fold.Hints, "hints");
            Assert.AreEqual(before.FailureCount, now.FailureCount, "failures");
            Assert.AreEqual(before.AttemptsLeft, now.AttemptsLeft, "attempts");
            Assert.AreEqual(before.Money, now.Money, "money");
        }
    }
}
