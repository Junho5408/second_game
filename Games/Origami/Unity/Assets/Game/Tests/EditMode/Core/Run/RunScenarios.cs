using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Origami.Core.Content;
using Origami.Core.Run;

namespace Origami.Tests.Run
{
    // ORIGAMI-003: scenario helpers for OrigamiRun tests (FEATURE_SPECS "공통 표기").
    internal static class RunScenarios
    {
        /// <summary>Make(m): Start(m), then the correct steps without mistakes or hints (→ ★3).</summary>
        public static CompletedPiece Make(OrigamiRun run, string modelId)
        {
            Accept(run.Start(modelId), CommandOutcome.FoldStarted, "Start(" + modelId + ")");
            var steps = OrigamiCatalog.FindModel(modelId).Steps;
            CommandResult r = default;
            for (int i = 0; i < steps.Count; i++)
            {
                r = run.Fold(steps[i]);
                Accept(r, i == steps.Count - 1 ? CommandOutcome.FoldCompleted : CommandOutcome.StepCorrect,
                    modelId + " step " + (i + 1));
            }
            Assert.AreEqual(Grade.Star3, r.Piece.Grade, "Make(" + modelId + ") grade");
            return r.Piece;
        }

        public static void Make(OrigamiRun run, string modelId, int times)
        {
            for (int i = 0; i < times; i++) Make(run, modelId);
        }

        /// <summary>Waste(m): Start(m) then Abandon.</summary>
        public static void Waste(OrigamiRun run, string modelId, int times = 1)
        {
            for (int i = 0; i < times; i++)
            {
                Accept(run.Start(modelId), CommandOutcome.FoldStarted, "Start(" + modelId + ")");
                Accept(run.Abandon(), CommandOutcome.FoldAbandoned, "Abandon");
            }
        }

        public static CommandResult EndDay(OrigamiRun run)
        {
            var r = run.EndDay();
            Assert.IsTrue(r.IsAccepted, "EndDay: " + r);
            Assert.IsNotNull(r.Settlement, "EndDay settlement");
            return r;
        }

        /// <summary>Feeds actions in order, asserting each is accepted; returns the last result.</summary>
        public static CommandResult Input(OrigamiRun run, params FoldAction[] actions)
        {
            CommandResult r = default;
            foreach (var a in actions)
            {
                r = run.Fold(a);
                Assert.IsTrue(r.IsAccepted, "Fold(" + a + "): " + r);
            }
            return r;
        }

        /// <summary>Days 1..n: Waste(plane)×5 + EndDay each (money 20 − 5n).</summary>
        public static void WastedDays(OrigamiRun run, int days)
        {
            for (int d = 0; d < days; d++)
            {
                Waste(run, ModelIds.Plane, 5);
                EndDay(run);
            }
        }

        public static void Accept(CommandResult r, CommandOutcome expected, string what)
        {
            Assert.IsTrue(r.IsAccepted, what + ": " + r);
            Assert.AreEqual(RejectReason.None, r.Reason, what);
            Assert.AreEqual(expected, r.Outcome, what);
        }

        /// <summary>Runs the command, asserts it was rejected with <paramref name="reason"/> and that the snapshot is unchanged (R-015).</summary>
        public static void AssertRejected(OrigamiRun run, Func<OrigamiRun, CommandResult> command, RejectReason reason, string what = "")
        {
            var before = run.GetSnapshot();
            var r = command(run);
            Assert.IsFalse(r.IsAccepted, what + " accepted: " + r);
            Assert.AreNotEqual(RejectReason.None, r.Reason, what + " rejected with None");
            Assert.AreEqual(reason, r.Reason, what);
            Assert.AreEqual(CommandOutcome.Rejected, r.Outcome, what);
            Assert.IsNull(r.Settlement, what);
            AssertSameState(before, run.GetSnapshot(), what);
        }

        /// <summary>Field-by-field snapshot comparison (CODING_CONVENTION "거부 시 상태 불변").</summary>
        public static void AssertSameState(RunSnapshot a, RunSnapshot b, string what = "")
        {
            Assert.AreEqual(a.Day, b.Day, what + " Day");
            Assert.AreEqual(a.Money, b.Money, what + " Money");
            Assert.AreEqual(a.AttemptsLeft, b.AttemptsLeft, what + " AttemptsLeft");
            Assert.AreEqual(a.PopularModelId, b.PopularModelId, what + " PopularModelId");
            CollectionAssert.AreEqual(a.UnlockedModelIds, b.UnlockedModelIds, what + " UnlockedModelIds");
            Assert.IsTrue(a.Completions.Equals(b.Completions), what + " Completions");
            Assert.AreEqual(a.FailureCount, b.FailureCount, what + " FailureCount");
            Assert.AreEqual(a.TotalSales, b.TotalSales, what + " TotalSales");
            Assert.AreEqual(a.TotalPaperCost, b.TotalPaperCost, what + " TotalPaperCost");
            CollectionAssert.AreEqual(a.TodayPieces, b.TodayPieces, what + " TodayPieces");
            Assert.AreEqual(a.EndReason, b.EndReason, what + " EndReason");
            Assert.AreEqual(a.EndingId, b.EndingId, what + " EndingId");
            Assert.AreEqual(a.IsFolding, b.IsFolding, what + " IsFolding");
            if (a.Fold != null && b.Fold != null)
            {
                Assert.AreEqual(a.Fold.ModelId, b.Fold.ModelId, what + " Fold.ModelId");
                Assert.AreEqual(a.Fold.StepCount, b.Fold.StepCount, what + " Fold.StepCount");
                CollectionAssert.AreEqual(a.Fold.CorrectSteps, b.Fold.CorrectSteps, what + " Fold.CorrectSteps");
                Assert.AreEqual(a.Fold.Mistakes, b.Fold.Mistakes, what + " Fold.Mistakes");
                Assert.AreEqual(a.Fold.Hints, b.Fold.Hints, what + " Fold.Hints");
                Assert.AreEqual(a.Fold.RevealedAction, b.Fold.RevealedAction, what + " Fold.RevealedAction");
                Assert.AreEqual(a.Fold.Penalty, b.Fold.Penalty, what + " Fold.Penalty");
                Assert.AreEqual(a.Fold.ExpectedGrade, b.Fold.ExpectedGrade, what + " Fold.ExpectedGrade");
            }
        }

        /// <summary>F-001 AC-01 start state.</summary>
        public static void AssertStartState(RunSnapshot s)
        {
            Assert.AreEqual(1, s.Day, "Day");
            Assert.AreEqual(20, s.Money, "Money");
            Assert.AreEqual(5, s.AttemptsLeft, "AttemptsLeft");
            Assert.AreEqual(ModelIds.Plane, s.PopularModelId, "PopularModelId");
            CollectionAssert.AreEqual(new[] { ModelIds.Plane, ModelIds.Boat }, s.UnlockedModelIds, "Unlocked");
            Assert.IsFalse(s.IsUnlocked(ModelIds.Frog));
            Assert.IsFalse(s.IsUnlocked(ModelIds.Crane));
            Assert.IsFalse(s.IsUnlocked(ModelIds.Dragon));
            Assert.IsTrue(s.Completions.Equals(Origami.Core.Rules.CompletionCounts.Empty), "Completions");
            Assert.AreEqual(0, s.FailureCount, "FailureCount");
            Assert.AreEqual(0, s.TotalSales, "TotalSales");
            Assert.AreEqual(0, s.TotalPaperCost, "TotalPaperCost");
            Assert.AreEqual(0, s.TodayPieces.Count, "TodayPieces");
            Assert.IsFalse(s.IsFolding, "IsFolding");
            Assert.IsNull(s.Fold, "Fold");
            Assert.IsFalse(s.IsRunEnded, "IsRunEnded");
            Assert.AreEqual(Origami.Core.Rules.RunEndReason.None, s.EndReason, "EndReason");
            Assert.IsNull(s.EndingId, "EndingId");
        }

        public static List<CompletedPiece> Pieces(params object[] idGradePairs)
        {
            var list = new List<CompletedPiece>();
            for (int i = 0; i < idGradePairs.Length; i += 2)
                list.Add(new CompletedPiece((string)idGradePairs[i], (Grade)idGradePairs[i + 1]));
            return list;
        }

        public static int[] Prices(SettlementResult s) => s.Lines.Select(l => l.Price).ToArray();
    }
}
