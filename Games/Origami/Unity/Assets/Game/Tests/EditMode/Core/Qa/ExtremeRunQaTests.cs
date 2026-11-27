using System;
using System.Collections.Generic;
using NUnit.Framework;
using Origami.Core.Content;
using Origami.Core.Rules;
using Origami.Core.Run;

namespace Origami.Tests.Qa
{
    // QA ORIGAMI-003: extreme / boundary whole-run scenarios beyond the F-001..F-006 acceptance criteria.
    // Expected values are hand-computed from GAME_RULES / CONTENT_SPECS (derivations in the comments).
    public class ExtremeRunQaTests
    {
        private static readonly FoldAction U = FoldAction.U, D = FoldAction.D, L = FoldAction.L, R = FoldAction.R, F = FoldAction.F;

        private static void Ok(CommandResult r, CommandOutcome expected, string what = "")
        {
            Assert.IsTrue(r.IsAccepted, what + " rejected: " + r);
            Assert.AreEqual(expected, r.Outcome, what);
        }

        private static void Make(OrigamiRun run, string id, int times = 1)
        {
            for (int t = 0; t < times; t++)
            {
                Ok(run.Start(id), CommandOutcome.FoldStarted, "Start(" + id + ")");
                var steps = OrigamiCatalog.FindModel(id).Steps;
                for (int i = 0; i < steps.Count; i++) Assert.IsTrue(run.Fold(steps[i]).IsAccepted);
            }
        }

        private static void Waste(OrigamiRun run, string id, int times = 1)
        {
            for (int t = 0; t < times; t++)
            {
                Ok(run.Start(id), CommandOutcome.FoldStarted);
                Ok(run.Abandon(), CommandOutcome.FoldAbandoned);
            }
        }

        private static SettlementResult EndDay(OrigamiRun run)
        {
            var r = run.EndDay();
            Assert.IsTrue(r.IsAccepted, "EndDay: " + r);
            return r.Settlement;
        }

        private static void Rejected(CommandResult r, RejectReason reason, string what = "")
        {
            Assert.IsFalse(r.IsAccepted, what + " accepted: " + r);
            Assert.AreEqual(reason, r.Reason, what);
        }

        [Test]
        public void PlanesOnly_SevenFullDays_LocalShop()
        {
            // Day 1 (popular plane): 5 × (9 − 1) = +40 → 60. Days 2..7: 5 × (6 − 1) = +25/day → 210.
            var run = new OrigamiRun();
            var money = new List<int>();
            for (int day = 1; day <= 7; day++)
            {
                Make(run, ModelIds.Plane, 5);
                Rejected(run.Start(ModelIds.Plane), RejectReason.NoAttemptsLeft);
                money.Add(EndDay(run).MoneyAfter);
            }
            CollectionAssert.AreEqual(new[] { 60, 85, 110, 135, 160, 185, 210 }, money);
            var s = run.GetSnapshot();
            Assert.AreEqual(RunEndReason.Completed, s.EndReason);
            Assert.AreEqual(EndingIds.LocalShop, s.EndingId, "only plane ★3 → E-04 not met");
            Assert.AreEqual(35, s.Completions.Get(ModelIds.Plane, Grade.Star3));
            CollectionAssert.AreEqual(new[] { ModelIds.Plane, ModelIds.Boat }, s.UnlockedModelIds, "planes unlock nothing");
            Assert.AreEqual(225, s.TotalSales);
            Assert.AreEqual(35, s.TotalPaperCost);
        }

        [Test]
        public void NeverFold_ButProbeEveryCommandEachDay_EmptyHanded()
        {
            var run = new OrigamiRun();
            for (int day = 1; day <= 7; day++)
            {
                Rejected(run.Fold(U), RejectReason.NoFold);
                Rejected(run.Hint(), RejectReason.NoFold);
                Rejected(run.Abandon(), RejectReason.NoFold);
                Rejected(run.Start(ModelIds.Frog), RejectReason.Locked);
                var st = EndDay(run);
                Assert.AreEqual(0, st.Total);
                Assert.AreEqual(20, st.MoneyAfter);
            }
            var s = run.GetSnapshot();
            Assert.AreEqual(7, s.Day);
            Assert.AreEqual(EndingIds.EmptyHanded, s.EndingId);
            Assert.AreEqual(RunEndReason.Completed, s.EndReason);
        }

        [Test]
        public void MaxCranes_29Cranes_CraneWish()
        {
            // d1: boat×3 + frog×2 → 70. d2 (boat pop): frog + crane×4 → 70 − 14 + 15 + 108 = 179 (crane unlocked same day).
            // d3: crane×5 → +120 = 299. d4 → 419. d5 (crane pop 40): +185 = 604. d6 → 724. d7 → 844.
            var run = new OrigamiRun();
            Make(run, ModelIds.Boat, 3);
            Make(run, ModelIds.Frog, 2);
            Assert.AreEqual(70, EndDay(run).MoneyAfter);
            Make(run, ModelIds.Frog);
            Make(run, ModelIds.Crane, 4);
            Assert.AreEqual(179, EndDay(run).MoneyAfter);
            var expected = new[] { 299, 419, 604, 724, 844 };
            for (int day = 3; day <= 7; day++)
            {
                Make(run, ModelIds.Crane, 5);
                Assert.AreEqual(expected[day - 3], EndDay(run).MoneyAfter, "day " + day);
            }
            var s = run.GetSnapshot();
            Assert.AreEqual(29, s.Completions.Total(ModelIds.Crane));
            Assert.AreEqual(EndingIds.CraneWish, s.EndingId);
        }

        [Test]
        public void CraneWish_BeatsTycoon_InARealRun()
        {
            // d1 70, d2 209 (F-006 AC-10). d3 dragon×5 +270 → 479. d4 dragon×3 + crane×2: +162 + 48 → 689.
            // d5 (crane pop): crane×5 +185 → 874. d6: crane×5 +120 → 994. d7 (dragon pop): dragon×5 +420 → 1414.
            // Cranes 3 + 2 + 5 + 5 = 15 → E-02 although money ≥ 1000 (E-03).
            var run = new OrigamiRun();
            Make(run, ModelIds.Boat, 3); Make(run, ModelIds.Frog, 2); Assert.AreEqual(70, EndDay(run).MoneyAfter);
            Make(run, ModelIds.Frog); Make(run, ModelIds.Crane, 3); Make(run, ModelIds.Dragon); Assert.AreEqual(209, EndDay(run).MoneyAfter);
            Make(run, ModelIds.Dragon, 5); Assert.AreEqual(479, EndDay(run).MoneyAfter);
            Make(run, ModelIds.Dragon, 3); Make(run, ModelIds.Crane, 2); Assert.AreEqual(689, EndDay(run).MoneyAfter);
            Make(run, ModelIds.Crane, 5); Assert.AreEqual(874, EndDay(run).MoneyAfter);
            Make(run, ModelIds.Crane, 5); Assert.AreEqual(994, EndDay(run).MoneyAfter);
            Make(run, ModelIds.Dragon, 5);
            var last = run.EndDay();
            Ok(last, CommandOutcome.RunFinished);
            Assert.AreEqual(1414, last.Settlement.MoneyAfter);
            var s = run.GetSnapshot();
            Assert.AreEqual(15, s.Completions.Total(ModelIds.Crane));
            Assert.AreEqual(EndingIds.CraneWish, s.EndingId);
        }

        [Test]
        public void Master_AllFiveStar3_InARealRun()
        {
            // d1 (plane pop): plane, boat×3, frog → 20 − 6 + 9 + 27 + 15 = 65. d2 (boat pop): frog×2, crane×3
            // → 65 − 13 + 30 + 81 = 163. d3: dragon → 163 − 6 + 60 = 217. d4..7 idle → 217 → E-04.
            var run = new OrigamiRun();
            Make(run, ModelIds.Plane); Make(run, ModelIds.Boat, 3); Make(run, ModelIds.Frog);
            Assert.AreEqual(65, EndDay(run).MoneyAfter);
            Make(run, ModelIds.Frog, 2); Make(run, ModelIds.Crane, 3);
            Assert.AreEqual(163, EndDay(run).MoneyAfter);
            Make(run, ModelIds.Dragon);
            Assert.AreEqual(217, EndDay(run).MoneyAfter);
            for (int i = 0; i < 4; i++) EndDay(run);
            Assert.AreEqual(EndingIds.Master, run.GetSnapshot().EndingId);
        }

        [Test]
        public void Unlock_ThirdBoatOnFifthAttempt_FrogIsUnlockedButNoAttemptsLeft()
        {
            var run = new OrigamiRun();
            Waste(run, ModelIds.Plane, 2);
            Make(run, ModelIds.Boat, 3);
            Assert.IsTrue(run.GetSnapshot().IsUnlocked(ModelIds.Frog));
            // Locked is checked before NoAttemptsLeft, so NoAttemptsLeft proves the unlock applied immediately.
            Rejected(run.Start(ModelIds.Frog), RejectReason.NoAttemptsLeft);
            EndDay(run);
            Ok(run.Start(ModelIds.Frog), CommandOutcome.FoldStarted, "next day");
        }

        [Test]
        public void Unlock_TwoBoatsAndNoAttempts_ReportsLockedNotNoAttempts()
        {
            var run = new OrigamiRun();
            Waste(run, ModelIds.Plane, 3);
            Make(run, ModelIds.Boat, 2);
            Assert.AreEqual(0, run.GetSnapshot().AttemptsLeft);
            Rejected(run.Start(ModelIds.Frog), RejectReason.Locked);
        }

        [Test]
        public void Unlock_ThirdBoatWithTwoMistakesOnLastStep_Star1_UnlocksImmediately()
        {
            var run = new OrigamiRun();
            Make(run, ModelIds.Boat, 2);
            Ok(run.Start(ModelIds.Boat), CommandOutcome.FoldStarted);
            Ok(run.Fold(D), CommandOutcome.StepCorrect);
            Ok(run.Fold(U), CommandOutcome.StepCorrect);
            Ok(run.Fold(F), CommandOutcome.StepCorrect);
            Ok(run.Fold(D), CommandOutcome.StepMistake);
            Ok(run.Fold(L), CommandOutcome.StepMistake);
            Assert.IsFalse(run.GetSnapshot().IsUnlocked(ModelIds.Frog), "not yet");
            var r = run.Fold(U);
            Ok(r, CommandOutcome.FoldCompleted);
            Assert.AreEqual(Grade.Star1, r.Piece.Grade);
            Assert.IsTrue(run.GetSnapshot().IsUnlocked(ModelIds.Frog));
            Ok(run.Start(ModelIds.Frog), CommandOutcome.FoldStarted, "same day");
        }

        [Test]
        public void Unlock_ThirdBoatRuinedOnLastStep_FrogStaysLocked()
        {
            var run = new OrigamiRun();
            Make(run, ModelIds.Boat, 2);
            run.Start(ModelIds.Boat);
            run.Fold(D); run.Fold(U); run.Fold(F);
            run.Fold(L); run.Fold(L);
            Ok(run.Fold(D), CommandOutcome.FoldRuined, "3rd mistake on the last step");
            var s = run.GetSnapshot();
            Assert.AreEqual(2, s.Completions.Total(ModelIds.Boat));
            Assert.AreEqual(1, s.FailureCount);
            Assert.IsFalse(s.IsUnlocked(ModelIds.Frog));
            Rejected(run.Start(ModelIds.Frog), RejectReason.Locked);
        }

        [Test]
        public void RuinOnLastAttempt_DayCanEnd_EarlierPiecesStillSold()
        {
            var run = new OrigamiRun();
            Make(run, ModelIds.Boat, 4);
            run.Start(ModelIds.Plane);
            run.Fold(D); run.Fold(D);
            Ok(run.Fold(D), CommandOutcome.FoldRuined);
            var s = run.GetSnapshot();
            Assert.AreEqual(0, s.AttemptsLeft);
            Assert.IsFalse(s.IsFolding);
            Assert.AreEqual(15, s.Money, "20 − 5");
            Rejected(run.Start(ModelIds.Plane), RejectReason.NoAttemptsLeft);
            Rejected(run.Fold(U), RejectReason.NoFold);
            var st = EndDay(run);
            Assert.AreEqual(4, st.Lines.Count, "ruined fold is not sold");
            Assert.AreEqual(36, st.Total, "boat ★3 day 1 = 9 × 4");
            Assert.AreEqual(51, st.MoneyAfter);
            Assert.AreEqual(5, run.GetSnapshot().AttemptsLeft);
        }

        [Test]
        public void EndDayWithUnfinishedFold_DeniedEvenOnDay7AndWithZeroAttempts()
        {
            var run = new OrigamiRun();
            for (int i = 0; i < 6; i++) EndDay(run);
            Assert.AreEqual(7, run.GetSnapshot().Day);
            Waste(run, ModelIds.Plane, 4);
            run.Start(ModelIds.Boat);
            run.Fold(D); run.Fold(L); run.Hint();
            var before = run.GetSnapshot();
            Rejected(run.EndDay(), RejectReason.FoldInProgress, "day 7, 0 attempts, mid-fold");
            var after = run.GetSnapshot();
            Assert.AreEqual(before.Day, after.Day);
            Assert.AreEqual(before.Money, after.Money);
            Assert.AreEqual(0, after.AttemptsLeft);
            Assert.IsTrue(after.IsFolding);
            Assert.AreEqual(1, after.Fold.Progress);
            Assert.AreEqual(1, after.Fold.Mistakes);
            Assert.AreEqual(1, after.Fold.Hints);
            Assert.AreEqual(U, after.Fold.RevealedAction);
            Assert.IsFalse(after.IsRunEnded);
            // Finishing the fold then allows the final settlement (boat ★1: penalty 3 → day-7 price 3).
            run.Fold(U); run.Fold(F);
            Ok(run.Fold(U), CommandOutcome.FoldCompleted);
            var last = run.EndDay();
            Ok(last, CommandOutcome.RunFinished);
            Assert.AreEqual(3, last.Settlement.Total);
        }

        [Test]
        public void MoneyZeroAtEndOfDay7_CompletedNotBankrupt()
        {
            var run = new OrigamiRun();
            for (int d = 1; d <= 3; d++) { Waste(run, ModelIds.Plane, 5); EndDay(run); } // money 5
            for (int d = 4; d <= 6; d++) EndDay(run);
            Waste(run, ModelIds.Plane, 5);
            Assert.AreEqual(0, run.GetSnapshot().Money);
            var r = run.EndDay();
            Ok(r, CommandOutcome.RunFinished);
            Assert.AreEqual(RunEndReason.Completed, r.Settlement.EndReason, "R-013: no bankruptcy check after day 7");
            Assert.IsNull(r.Settlement.NextPopularModelId);
            var s = run.GetSnapshot();
            Assert.AreEqual(7, s.Day);
            Assert.AreEqual(0, s.Money);
            Assert.AreEqual(EndingIds.EmptyHanded, s.EndingId);
        }

        [Test]
        public void BankruptAtStartOfDay7_DayStays7_ThenOnlyNewRun()
        {
            var run = new OrigamiRun();
            for (int d = 1; d <= 3; d++) { Waste(run, ModelIds.Plane, 5); EndDay(run); } // money 5
            EndDay(run); EndDay(run);                                                       // day 6
            Waste(run, ModelIds.Plane, 5);                                                  // money 0
            var r = run.EndDay();
            Ok(r, CommandOutcome.RunFinished);
            Assert.AreEqual(6, r.Settlement.SettledDay);
            Assert.AreEqual(RunEndReason.Bankrupt, r.Settlement.EndReason);
            Assert.IsNull(r.Settlement.NextPopularModelId);
            var s = run.GetSnapshot();
            Assert.AreEqual(7, s.Day, "the new day's value");
            Assert.AreEqual(5, s.AttemptsLeft);
            Assert.AreEqual(EndingIds.EmptyHanded, s.EndingId);
            Rejected(run.Start(ModelIds.Plane), RejectReason.RunEnded);
            Rejected(run.Fold((FoldAction)99), RejectReason.RunEnded);
            Rejected(run.Hint(), RejectReason.RunEnded);
            Rejected(run.Abandon(), RejectReason.RunEnded);
            Rejected(run.EndDay(), RejectReason.RunEnded);
            Ok(run.NewRun(), CommandOutcome.RunStarted);
            Assert.AreEqual(1, run.GetSnapshot().Day);
        }

        [Test]
        public void MoneyZeroMidDay_WithUnsoldPiece_Survives_AndNotEnoughMoneyMeanwhile()
        {
            var run = new OrigamiRun();
            for (int d = 1; d <= 3; d++) { Waste(run, ModelIds.Plane, 5); EndDay(run); } // day 4, money 5
            Waste(run, ModelIds.Plane, 3);                                                  // money 2
            Make(run, ModelIds.Boat);                                                       // money 1
            Make(run, ModelIds.Plane);                                                      // money 0, attempts 0
            Rejected(run.Start(ModelIds.Plane), RejectReason.NoAttemptsLeft, "attempts before money");
            var st = EndDay(run);
            Assert.AreEqual(13 + 6, st.Total, "day 4: boat popular 13, plane 6");
            Assert.AreEqual(RunEndReason.None, st.EndReason);
            Assert.AreEqual(19, run.GetSnapshot().Money);
        }

        [Test]
        public void HintSpamAndInvalidActionSpam_NeverRuinOrDoubleCount()
        {
            var run = new OrigamiRun();
            run.Start(ModelIds.Plane);
            for (int i = 0; i < 1000; i++) Ok(run.Hint(), CommandOutcome.HintRevealed);
            foreach (var bad in new[] { 0, 6, -1, 255, int.MaxValue, int.MinValue })
                for (int i = 0; i < 10; i++) Rejected(run.Fold((FoldAction)bad), RejectReason.InvalidAction, "action " + bad);
            var s = run.GetSnapshot();
            Assert.AreEqual(1, s.Fold.Hints);
            Assert.AreEqual(0, s.Fold.Mistakes);
            Assert.AreEqual(0, s.Fold.Progress);
            Assert.AreEqual(U, s.Fold.RevealedAction);
        }

        [Test]
        public void HintOnEveryDragonStep_Penalty10_Star1_NotRuined()
        {
            var run = new OrigamiRun();
            Make(run, ModelIds.Boat, 3); Make(run, ModelIds.Frog, 2); EndDay(run);
            Make(run, ModelIds.Frog); Make(run, ModelIds.Crane, 3);
            run.Start(ModelIds.Dragon);
            var steps = OrigamiCatalog.FindModel(ModelIds.Dragon).Steps;
            CommandResult r = default;
            for (int i = 0; i < steps.Count; i++)
            {
                Ok(run.Hint(), CommandOutcome.HintRevealed);
                Assert.AreEqual(steps[i], run.GetSnapshot().Fold.RevealedAction, "hint shows the current step " + i);
                r = run.Fold(steps[i]);
            }
            Ok(r, CommandOutcome.FoldCompleted);
            Assert.AreEqual(Grade.Star1, r.Piece.Grade);
            // Day 2 dragon ★1, not popular: floor(40 × 60 × 100 / 10000) = 24.
            var st = EndDay(run);
            Assert.AreEqual(24, st.Lines[st.Lines.Count - 1].Price);
        }

        [Test]
        public void TwoMistakesThenHint_Penalty5_Star1()
        {
            var run = new OrigamiRun();
            run.Start(ModelIds.Plane);
            run.Fold(D); run.Fold(D);
            run.Hint();
            var f = run.GetSnapshot().Fold;
            Assert.AreEqual(5, f.Penalty);
            Assert.AreEqual(Grade.Star1, f.ExpectedGrade);
            Assert.AreEqual(U, f.RevealedAction);
            run.Fold(U); run.Fold(L);
            var r = run.Fold(R);
            Ok(r, CommandOutcome.FoldCompleted);
            Assert.AreEqual(Grade.Star1, r.Piece.Grade);
        }

        [Test]
        public void SnapshotAndSettlementCollections_AreReadOnly()
        {
            var run = new OrigamiRun();
            Make(run, ModelIds.Boat);
            var s = run.GetSnapshot();
            Assert.Throws<NotSupportedException>(() => ((IList<string>)s.UnlockedModelIds).Add(ModelIds.Dragon));
            Assert.Throws<NotSupportedException>(() => ((IList<CompletedPiece>)s.TodayPieces).Clear());
            var st = EndDay(run);
            Assert.Throws<NotSupportedException>(() => ((IList<SaleLine>)st.Lines).Clear());
            Assert.AreEqual(1, s.TodayPieces.Count, "older snapshot keeps its pieces after settlement");
            Assert.AreEqual(0, run.GetSnapshot().TodayPieces.Count);
        }

        [Test]
        public void Settlement_LinesKeepCompletionOrder_MixedPopularity()
        {
            // Day 2 (boat popular): plane ★3 6, boat ★3 13, plane ★2 (1 hint) 4, boat ★2 (1 mistake) 9.
            var run = new OrigamiRun();
            EndDay(run);
            Make(run, ModelIds.Plane);
            Make(run, ModelIds.Boat);
            run.Start(ModelIds.Plane); run.Hint(); run.Fold(U); run.Fold(L); run.Fold(R);          // ★2
            run.Start(ModelIds.Boat); run.Fold(U); run.Fold(D); run.Fold(U); run.Fold(F); run.Fold(U); // 1 mistake → ★2
            var st = EndDay(run);
            Assert.AreEqual(4, st.Lines.Count);
            CollectionAssert.AreEqual(new[] { ModelIds.Plane, ModelIds.Boat, ModelIds.Plane, ModelIds.Boat },
                new[] { st.Lines[0].ModelId, st.Lines[1].ModelId, st.Lines[2].ModelId, st.Lines[3].ModelId });
            CollectionAssert.AreEqual(new[] { false, true, false, true },
                new[] { st.Lines[0].Popular, st.Lines[1].Popular, st.Lines[2].Popular, st.Lines[3].Popular });
            CollectionAssert.AreEqual(new[] { 6, 13, 4, 9 },
                new[] { st.Lines[0].Price, st.Lines[1].Price, st.Lines[2].Price, st.Lines[3].Price });
            Assert.AreEqual(32, st.Total);
            Assert.AreEqual(2, st.SettledDay);
            Assert.AreEqual(ModelIds.Frog, st.NextPopularModelId);
        }
        [Test]
        public void NewDayWithMoney1_IsNotBankrupt()
        {
            // R-013 boundary: bankrupt only if money < cheapest paper (1), i.e. money 0. Money 1 survives.
            var run = new OrigamiRun();
            for (int d = 1; d <= 3; d++) { Waste(run, ModelIds.Plane, 5); EndDay(run); } // day 4, money 5
            Waste(run, ModelIds.Plane, 4);                                                  // money 1
            var r = run.EndDay();
            Ok(r, CommandOutcome.DayEnded);
            Assert.AreEqual(RunEndReason.None, r.Settlement.EndReason);
            Assert.AreEqual(ModelIds.Crane, r.Settlement.NextPopularModelId, "day 5 popular");
            var s = run.GetSnapshot();
            Assert.AreEqual(5, s.Day);
            Assert.AreEqual(1, s.Money);
            Assert.IsFalse(s.IsRunEnded);
            Ok(run.Start(ModelIds.Plane), CommandOutcome.FoldStarted);
        }

        [Test]
        public void Day7Settlement_LiftsMoneyAbove20_EndingUsesMoneyAfterSettlement()
        {
            // Idle days 1..6 (money 20), day 7: Make(plane) → 19 + 6 = 25 → E-05 (pre-settlement 19 would give E-01).
            var run = new OrigamiRun();
            for (int d = 1; d <= 6; d++) EndDay(run);
            Make(run, ModelIds.Plane);
            Assert.AreEqual(19, run.GetSnapshot().Money);
            var r = run.EndDay();
            Ok(r, CommandOutcome.RunFinished);
            Assert.AreEqual(25, r.Settlement.MoneyAfter);
            Assert.AreEqual(EndingIds.LocalShop, run.GetSnapshot().EndingId);
        }
    }
}
