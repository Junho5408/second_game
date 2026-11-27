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
    // ORIGAMI-004: F-007 AC-01..06, 09 and F-006 AC-11 at text level (C-021 ScreenLines).
    public class F007ScreenTextTests
    {
        private const string HitPrefix = "맞힌 단계: ";
        private const string HintPrefix = "힌트: ";

        [Test]
        public void F007_AC01_AC02_InitialWorkbench()
        {
            CollectionAssert.AreEqual(new[]
            {
                "일차 1/7   돈 20원   시도 5/5",
                "오늘의 인기 작품: 종이비행기",
                "1. 종이비행기  3단계  종이 1원  오늘 ★3 9원",
                "2. 종이배  4단계  종이 1원  오늘 ★3 9원",
                "3. 개구리  잠김 - 종이배 3개 완성 시 (0/3)",
                "4. 학  잠김 - 개구리 3개 완성 시 (0/3)",
                "5. 용  잠김 - 학 3개 완성 시 (0/3)",
                "오늘 완성: 없음",
            }, Lines(new OrigamiFlow()));
        }

        [Test]
        public void Workbench_UnlockedListAndTodayPieces_Update()
        {
            var flow = new OrigamiFlow();
            Make(flow, ModelIds.Boat, 3);
            // plane with one hint → ★2
            StartFolding(flow, ModelIds.Plane);
            flow.Hint();
            foreach (var a in OrigamiCatalog.FindModel(ModelIds.Plane).Steps) flow.Fold(a);
            flow.Acknowledge();
            var lines = Lines(flow);
            Assert.AreEqual("일차 1/7   돈 16원   시도 1/5", lines[0]);
            // frog: 10 × 150 × 100 / 10000 = 15
            Assert.AreEqual("3. 개구리  6단계  종이 2원  오늘 ★3 15원", lines[4]);
            Assert.AreEqual("4. 학  잠김 - 개구리 3개 완성 시 (0/3)", lines[5]);
            Assert.AreEqual("오늘 완성: 종이배 ★★★, 종이배 ★★★, 종이배 ★★★, 종이비행기 ★★☆", lines[7]);
            Assert.AreEqual(8, lines.Count, "no message line when message is empty");
        }

        [Test]
        public void Workbench_PopularFollowsDay()
        {
            var flow = new OrigamiFlow();
            flow.EndDay();
            flow.Acknowledge();
            var lines = Lines(flow);
            Assert.AreEqual("일차 2/7   돈 20원   시도 5/5", lines[0]);
            Assert.AreEqual("오늘의 인기 작품: 종이배", lines[1]);
            // day 2: plane not popular 4×150×100/10000 = 6, boat popular 6×150×150/10000 = 13
            Assert.AreEqual("1. 종이비행기  3단계  종이 1원  오늘 ★3 6원", lines[2]);
            Assert.AreEqual("2. 종이배  4단계  종이 1원  오늘 ★3 13원", lines[3]);
        }

        [Test]
        public void Workbench_MessageIsLastLine()
        {
            var flow = new OrigamiFlow();
            flow.SelectModel("nope");
            var lines = Lines(flow);
            Assert.AreEqual(9, lines.Count);
            Assert.AreEqual("없는 작품입니다", lines[8]);
        }

        [Test]
        public void Preview_Lines()
        {
            var flow = At(FlowScreen.Preview);
            CollectionAssert.AreEqual(new[] { "종이비행기 도안", "↑ ← →", "종이값 1원" }, Lines(flow));
            flow = new OrigamiFlow();
            flow.SelectModel(ModelIds.Boat);
            CollectionAssert.AreEqual(new[] { "종이배 도안", "↓ ↑ F ↑", "종이값 1원" }, Lines(flow));
        }

        [Test]
        public void Folding_InitialLines()
        {
            CollectionAssert.AreEqual(new[]
            {
                "종이비행기 접는 중", "진행 0/3", "맞힌 단계: -", "실수 0/3", "힌트 0", "예상 등급 ★★★",
            }, Lines(At(FlowScreen.Folding)));
        }

        [Test]
        public void Folding_AfterHintAndMistake_Lines()
        {
            var flow = new OrigamiFlow();
            StartFolding(flow, ModelIds.Boat);
            flow.Fold(FoldAction.D);
            flow.Fold(FoldAction.L); // mistake
            flow.Hint();             // reveals U
            CollectionAssert.AreEqual(new[]
            {
                "종이배 접는 중", "진행 1/4", "맞힌 단계: ↓", "실수 1/3", "힌트 1", "예상 등급 ★☆☆",
                "힌트: ↑ 위로 접기",
            }, Lines(flow));
            flow.Fold(FoldAction.U);
            flow.Fold(FoldAction.F);
            CollectionAssert.AreEqual(new[]
            {
                "종이배 접는 중", "진행 3/4", "맞힌 단계: ↓ ↑ F", "실수 1/3", "힌트 1", "예상 등급 ★☆☆",
            }, Lines(flow));
        }

        [Test]
        public void AbandonConfirm_FoldingLinesPlusQuestion()
        {
            var flow = At(FlowScreen.Folding);
            flow.Fold(FoldAction.U);
            var folding = Lines(flow).ToList();
            flow.RequestAbandon();
            folding.Add("정말 포기할까요? (Enter: 포기한다 / Esc: 계속 접기)");
            CollectionAssert.AreEqual(folding, Lines(flow));
        }

        [Test]
        public void F007_AC03_ProgressAndMistakeLines_PerKey()
        {
            var flow = new OrigamiFlow();
            StartFolding(flow, ModelIds.Boat); // D U F U
            CollectionAssert.Contains(Lines(flow), "진행 0/4");
            flow.ProcessKeys(FlowKeys.FoldDown);
            CollectionAssert.Contains(Lines(flow), "진행 1/4");
            CollectionAssert.Contains(Lines(flow), "실수 0/3");
            flow.ProcessKeys(FlowKeys.FoldLeft);
            CollectionAssert.Contains(Lines(flow), "진행 1/4");
            CollectionAssert.Contains(Lines(flow), "실수 1/3");
            flow.ProcessKeys(FlowKeys.FoldUp);
            CollectionAssert.Contains(Lines(flow), "진행 2/4");
            flow.ProcessKeys(FlowKeys.FoldRight);
            CollectionAssert.Contains(Lines(flow), "실수 2/3");
            flow.ProcessKeys(FlowKeys.Flip);
            CollectionAssert.Contains(Lines(flow), "진행 3/4");
            CollectionAssert.Contains(Lines(flow), "실수 2/3");
            // same as F-003 on a bare OrigamiRun
            var run = new OrigamiRun();
            run.Start(ModelIds.Boat);
            run.Fold(FoldAction.D); run.Fold(FoldAction.L); run.Fold(FoldAction.U); run.Fold(FoldAction.R); run.Fold(FoldAction.F);
            Assert.AreEqual(run.GetSnapshot().Fold.Progress, flow.Snapshot.Fold.Progress);
            Assert.AreEqual(run.GetSnapshot().Fold.Mistakes, flow.Snapshot.Fold.Mistakes);
        }

        [Test]
        public void F007_AC04_FoldingTextNeverShowsRemainingSteps_EveryModel()
        {
            foreach (var model in OrigamiCatalog.Models)
            {
                var flow = FlowWithUnlocked(model.Id);
                StartFolding(flow, model.Id);
                AssertNoRemaining(flow, model);
                for (int i = 0; i < model.StepCount; i++)
                {
                    if (i == 1 || i == 3)
                    {
                        flow.Fold(WrongFor(model.Steps[i])); // two mistakes in total, never ruins
                        AssertNoRemaining(flow, model);
                    }
                    if (i % 2 == 0)
                    {
                        flow.Hint();
                        AssertNoRemaining(flow, model);
                    }
                    if (i == 2)
                    {
                        flow.RequestAbandon();
                        AssertNoRemaining(flow, model);
                        flow.CancelAbandon();
                        flow.Fold((FoldAction)0); // InvalidAction message line
                        AssertNoRemaining(flow, model);
                    }
                    flow.Fold(model.Steps[i]);
                    if (i < model.StepCount - 1) AssertNoRemaining(flow, model);
                }
                Assert.AreEqual(FlowScreen.Notice, flow.Screen, model.Id);
                Assert.AreEqual(CommandOutcome.FoldCompleted, flow.LastResult.Outcome, model.Id);
            }
        }

        [Test]
        public void Notice_Lines()
        {
            var flow = At(FlowScreen.Folding);
            flow.Fold(FoldAction.U); flow.Fold(FoldAction.L); flow.Fold(FoldAction.R);
            CollectionAssert.AreEqual(new[] { "종이비행기 완성! ★★★" }, Lines(flow));

            flow = new OrigamiFlow();
            StartFolding(flow, ModelIds.Boat);
            flow.Hint(); flow.Hint();
            flow.Fold(FoldAction.D); flow.Hint(); flow.Fold(FoldAction.U); flow.Fold(FoldAction.F); flow.Fold(FoldAction.U);
            CollectionAssert.AreEqual(new[] { "종이배 완성! ★★☆" }, Lines(flow), "2 hints → ★2");

            flow = At(FlowScreen.Folding);
            flow.Fold(FoldAction.D); flow.Fold(FoldAction.D); flow.Fold(FoldAction.D);
            CollectionAssert.AreEqual(new[] { "종이를 망쳤다" }, Lines(flow));

            CollectionAssert.AreEqual(new[] { "접기를 포기했다" }, Lines(At(FlowScreen.Notice)));
        }

        [Test]
        public void F007_AC06_SettlementMatchesF005()
        {
            var flow = new OrigamiFlow();
            Make(flow, ModelIds.Plane);
            StartFolding(flow, ModelIds.Boat);
            flow.Hint();
            foreach (var a in OrigamiCatalog.FindModel(ModelIds.Boat).Steps) flow.Fold(a);
            flow.Acknowledge();

            var run = new OrigamiRun();
            run.Start(ModelIds.Plane); foreach (var a in OrigamiCatalog.FindModel(ModelIds.Plane).Steps) run.Fold(a);
            run.Start(ModelIds.Boat); run.Hint(); foreach (var a in OrigamiCatalog.FindModel(ModelIds.Boat).Steps) run.Fold(a);
            var expected = run.EndDay().Settlement;

            Assert.IsTrue(flow.EndDay());
            Assert.AreEqual(FlowScreen.Settlement, flow.Screen);
            Assert.AreEqual(expected.Total, flow.Settlement.Total);
            Assert.AreEqual(expected.MoneyAfter, flow.Settlement.MoneyAfter);
            // plane ★3 popular 4×150×150/10000 = 9, boat ★2 6×100×100/10000 = 6
            CollectionAssert.AreEqual(new[]
            {
                "1일차 정산",
                "종이비행기 ★★★ (인기) 9원",
                "종이배 ★★☆ 6원",
                "합계 15원",
                "정산 후 돈 33원",
                "내일의 인기 작품: 종이배",
            }, Lines(flow));
            Assert.AreEqual(15, expected.Total);
            Assert.AreEqual(33, expected.MoneyAfter);
        }

        [Test]
        public void Settlement_EmptyDay_Lines()
        {
            CollectionAssert.AreEqual(new[]
            {
                "1일차 정산", "합계 0원", "정산 후 돈 20원", "내일의 인기 작품: 종이배",
            }, Lines(At(FlowScreen.Settlement)));
        }

        [Test]
        public void Settlement_Day7_NoTomorrowLine_ThenResult()
        {
            var flow = new OrigamiFlow();
            for (int d = 1; d <= 6; d++) { flow.EndDay(); flow.Acknowledge(); }
            Make(flow, ModelIds.Plane);
            flow.EndDay();
            // day 7 popular dragon → plane 4×150×100/10000 = 6
            CollectionAssert.AreEqual(new[]
            {
                "7일차 정산", "종이비행기 ★★★ 6원", "합계 6원", "정산 후 돈 25원",
            }, Lines(flow));
            StringAssert.DoesNotContain(FlowTexts.BankruptNotice, Text(flow));
            flow.Acknowledge();
            Assert.AreEqual(FlowScreen.Result, flow.Screen);
        }

        [Test]
        public void F007_AC09_BankruptSettlement_ThenResultE01()
        {
            var flow = new OrigamiFlow();
            for (int d = 1; d <= 4; d++)
            {
                Waste(flow, ModelIds.Plane, 5);
                Assert.IsTrue(flow.EndDay());
                if (d < 4)
                {
                    Assert.IsFalse(flow.Settlement.RunEnded);
                    flow.Acknowledge();
                }
            }
            Assert.AreEqual(FlowScreen.Settlement, flow.Screen);
            Assert.AreEqual(RunEndReason.Bankrupt, flow.Settlement.EndReason);
            var lines = Lines(flow);
            CollectionAssert.AreEqual(new[]
            {
                "4일차 정산", "합계 0원", "정산 후 돈 0원", FlowTexts.BankruptNotice,
            }, lines);
            Assert.IsFalse(lines.Any(l => l.StartsWith("내일의 인기 작품")), "no next popular model");

            Assert.IsTrue(flow.ProcessKeys(FlowKeys.Enter));
            Assert.AreEqual(FlowScreen.Result, flow.Screen);
            Assert.AreEqual(EndingIds.EmptyHanded, flow.Snapshot.EndingId);
            var result = Lines(flow);
            Assert.AreEqual("빈손", result[0]);
            Assert.AreEqual("종이는 다 떨어졌지만, 종이접기는 취미로 남았다.", result[1]);
            CollectionAssert.Contains(result, "최종 돈 0원");
            CollectionAssert.Contains(result, "실패 20회");
        }

        [Test]
        public void F006_AC11_ResultScreen_ThenRestart()
        {
            var flow = new OrigamiFlow();
            Make(flow, ModelIds.Plane);
            StartFolding(flow, ModelIds.Boat);
            flow.Hint();
            foreach (var a in OrigamiCatalog.FindModel(ModelIds.Boat).Steps) flow.Fold(a);
            flow.Acknowledge();
            Waste(flow, ModelIds.Plane);
            for (int d = 1; d <= 7; d++) { flow.EndDay(); flow.Acknowledge(); }
            Assert.AreEqual(FlowScreen.Result, flow.Screen);
            // money 20 − 3 + 15 = 32 → E-05
            CollectionAssert.AreEqual(new[]
            {
                "동네 종이접기 가게",
                "소박하지만 단골이 생긴 동네 가게. 다음 주도 문을 연다.",
                "최종 돈 32원",
                "종이비행기 ★3 1 / ★2 0 / ★1 0",
                "종이배 ★3 0 / ★2 1 / ★1 0",
                "개구리 ★3 0 / ★2 0 / ★1 0",
                "학 ★3 0 / ★2 0 / ★1 0",
                "용 ★3 0 / ★2 0 / ★1 0",
                "실패 1회",
                "총 판매액 15원",
                "총 종이값 3원",
            }, Lines(flow));

            Assert.IsTrue(flow.Restart());
            Assert.AreEqual(FlowScreen.Workbench, flow.Screen);
            var s = flow.Snapshot;
            Assert.AreEqual(1, s.Day);
            Assert.AreEqual(20, s.Money);
            Assert.AreEqual(5, s.AttemptsLeft);
            CollectionAssert.AreEqual(new[] { ModelIds.Plane, ModelIds.Boat }, s.UnlockedModelIds);
            Assert.AreEqual(CompletionCounts.Empty, s.Completions);
            Assert.AreEqual(0, s.FailureCount);
            Assert.AreEqual(0, s.TotalSales);
            Assert.AreEqual(0, s.TotalPaperCost);
            Assert.AreEqual(0, s.TodayPieces.Count);
            Assert.IsFalse(s.IsFolding);
            Assert.IsFalse(s.IsRunEnded);
            CollectionAssert.AreEqual(Lines(new OrigamiFlow()), Lines(flow));
            Assert.AreEqual("", flow.Message);
            Assert.IsNull(flow.Settlement);
            Assert.IsNull(flow.SelectedModelId);
        }

        [Test]
        public void Result_StarCountsPerGrade()
        {
            var flow = new OrigamiFlow();
            StartFolding(flow, ModelIds.Plane);
            flow.Fold(FoldAction.D); flow.Fold(FoldAction.D); // 2 mistakes → penalty 4 → ★1
            foreach (var a in OrigamiCatalog.FindModel(ModelIds.Plane).Steps) flow.Fold(a);
            flow.Acknowledge();
            for (int d = 1; d <= 7; d++) { flow.EndDay(); flow.Acknowledge(); }
            CollectionAssert.Contains(Lines(flow), "종이비행기 ★3 0 / ★2 0 / ★1 1");
        }

        // --- helpers ---

        private static OrigamiFlow FlowWithUnlocked(string modelId)
        {
            var flow = new OrigamiFlow();
            int order = OrigamiCatalog.FindModel(modelId).Order;
            if (order >= 3) Make(flow, ModelIds.Boat, 3);
            if (order >= 4)
            {
                Make(flow, ModelIds.Frog, 2);
                flow.EndDay();
                flow.Acknowledge();
                Make(flow, ModelIds.Frog);
            }
            if (order >= 5) Make(flow, ModelIds.Crane, 3);
            Assert.IsTrue(flow.Snapshot.IsUnlocked(modelId), modelId);
            Assert.Greater(flow.Snapshot.AttemptsLeft, 0);
            return flow;
        }

        private static FoldAction WrongFor(FoldAction correct) => correct == FoldAction.U ? FoldAction.D : FoldAction.U;

        private static readonly string[] Glyphs = { "↑", "↓", "←", "→", "F" };

        private static void AssertNoRemaining(OrigamiFlow flow, ModelDefinition model)
        {
            Assert.That(flow.Screen == FlowScreen.Folding || flow.Screen == FlowScreen.AbandonConfirm, flow.Screen.ToString());
            var fold = flow.Snapshot.Fold;
            int p = fold.Progress;
            var lines = Lines(flow);
            string ctx = model.Id + " at " + p + ": " + string.Join(" | ", lines);

            var hit = lines.Where(l => l.StartsWith(HitPrefix)).ToList();
            Assert.AreEqual(1, hit.Count, ctx);
            string expectedHit = p == 0 ? "-" : string.Join(" ", model.Steps.Take(p).Select(FlowTexts.ActionGlyph));
            Assert.AreEqual(HitPrefix + expectedHit, hit[0], ctx);

            var hint = lines.Where(l => l.StartsWith(HintPrefix)).ToList();
            if (fold.IsCurrentStepRevealed)
            {
                var cur = model.Steps[p];
                CollectionAssert.AreEqual(new[] { HintPrefix + FlowTexts.ActionGlyph(cur) + " " + FlowTexts.ActionName(cur) }, hint, ctx);
            }
            else
            {
                Assert.AreEqual(0, hint.Count, ctx);
            }

            foreach (var line in lines)
            {
                if (line.StartsWith(HitPrefix) || line.StartsWith(HintPrefix)) continue;
                foreach (var g in Glyphs) StringAssert.DoesNotContain(g, line, ctx);
                foreach (var a in FoldActions.All) StringAssert.DoesNotContain(FlowTexts.ActionName(a), line, ctx);
            }

            Assert.AreEqual(model.DisplayName + " 접는 중", lines[0], ctx);
            CollectionAssert.Contains(lines, "진행 " + p + "/" + model.StepCount, ctx);
            CollectionAssert.Contains(lines, "실수 " + fold.Mistakes + "/3", ctx);
            CollectionAssert.Contains(lines, "힌트 " + fold.Hints, ctx);
            CollectionAssert.Contains(lines, "예상 등급 " + FlowTexts.Stars(fold.ExpectedGrade), ctx);
        }
    }
}
