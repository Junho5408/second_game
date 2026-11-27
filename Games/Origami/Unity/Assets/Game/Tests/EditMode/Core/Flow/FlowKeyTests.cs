using System;
using System.Collections.Generic;
using NUnit.Framework;
using Origami.Core.Content;
using Origami.Core.Flow;
using Origami.Core.Run;
using static Origami.Tests.Flow.FlowScenarios;

namespace Origami.Tests.Flow
{
    // ORIGAMI-004: C-020 key table, priority, one key per frame; F-007 AC-05 (key == button), AC-08 (Esc confirm).
    public class FlowKeyTests
    {
        /// <summary>C-020 key table: (screen, key) → the button method it must equal. Missing = ignored.</summary>
        private static readonly Dictionary<(FlowScreen, FlowKeys), Func<OrigamiFlow, bool>> Table =
            new Dictionary<(FlowScreen, FlowKeys), Func<OrigamiFlow, bool>>
            {
                { (FlowScreen.Folding, FlowKeys.FoldUp), f => f.Fold(FoldAction.U) },
                { (FlowScreen.Folding, FlowKeys.FoldDown), f => f.Fold(FoldAction.D) },
                { (FlowScreen.Folding, FlowKeys.FoldLeft), f => f.Fold(FoldAction.L) },
                { (FlowScreen.Folding, FlowKeys.FoldRight), f => f.Fold(FoldAction.R) },
                { (FlowScreen.Folding, FlowKeys.Flip), f => f.Fold(FoldAction.F) },
                { (FlowScreen.Folding, FlowKeys.Hint), f => f.Hint() },
                { (FlowScreen.Folding, FlowKeys.Escape), f => f.RequestAbandon() },
                { (FlowScreen.AbandonConfirm, FlowKeys.Escape), f => f.CancelAbandon() },
                { (FlowScreen.AbandonConfirm, FlowKeys.Enter), f => f.ConfirmAbandon() },
                { (FlowScreen.Preview, FlowKeys.Escape), f => f.ClosePreview() },
                { (FlowScreen.Preview, FlowKeys.Enter), f => f.StartSelected() },
                { (FlowScreen.Notice, FlowKeys.Enter), f => f.Acknowledge() },
                { (FlowScreen.Settlement, FlowKeys.Enter), f => f.Acknowledge() },
            };

        [Test]
        public void EverySingleKeyOnEveryScreen_MatchesTable()
        {
            foreach (var screen in AllScreens)
            {
                foreach (var key in KeysByPriority)
                {
                    string what = screen + "+" + key;
                    var byKey = At(screen);
                    var before = Fingerprint(byKey);
                    bool handled = byKey.ProcessKeys(key);
                    if (Table.TryGetValue((screen, key), out var method))
                    {
                        var byButton = At(screen);
                        bool buttonHandled = method(byButton);
                        Assert.IsTrue(handled, what);
                        Assert.AreEqual(buttonHandled, handled, what);
                        Assert.AreEqual(Fingerprint(byButton), Fingerprint(byKey), what + ": key == button (AC-05)");
                    }
                    else
                    {
                        Assert.IsFalse(handled, what + " must be ignored");
                        Assert.AreEqual(before, Fingerprint(byKey), what + " must not change anything");
                    }
                }
            }
        }

        [Test]
        public void NoneAndUnknownBits_Ignored()
        {
            foreach (var screen in AllScreens)
            {
                foreach (var keys in new[] { FlowKeys.None, (FlowKeys)256, (FlowKeys)(1 << 20) })
                {
                    var flow = At(screen);
                    var before = Fingerprint(flow);
                    Assert.IsFalse(flow.ProcessKeys(keys), screen + "+" + (int)keys);
                    Assert.AreEqual(before, Fingerprint(flow));
                }
            }
        }

        [Test]
        public void EveryKeyCombinationOnEveryScreen_OnlyHighestPriorityKeyCounts()
        {
            foreach (var screen in AllScreens)
            {
                for (int bits = 1; bits < 256; bits++)
                {
                    var combo = (FlowKeys)bits;
                    FlowKeys top = FlowKeys.None;
                    foreach (var k in KeysByPriority)
                    {
                        if ((combo & k) != 0) { top = k; break; }
                    }
                    var a = At(screen);
                    var b = At(screen);
                    bool ha = a.ProcessKeys(combo | (FlowKeys)512);
                    bool hb = b.ProcessKeys(top);
                    string what = screen + "+" + combo;
                    Assert.AreEqual(hb, ha, what);
                    Assert.AreEqual(Fingerprint(b), Fingerprint(a), what);
                }
            }
        }

        [Test]
        public void Priority_ExplicitExamples()
        {
            // Folding: Esc beats arrows and H.
            var flow = At(FlowScreen.Folding);
            Assert.IsTrue(flow.ProcessKeys(FlowKeys.Escape | FlowKeys.FoldUp | FlowKeys.Hint));
            Assert.AreEqual(FlowScreen.AbandonConfirm, flow.Screen);
            Assert.AreEqual(0, flow.Snapshot.Fold.Progress);
            Assert.AreEqual(0, flow.Snapshot.Fold.Hints);

            // Folding: H beats arrows; Enter (ignored on Folding) still wins priority → nothing happens.
            flow = At(FlowScreen.Folding);
            Assert.IsTrue(flow.ProcessKeys(FlowKeys.Hint | FlowKeys.FoldUp));
            Assert.AreEqual(1, flow.Snapshot.Fold.Hints);
            Assert.AreEqual(0, flow.Snapshot.Fold.Progress);
            Assert.IsFalse(flow.ProcessKeys(FlowKeys.Enter | FlowKeys.FoldUp), "Enter wins and is ignored on Folding");
            Assert.AreEqual(0, flow.Snapshot.Fold.Progress);

            // Folding: Up beats Down beats Left beats Right beats Flip (plane = U L R).
            flow = At(FlowScreen.Folding);
            Assert.IsTrue(flow.ProcessKeys(FlowKeys.FoldUp | FlowKeys.FoldDown | FlowKeys.Flip));
            Assert.AreEqual(1, flow.Snapshot.Fold.Progress, "U was used");
            Assert.IsTrue(flow.ProcessKeys(FlowKeys.FoldLeft | FlowKeys.FoldRight | FlowKeys.Flip));
            Assert.AreEqual(2, flow.Snapshot.Fold.Progress, "L was used");
            Assert.IsTrue(flow.ProcessKeys(FlowKeys.FoldDown | FlowKeys.FoldRight));
            Assert.AreEqual(1, flow.Snapshot.Fold.Mistakes, "D was used, not R");
            Assert.IsTrue(flow.ProcessKeys(FlowKeys.FoldRight | FlowKeys.Flip));
            Assert.AreEqual(FlowScreen.Notice, flow.Screen, "R completed the plane");
        }

        [Test]
        public void OneKeyPerFrame_OnlyOneCommand()
        {
            var flow = At(FlowScreen.Folding);
            flow.ProcessKeys(FlowKeys.FoldUp | FlowKeys.FoldLeft | FlowKeys.FoldRight);
            Assert.AreEqual(FlowScreen.Folding, flow.Screen, "only U applied, plane not completed");
            Assert.AreEqual(1, flow.Snapshot.Fold.Progress);

            var preview = At(FlowScreen.Preview);
            Assert.IsTrue(preview.ProcessKeys(FlowKeys.Enter | FlowKeys.FoldUp));
            Assert.AreEqual(FlowScreen.Folding, preview.Screen);
            Assert.AreEqual(0, preview.Snapshot.Fold.Progress, "the same frame's arrow is not applied after Start");
        }

        [Test]
        public void F007_AC08_EscConfirm_Sequence()
        {
            var flow = new OrigamiFlow();
            StartFolding(flow, ModelIds.Plane);
            var s = flow.Snapshot;

            Assert.IsTrue(flow.ProcessKeys(FlowKeys.Escape));
            Assert.AreEqual(FlowScreen.AbandonConfirm, flow.Screen, "Esc → confirm");
            AssertUnchanged(s, flow.Snapshot);
            StringAssert.Contains("정말 포기할까요? (Enter: 포기한다 / Esc: 계속 접기)", Text(flow));

            Assert.IsFalse(flow.ProcessKeys(FlowKeys.FoldUp), "↑ ignored on confirm");
            Assert.IsFalse(flow.ProcessKeys(FlowKeys.Hint), "H ignored on confirm");
            Assert.IsFalse(flow.ProcessKeys(FlowKeys.Flip), "Space ignored on confirm");
            Assert.AreEqual(FlowScreen.AbandonConfirm, flow.Screen);
            AssertUnchanged(s, flow.Snapshot);

            Assert.IsTrue(flow.ProcessKeys(FlowKeys.Escape));
            Assert.AreEqual(FlowScreen.Folding, flow.Screen, "Esc again → cancel");
            Assert.IsTrue(flow.Snapshot.IsFolding);
            AssertUnchanged(s, flow.Snapshot);

            Assert.IsTrue(flow.ProcessKeys(FlowKeys.Escape));
            Assert.AreEqual(FlowScreen.AbandonConfirm, flow.Screen);
            Assert.IsTrue(flow.ProcessKeys(FlowKeys.Enter));
            Assert.AreEqual(FlowScreen.Notice, flow.Screen);
            Assert.AreEqual(CommandOutcome.FoldAbandoned, flow.LastResult.Outcome);
            // F-003 AC-11: not folding, failures 1, money 19, attempts 4.
            Assert.IsFalse(flow.Snapshot.IsFolding);
            Assert.AreEqual(1, flow.Snapshot.FailureCount);
            Assert.AreEqual(19, flow.Snapshot.Money);
            Assert.AreEqual(4, flow.Snapshot.AttemptsLeft);
            CollectionAssert.AreEqual(new[] { "접기를 포기했다" }, Lines(flow));
        }

        [Test]
        public void F007_AC08_ArrowOnConfirm_DoesNotChangeProgressOrMistakes()
        {
            var flow = At(FlowScreen.Folding);
            flow.ProcessKeys(FlowKeys.FoldUp);  // correct
            flow.ProcessKeys(FlowKeys.FoldDown); // mistake
            flow.ProcessKeys(FlowKeys.Escape);
            var s = flow.Snapshot;
            foreach (var k in new[] { FlowKeys.FoldUp, FlowKeys.FoldDown, FlowKeys.FoldLeft, FlowKeys.FoldRight, FlowKeys.Flip, FlowKeys.Hint })
            {
                Assert.IsFalse(flow.ProcessKeys(k), k.ToString());
            }
            Assert.AreEqual(1, flow.Snapshot.Fold.Progress);
            Assert.AreEqual(1, flow.Snapshot.Fold.Mistakes);
            Assert.AreEqual(0, flow.Snapshot.Fold.Hints);
            AssertUnchanged(s, flow.Snapshot);
        }

        [Test]
        public void EscPlusEnterInOneFrame_CannotSkipConfirm()
        {
            var flow = At(FlowScreen.Folding);
            // Frame 1: Esc+Enter → only Esc → confirm window.
            Assert.IsTrue(flow.ProcessKeys(FlowKeys.Escape | FlowKeys.Enter));
            Assert.AreEqual(FlowScreen.AbandonConfirm, flow.Screen);
            Assert.IsTrue(flow.Snapshot.IsFolding);
            Assert.AreEqual(0, flow.Snapshot.FailureCount);
            // Frame 2: Esc+Enter again → Esc wins → cancel, never confirm.
            Assert.IsTrue(flow.ProcessKeys(FlowKeys.Escape | FlowKeys.Enter));
            Assert.AreEqual(FlowScreen.Folding, flow.Screen);
            Assert.IsTrue(flow.Snapshot.IsFolding);
            Assert.AreEqual(0, flow.Snapshot.FailureCount);
            // Holding every key for many frames never abandons.
            for (int i = 0; i < 10; i++) flow.ProcessKeys((FlowKeys)255);
            Assert.AreEqual(0, flow.Snapshot.FailureCount);
            Assert.IsTrue(flow.Snapshot.IsFolding);
            Assert.AreEqual(0, flow.Snapshot.Fold.Progress);
        }

        [Test]
        public void ArrowsAndSpaceOutsideFolding_NoEffect()
        {
            foreach (var screen in AllScreens)
            {
                if (screen == FlowScreen.Folding) continue;
                foreach (var k in new[] { FlowKeys.FoldUp, FlowKeys.FoldDown, FlowKeys.FoldLeft, FlowKeys.FoldRight, FlowKeys.Flip, FlowKeys.Hint })
                {
                    var flow = At(screen);
                    var before = Fingerprint(flow);
                    Assert.IsFalse(flow.ProcessKeys(k), screen + "+" + k);
                    Assert.AreEqual(before, Fingerprint(flow));
                }
            }
        }

        [Test]
        public void KeyboardOnly_FullDay_PreviewEnterStarts_NoticeAndSettlementEnterConfirm()
        {
            var flow = new OrigamiFlow();
            flow.SelectModel(ModelIds.Plane);
            Assert.IsTrue(flow.ProcessKeys(FlowKeys.Enter));
            Assert.AreEqual(FlowScreen.Folding, flow.Screen);
            flow.ProcessKeys(FlowKeys.FoldUp);
            flow.ProcessKeys(FlowKeys.FoldLeft);
            flow.ProcessKeys(FlowKeys.FoldRight);
            Assert.AreEqual(FlowScreen.Notice, flow.Screen);
            Assert.IsTrue(flow.ProcessKeys(FlowKeys.Enter));
            Assert.AreEqual(FlowScreen.Workbench, flow.Screen);
            flow.EndDay();
            Assert.IsTrue(flow.ProcessKeys(FlowKeys.Enter));
            Assert.AreEqual(FlowScreen.Workbench, flow.Screen);
            Assert.AreEqual(2, flow.Snapshot.Day);
            flow.SelectModel(ModelIds.Boat);
            Assert.IsTrue(flow.ProcessKeys(FlowKeys.Escape));
            Assert.AreEqual(FlowScreen.Workbench, flow.Screen, "Esc closes preview");
        }

        private static void AssertUnchanged(RunSnapshot expected, RunSnapshot actual)
        {
            Assert.AreEqual(expected.FailureCount, actual.FailureCount, "failures");
            Assert.AreEqual(expected.AttemptsLeft, actual.AttemptsLeft, "attempts");
            Assert.AreEqual(expected.Money, actual.Money, "money");
            Assert.AreEqual(expected.IsFolding, actual.IsFolding, "folding");
            if (expected.Fold != null)
            {
                Assert.AreEqual(expected.Fold.Progress, actual.Fold.Progress, "progress");
                Assert.AreEqual(expected.Fold.Mistakes, actual.Fold.Mistakes, "mistakes");
                Assert.AreEqual(expected.Fold.Hints, actual.Fold.Hints, "hints");
            }
        }
    }
}
