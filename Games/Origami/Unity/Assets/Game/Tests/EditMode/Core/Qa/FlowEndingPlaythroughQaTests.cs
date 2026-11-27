using System.Collections.Generic;
using NUnit.Framework;
using Origami.Core.Content;
using Origami.Core.Flow;
using Origami.Core.Rules;
using Origami.Core.Run;

namespace Origami.Tests.Qa
{
    // QA ORIGAMI-004: whole runs played through OrigamiFlow, using KEYS for every command that has a key in C-020
    // (Preview Enter/Esc, fold keys, H, Esc/Enter confirm, Notice/Settlement Enter). Only the three commands that
    // have no key in C-020 — SelectModel and EndDay on Workbench, Restart on Result — are button calls
    // (see KeyboardAlone_CannotLeaveWorkbenchOrResult). Every ending E-01..E-05 is reached; the Result text must
    // match EndingRules and the catalog, and "다시 시작" must give the F-001 AC-01 workbench.
    public class FlowEndingPlaythroughQaTests
    {
        private static FlowKeys KeyFor(FoldAction a)
        {
            switch (a)
            {
                case FoldAction.U: return FlowKeys.FoldUp;
                case FoldAction.D: return FlowKeys.FoldDown;
                case FoldAction.L: return FlowKeys.FoldLeft;
                case FoldAction.R: return FlowKeys.FoldRight;
                default: return FlowKeys.Flip;
            }
        }

        private static void Key(OrigamiFlow flow, FlowKeys k, FlowScreen expectedAfter)
        {
            Assert.IsTrue(flow.ProcessKeys(k), k + " on " + flow.Screen + " handled (" + flow.Message + ")");
            Assert.AreEqual(expectedAfter, flow.Screen, "after " + k);
        }

        private static void Make(OrigamiFlow flow, string id, int times = 1, bool hintFirst = false)
        {
            for (int t = 0; t < times; t++)
            {
                Assert.IsTrue(flow.SelectModel(id), "select " + id);
                Assert.AreEqual(FlowScreen.Preview, flow.Screen, "preview " + id + " (" + flow.Message + ")");
                Key(flow, FlowKeys.Enter, FlowScreen.Folding);
                if (hintFirst) Key(flow, FlowKeys.Hint, FlowScreen.Folding);
                var steps = OrigamiCatalog.FindModel(id).Steps;
                for (int i = 0; i < steps.Count; i++)
                    Key(flow, KeyFor(steps[i]), i < steps.Count - 1 ? FlowScreen.Folding : FlowScreen.Notice);
                Assert.AreEqual(CommandOutcome.FoldCompleted, flow.LastResult.Outcome);
                StringAssert.StartsWith(OrigamiCatalog.FindModel(id).DisplayName + " 완성! ", FlowTexts.ScreenLines(flow)[0]);
                Key(flow, FlowKeys.Enter, FlowScreen.Workbench);
            }
        }

        private static void Waste(OrigamiFlow flow, string id, int times)
        {
            for (int t = 0; t < times; t++)
            {
                Assert.IsTrue(flow.SelectModel(id));
                Key(flow, FlowKeys.Enter, FlowScreen.Folding);
                Key(flow, FlowKeys.Escape, FlowScreen.AbandonConfirm);
                Key(flow, FlowKeys.Enter, FlowScreen.Notice);
                Assert.AreEqual("접기를 포기했다", FlowTexts.ScreenLines(flow)[0]);
                Key(flow, FlowKeys.Enter, FlowScreen.Workbench);
            }
        }

        private static SettlementResult EndDay(OrigamiFlow flow)
        {
            Assert.IsTrue(flow.EndDay(), "EndDay");
            Assert.AreEqual(FlowScreen.Settlement, flow.Screen);
            var s = flow.Settlement;
            var lines = FlowTexts.ScreenLines(flow);
            CollectionAssert.Contains(lines, "합계 " + s.Total + "원");
            CollectionAssert.Contains(lines, "정산 후 돈 " + s.MoneyAfter + "원");
            Key(flow, FlowKeys.Enter, s.RunEnded ? FlowScreen.Result : FlowScreen.Workbench);
            return s;
        }

        private static void EndDaysUntilResult(OrigamiFlow flow)
        {
            for (int guard = 0; guard < 7 && flow.Screen != FlowScreen.Result; guard++) EndDay(flow);
            Assert.AreEqual(FlowScreen.Result, flow.Screen);
        }

        private static void AssertResultAndRestart(OrigamiFlow flow, string expectedEnding)
        {
            var s = flow.Snapshot;
            Assert.AreEqual(expectedEnding, s.EndingId, "ending id");
            Assert.AreEqual(expectedEnding, EndingRules.Evaluate(s.EndReason, s.Money, s.Completions), "EndingRules agrees");
            var e = OrigamiCatalog.FindEnding(expectedEnding);
            var lines = FlowTexts.ScreenLines(flow);
            Assert.AreEqual(e.DisplayName, lines[0], "ending name line");
            Assert.AreEqual(e.Text, lines[1], "ending text line");
            Assert.AreEqual("최종 돈 " + s.Money + "원", lines[2]);
            for (int i = 0; i < OrigamiCatalog.Models.Count; i++)
            {
                var m = OrigamiCatalog.Models[i];
                Assert.AreEqual(m.DisplayName + " ★3 " + s.Completions.Get(m.Id, Grade.Star3) + " / ★2 " + s.Completions.Get(m.Id, Grade.Star2)
                    + " / ★1 " + s.Completions.Get(m.Id, Grade.Star1), lines[3 + i], "counts " + m.Id);
            }
            Assert.AreEqual("실패 " + s.FailureCount + "회", lines[8]);
            Assert.AreEqual("총 판매액 " + s.TotalSales + "원", lines[9]);
            Assert.AreEqual("총 종이값 " + s.TotalPaperCost + "원", lines[10]);
            Assert.AreEqual(11, lines.Count);
            foreach (var other in OrigamiCatalog.Endings)
                if (other.Id != expectedEnding) CollectionAssert.DoesNotContain(lines, other.DisplayName);

            // No key leaves the Result screen; "다시 시작" (button) → F-001 AC-01 workbench.
            for (int k = 0; k < 256; k++) Assert.IsFalse(flow.ProcessKeys((FlowKeys)k), "key " + k + " on Result");
            Assert.IsTrue(flow.Restart());
            Assert.AreEqual(FlowScreen.Workbench, flow.Screen);
            CollectionAssert.AreEqual(FlowTexts.ScreenLines(new OrigamiFlow()), FlowTexts.ScreenLines(flow), "restart == fresh flow");
            Assert.AreEqual("일차 1/7   돈 20원   시도 5/5", FlowTexts.ScreenLines(flow)[0]);
        }

        [Test]
        public void E01_Bankrupt_ByKeys_SettlementShowsNotice_ThenResult()
        {
            var flow = new OrigamiFlow();
            for (int day = 1; day <= 4; day++)
            {
                Waste(flow, ModelIds.Plane, 5);
                Assert.IsTrue(flow.EndDay());
                var lines = FlowTexts.ScreenLines(flow);
                if (day < 4)
                {
                    StringAssert.StartsWith("내일의 인기 작품: ", lines[lines.Count - 1]);
                    Key(flow, FlowKeys.Enter, FlowScreen.Workbench);
                }
                else
                {
                    CollectionAssert.AreEqual(new[] { "4일차 정산", "합계 0원", "정산 후 돈 0원", FlowTexts.BankruptNotice }, lines);
                    Key(flow, FlowKeys.Enter, FlowScreen.Result);
                }
            }
            Assert.AreEqual(RunEndReason.Bankrupt, flow.Snapshot.EndReason);
            AssertResultAndRestart(flow, EndingIds.EmptyHanded);
        }

        [Test]
        public void E01_CompletedEmptyHanded_ByKeys()
        {
            var flow = new OrigamiFlow();
            EndDaysUntilResult(flow);
            Assert.AreEqual(RunEndReason.Completed, flow.Snapshot.EndReason);
            Assert.AreEqual(20, flow.Snapshot.Money, "money 20 <= EmptyHandedMoney");
            AssertResultAndRestart(flow, EndingIds.EmptyHanded);
        }

        [Test]
        public void E02_CraneWish_ByKeys()
        {
            var flow = new OrigamiFlow();
            Make(flow, ModelIds.Boat, 3);
            Make(flow, ModelIds.Frog, 2);
            EndDay(flow);
            Make(flow, ModelIds.Frog);
            Make(flow, ModelIds.Crane, 4);
            EndDay(flow);
            for (int day = 3; day <= 7; day++)
            {
                Make(flow, ModelIds.Crane, 5);
                EndDay(flow);
            }
            Assert.AreEqual(29, flow.Snapshot.Completions.Total(ModelIds.Crane));
            AssertResultAndRestart(flow, EndingIds.CraneWish);
        }

        [Test]
        public void E03_Tycoon_ByKeys()
        {
            var flow = new OrigamiFlow();
            Make(flow, ModelIds.Boat, 3);
            Make(flow, ModelIds.Frog, 2);
            EndDay(flow);
            Make(flow, ModelIds.Frog);
            Make(flow, ModelIds.Crane, 3);
            Make(flow, ModelIds.Dragon);
            EndDay(flow);
            for (int day = 3; day <= 7; day++)
            {
                Make(flow, ModelIds.Dragon, 5);
                EndDay(flow);
            }
            Assert.AreEqual(1709, flow.Snapshot.Money, "F-006 AC-10 money");
            AssertResultAndRestart(flow, EndingIds.Tycoon);
        }

        [Test]
        public void E04_Master_ByKeys()
        {
            var flow = new OrigamiFlow();
            Make(flow, ModelIds.Plane);
            Make(flow, ModelIds.Boat, 3);
            Make(flow, ModelIds.Frog);
            EndDay(flow);
            Make(flow, ModelIds.Frog, 2);
            Make(flow, ModelIds.Crane, 3);
            EndDay(flow);
            Make(flow, ModelIds.Dragon);
            EndDaysUntilResult(flow);
            var s = flow.Snapshot;
            Assert.Less(s.Money, RuleConstants.TycoonMoney);
            Assert.Greater(s.Money, RuleConstants.EmptyHandedMoney);
            AssertResultAndRestart(flow, EndingIds.Master);
        }

        [Test]
        public void E05_LocalShop_ByKeys_WithHintedPiece()
        {
            var flow = new OrigamiFlow();
            Make(flow, ModelIds.Plane, 1, hintFirst: true);
            EndDaysUntilResult(flow);
            var lines = FlowTexts.ScreenLines(flow);
            Assert.AreEqual("종이비행기 ★3 0 / ★2 1 / ★1 0", lines[3]);
            Assert.AreEqual(25, flow.Snapshot.Money, "20 - 1 + 6 (★2 plane, popular day 1)");
            AssertResultAndRestart(flow, EndingIds.LocalShop);
        }

        [Test]
        public void KeyboardAlone_CannotLeaveWorkbenchOrResult()
        {
            // Evidence for the QA observation in Reports/qa/ORIGAMI-004.md: C-020 gives Workbench and Result no key,
            // so model selection, "하루 마치기" and "다시 시작" need the on-screen buttons (as specified).
            var flow = new OrigamiFlow();
            for (int k = 0; k < 256; k++)
            {
                Assert.IsFalse(flow.ProcessKeys((FlowKeys)k), "key " + k + " on Workbench");
                Assert.AreEqual(FlowScreen.Workbench, flow.Screen);
            }
        }
    }
}
