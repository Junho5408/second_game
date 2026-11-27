using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Origami.Core.Content;
using Origami.Core.Flow;
using Origami.Core.Rules;
using Origami.Core.Run;
using static Origami.Tests.Flow.FlowScenarios;

namespace Origami.Tests.Flow
{
    // ORIGAMI-004: C-020 screen-by-method table, return values, Message rules.
    public class FlowTransitionTests
    {
        private static readonly (string Name, Func<OrigamiFlow, bool> Call)[] Methods =
        {
            ("SelectModel", f => f.SelectModel(ModelIds.Plane)),
            ("ClosePreview", f => f.ClosePreview()),
            ("StartSelected", f => f.StartSelected()),
            ("Fold", f => f.Fold(FoldAction.U)),
            ("Hint", f => f.Hint()),
            ("RequestAbandon", f => f.RequestAbandon()),
            ("ConfirmAbandon", f => f.ConfirmAbandon()),
            ("CancelAbandon", f => f.CancelAbandon()),
            ("EndDay", f => f.EndDay()),
            ("Acknowledge", f => f.Acknowledge()),
            ("Restart", f => f.Restart()),
        };

        /// <summary>C-020 table: (screen, method) → next screen when valid.</summary>
        private static readonly Dictionary<(FlowScreen, string), FlowScreen> Valid = new Dictionary<(FlowScreen, string), FlowScreen>
        {
            { (FlowScreen.Workbench, "SelectModel"), FlowScreen.Preview },
            { (FlowScreen.Workbench, "EndDay"), FlowScreen.Settlement },
            { (FlowScreen.Preview, "StartSelected"), FlowScreen.Folding },
            { (FlowScreen.Preview, "ClosePreview"), FlowScreen.Workbench },
            { (FlowScreen.Folding, "Fold"), FlowScreen.Folding },
            { (FlowScreen.Folding, "Hint"), FlowScreen.Folding },
            { (FlowScreen.Folding, "RequestAbandon"), FlowScreen.AbandonConfirm },
            { (FlowScreen.AbandonConfirm, "ConfirmAbandon"), FlowScreen.Notice },
            { (FlowScreen.AbandonConfirm, "CancelAbandon"), FlowScreen.Folding },
            { (FlowScreen.Notice, "Acknowledge"), FlowScreen.Workbench },
            { (FlowScreen.Settlement, "Acknowledge"), FlowScreen.Workbench },
            { (FlowScreen.Result, "Restart"), FlowScreen.Workbench },
        };

        [Test]
        public void EveryMethodOnEveryScreen_MatchesTable()
        {
            foreach (var screen in AllScreens)
            {
                foreach (var m in Methods)
                {
                    var flow = At(screen);
                    var before = Fingerprint(flow);
                    bool handled = m.Call(flow);
                    string what = screen + "." + m.Name;
                    if (Valid.TryGetValue((screen, m.Name), out var next))
                    {
                        Assert.IsTrue(handled, what);
                        Assert.AreEqual(next, flow.Screen, what);
                    }
                    else
                    {
                        Assert.IsFalse(handled, what);
                        Assert.AreEqual(before, Fingerprint(flow), what + " must not change anything");
                    }
                }
            }
        }

        [Test]
        public void NewFlow_Workbench_Defaults()
        {
            var flow = new OrigamiFlow();
            Assert.AreEqual(FlowScreen.Workbench, flow.Screen);
            Assert.AreEqual("", flow.Message);
            Assert.IsNull(flow.SelectedModelId);
            Assert.IsNull(flow.Settlement);
            Assert.AreEqual(1, flow.Snapshot.Day);
            Assert.AreEqual(20, flow.Snapshot.Money);
            Assert.AreEqual(5, flow.Snapshot.AttemptsLeft);
        }

        [Test]
        public void SelectedModelId_PerScreen()
        {
            Assert.IsNull(At(FlowScreen.Workbench).SelectedModelId);
            Assert.AreEqual(ModelIds.Plane, At(FlowScreen.Preview).SelectedModelId);
            Assert.AreEqual(ModelIds.Plane, At(FlowScreen.Folding).SelectedModelId);
            Assert.AreEqual(ModelIds.Plane, At(FlowScreen.AbandonConfirm).SelectedModelId);
            Assert.AreEqual(ModelIds.Plane, At(FlowScreen.Notice).SelectedModelId);
            Assert.IsNull(At(FlowScreen.Settlement).SelectedModelId);
            Assert.IsNull(At(FlowScreen.Result).SelectedModelId);

            var flow = At(FlowScreen.Preview);
            flow.ClosePreview();
            Assert.IsNull(flow.SelectedModelId, "ClosePreview clears selection");
            flow = At(FlowScreen.Notice);
            flow.Acknowledge();
            Assert.IsNull(flow.SelectedModelId, "Acknowledge(Notice) clears selection");
        }

        [Test]
        public void Settlement_OnlyOnSettlementScreen()
        {
            foreach (var screen in AllScreens)
            {
                var flow = At(screen);
                if (screen == FlowScreen.Settlement) Assert.IsNotNull(flow.Settlement);
                else Assert.IsNull(flow.Settlement, screen.ToString());
            }
            var f = At(FlowScreen.Settlement);
            f.Acknowledge();
            Assert.IsNull(f.Settlement);
        }

        [Test]
        public void SelectModel_UnknownOrNull_MessageOnly_NoCoreCall()
        {
            foreach (var id in new[] { null, "", "Plane", "swan" })
            {
                var flow = new OrigamiFlow();
                var last = flow.LastResult.ToString();
                Assert.IsTrue(flow.SelectModel(id), "handled on Workbench: " + id);
                Assert.AreEqual(FlowScreen.Workbench, flow.Screen);
                Assert.AreEqual("없는 작품입니다", flow.Message);
                Assert.IsNull(flow.SelectedModelId);
                Assert.AreEqual(last, flow.LastResult.ToString(), "no Core command");
            }
        }

        [Test]
        public void SelectModel_Locked_ShowsOnlyUnlockCondition_NoPreview()
        {
            var expected = new Dictionary<string, string>
            {
                { ModelIds.Frog, "종이배 3개 완성 시 (0/3)" },
                { ModelIds.Crane, "개구리 3개 완성 시 (0/3)" },
                { ModelIds.Dragon, "학 3개 완성 시 (0/3)" },
            };
            foreach (var kv in expected)
            {
                var flow = new OrigamiFlow();
                var before = flow.Snapshot;
                var last = flow.LastResult.ToString();
                Assert.IsTrue(flow.SelectModel(kv.Key));
                Assert.AreEqual(FlowScreen.Workbench, flow.Screen, "no preview of a locked model");
                Assert.IsNull(flow.SelectedModelId);
                Assert.AreEqual(kv.Value, flow.Message);
                Assert.AreEqual(last, flow.LastResult.ToString(), "no Core command");
                Assert.AreEqual(before.Money, flow.Snapshot.Money);
                Assert.AreEqual(before.AttemptsLeft, flow.Snapshot.AttemptsLeft);

                // The model's sequence is never shown: the only text is the workbench plus the condition line.
                var lines = Lines(flow);
                Assert.AreEqual(kv.Value, lines[lines.Count - 1], "message is the last line");
                var model = OrigamiCatalog.FindModel(kv.Key);
                string seq = string.Join(" ", model.Steps.Select(FlowTexts.ActionGlyph));
                StringAssert.DoesNotContain(seq, Text(flow));
                StringAssert.DoesNotContain(model.DisplayName + " 도안", Text(flow));
            }
        }

        [Test]
        public void SelectModel_LockedProgress_ReflectsCompletions()
        {
            var flow = new OrigamiFlow();
            Make(flow, ModelIds.Boat, 2);
            flow.SelectModel(ModelIds.Frog);
            Assert.AreEqual("종이배 3개 완성 시 (2/3)", flow.Message);
            Make(flow, ModelIds.Boat);
            Assert.IsTrue(flow.SelectModel(ModelIds.Frog));
            Assert.AreEqual(FlowScreen.Preview, flow.Screen, "frog unlocked after 3 boats");
            Assert.AreEqual("", flow.Message);
        }

        [Test]
        public void SelectModel_NotBlockedByAttemptsOrMoney_StartRejectedOnPreview()
        {
            var flow = new OrigamiFlow();
            Waste(flow, ModelIds.Plane, 5);
            Assert.IsTrue(flow.SelectModel(ModelIds.Plane));
            Assert.AreEqual(FlowScreen.Preview, flow.Screen);
            var money = flow.Snapshot.Money;
            Assert.IsTrue(flow.StartSelected(), "rejection is still handled");
            Assert.AreEqual(FlowScreen.Preview, flow.Screen);
            Assert.AreEqual("오늘은 더 접을 수 없습니다", flow.Message);
            Assert.AreEqual(RejectReason.NoAttemptsLeft, flow.LastResult.Reason);
            Assert.AreEqual(money, flow.Snapshot.Money);
            Assert.AreEqual(0, flow.Snapshot.AttemptsLeft);
            Assert.IsTrue(flow.ClosePreview());
            Assert.AreEqual("", flow.Message, "screen change clears message");
        }

        [Test]
        public void StartSelected_NotEnoughMoney_F002AC06()
        {
            var flow = new OrigamiFlow();
            for (int d = 0; d < 3; d++)
            {
                Waste(flow, ModelIds.Plane, 5);
                flow.EndDay();
                flow.Acknowledge();
            }
            Make(flow, ModelIds.Boat, 3);
            Waste(flow, ModelIds.Plane);
            Assert.AreEqual(1, flow.Snapshot.Money);
            Assert.IsTrue(flow.SelectModel(ModelIds.Frog));
            Assert.IsTrue(flow.StartSelected());
            Assert.AreEqual(FlowScreen.Preview, flow.Screen);
            Assert.AreEqual("돈이 부족합니다", flow.Message);
            CollectionAssert.Contains(Lines(flow), "돈이 부족합니다");
            Assert.AreEqual(1, flow.Snapshot.Money);
            Assert.AreEqual(1, flow.Snapshot.AttemptsLeft);
            flow.ClosePreview();
            StartFolding(flow, ModelIds.Plane);
            Assert.AreEqual(0, flow.Snapshot.Money);
            Assert.AreEqual(0, flow.Snapshot.AttemptsLeft);
        }

        [Test]
        public void Fold_InvalidAction_Message_ThenClearedByAcceptedCommand()
        {
            var flow = At(FlowScreen.Folding);
            Assert.IsTrue(flow.Fold((FoldAction)0));
            Assert.AreEqual(FlowScreen.Folding, flow.Screen);
            Assert.AreEqual("알 수 없는 동작입니다", flow.Message);
            Assert.AreEqual(0, flow.Snapshot.Fold.Mistakes);
            CollectionAssert.Contains(Lines(flow), "알 수 없는 동작입니다");
            Assert.IsTrue(flow.Fold(FoldAction.U));
            Assert.AreEqual("", flow.Message);
            Assert.AreEqual(1, flow.Snapshot.Fold.Progress);
        }

        [Test]
        public void Message_ClearedByScreenChange_FromFolding()
        {
            var flow = At(FlowScreen.Folding);
            flow.Fold((FoldAction)99);
            Assert.AreNotEqual("", flow.Message);
            Assert.IsTrue(flow.RequestAbandon());
            Assert.AreEqual("", flow.Message);
        }

        [Test]
        public void Message_ClearedBySelectingUnlockedModel()
        {
            var flow = new OrigamiFlow();
            flow.SelectModel(ModelIds.Dragon);
            Assert.AreNotEqual("", flow.Message);
            flow.SelectModel(ModelIds.Boat);
            Assert.AreEqual("", flow.Message);
            Assert.AreEqual(ModelIds.Boat, flow.SelectedModelId);
        }

        [Test]
        public void Message_ClearedByEndDay()
        {
            var flow = new OrigamiFlow();
            flow.SelectModel("nope");
            Assert.IsTrue(flow.EndDay());
            Assert.AreEqual("", flow.Message);
        }

        [Test]
        public void Fold_Completion_GoesToNotice_WithPiece()
        {
            var flow = new OrigamiFlow();
            StartFolding(flow, ModelIds.Plane);
            Assert.IsTrue(flow.Fold(FoldAction.U));
            Assert.IsTrue(flow.Fold(FoldAction.L));
            Assert.AreEqual(FlowScreen.Folding, flow.Screen);
            Assert.IsTrue(flow.Fold(FoldAction.R));
            Assert.AreEqual(FlowScreen.Notice, flow.Screen);
            Assert.AreEqual(CommandOutcome.FoldCompleted, flow.LastResult.Outcome);
            Assert.AreEqual(new CompletedPiece(ModelIds.Plane, Grade.Star3), flow.LastResult.Piece);
            Assert.AreEqual(ModelIds.Plane, flow.SelectedModelId);
            Assert.IsTrue(flow.Acknowledge());
            Assert.AreEqual(FlowScreen.Workbench, flow.Screen);
            Assert.AreEqual(1, flow.Snapshot.TodayPieces.Count);
        }

        [Test]
        public void Fold_Ruin_GoesToNotice()
        {
            var flow = At(FlowScreen.Folding);
            flow.Fold(FoldAction.D);
            flow.Fold(FoldAction.D);
            Assert.AreEqual(FlowScreen.Folding, flow.Screen);
            flow.Fold(FoldAction.D);
            Assert.AreEqual(FlowScreen.Notice, flow.Screen);
            Assert.AreEqual(CommandOutcome.FoldRuined, flow.LastResult.Outcome);
            Assert.AreEqual(1, flow.Snapshot.FailureCount);
        }

        [Test]
        public void Hint_Twice_CountsOnce_StaysFolding()
        {
            var flow = At(FlowScreen.Folding);
            Assert.IsTrue(flow.Hint());
            Assert.IsTrue(flow.Hint());
            Assert.AreEqual(FlowScreen.Folding, flow.Screen);
            Assert.AreEqual(1, flow.Snapshot.Fold.Hints);
            Assert.AreEqual(FoldAction.U, flow.Snapshot.Fold.RevealedAction);
            Assert.AreEqual(CommandOutcome.HintRevealed, flow.LastResult.Outcome);
        }

        [Test]
        public void RequestAndCancelAbandon_StateUnchanged()
        {
            var flow = At(FlowScreen.Folding);
            flow.Fold(FoldAction.U);
            flow.Fold(FoldAction.D);
            var s = flow.Snapshot;
            var last = flow.LastResult.ToString();
            Assert.IsTrue(flow.RequestAbandon());
            Assert.AreEqual(FlowScreen.AbandonConfirm, flow.Screen);
            Assert.IsTrue(flow.CancelAbandon());
            Assert.AreEqual(FlowScreen.Folding, flow.Screen);
            Assert.AreEqual(last, flow.LastResult.ToString(), "no Core call");
            Assert.AreEqual(s.Fold.Progress, flow.Snapshot.Fold.Progress);
            Assert.AreEqual(s.Fold.Mistakes, flow.Snapshot.Fold.Mistakes);
            Assert.AreEqual(s.FailureCount, flow.Snapshot.FailureCount);
            Assert.AreEqual(s.AttemptsLeft, flow.Snapshot.AttemptsLeft);
        }

        [Test]
        public void FoldingBlocksWorkbenchCommands()
        {
            var flow = At(FlowScreen.Folding);
            var before = Fingerprint(flow);
            Assert.IsFalse(flow.SelectModel(ModelIds.Boat));
            Assert.IsFalse(flow.EndDay());
            Assert.AreEqual(before, Fingerprint(flow));
        }

        [Test]
        public void Settlement_Acknowledge_ContinuesToWorkbench_NextDay()
        {
            var flow = At(FlowScreen.Settlement);
            Assert.AreEqual(1, flow.Settlement.SettledDay);
            Assert.IsFalse(flow.Settlement.RunEnded);
            Assert.AreEqual(CommandOutcome.DayEnded, flow.LastResult.Outcome);
            Assert.IsTrue(flow.Acknowledge());
            Assert.AreEqual(FlowScreen.Workbench, flow.Screen);
            Assert.AreEqual(2, flow.Snapshot.Day);
        }

        [Test]
        public void Day7_Settlement_GoesToResult_ThenRestart()
        {
            var flow = new OrigamiFlow();
            for (int d = 1; d <= 6; d++)
            {
                flow.EndDay();
                flow.Acknowledge();
                Assert.AreEqual(FlowScreen.Workbench, flow.Screen);
            }
            Assert.IsTrue(flow.EndDay());
            Assert.AreEqual(FlowScreen.Settlement, flow.Screen);
            Assert.IsTrue(flow.Settlement.RunEnded);
            Assert.AreEqual(RunEndReason.Completed, flow.Settlement.EndReason);
            Assert.IsTrue(flow.Acknowledge());
            Assert.AreEqual(FlowScreen.Result, flow.Screen);
            Assert.IsTrue(flow.Snapshot.IsRunEnded);
            Assert.IsTrue(flow.Restart());
            Assert.AreEqual(FlowScreen.Workbench, flow.Screen);
            Assert.AreEqual(CommandOutcome.RunStarted, flow.LastResult.Outcome);
            Assert.IsFalse(flow.Snapshot.IsRunEnded);
            Assert.AreEqual(1, flow.Snapshot.Day);
        }

        [Test]
        public void Snapshot_RefreshedAfterEveryCoreCall()
        {
            var flow = new OrigamiFlow();
            var s0 = flow.Snapshot;
            flow.SelectModel(ModelIds.Plane);
            flow.StartSelected();
            Assert.AreNotSame(s0, flow.Snapshot);
            Assert.AreEqual(19, flow.Snapshot.Money);
            var s1 = flow.Snapshot;
            flow.Fold((FoldAction)0); // rejected
            Assert.AreNotSame(s1, flow.Snapshot, "refreshed after a rejected call too");
        }
    }
}
