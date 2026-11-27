using System;
using System.Collections.Generic;
using NUnit.Framework;
using Origami.Core.Content;
using Origami.Core.Rules;
using Origami.Core.Run;

namespace Origami.Tests.Contracts
{
    // ORIGAMI-002: enums, constants and the completed (non-rule) data types of INTERFACE_CONTRACTS.md.
    public class ValueTypeContractTests
    {
        [Test]
        public void FoldAction_HasFiveMembersAndNoZero()
        {
            CollectionAssert.AreEqual(new[] { "U", "D", "L", "R", "F" }, Enum.GetNames(typeof(FoldAction)));
            Assert.IsFalse(Enum.IsDefined(typeof(FoldAction), 0));
            Assert.IsFalse(FoldActions.IsValid(default(FoldAction)));
            Assert.IsFalse(FoldActions.IsValid((FoldAction)6));
            Assert.IsFalse(FoldActions.IsValid((FoldAction)(-1)));
            foreach (var a in FoldActions.All) Assert.IsTrue(FoldActions.IsValid(a));
            Assert.AreEqual(5, FoldActions.All.Count);
            Assert.AreEqual("F", FoldActions.Symbol(FoldAction.F));
            Assert.AreEqual("?", FoldActions.Symbol((FoldAction)99));
        }

        [Test]
        public void Grade_ValueIsStarCount()
        {
            Assert.AreEqual(1, (int)Grade.Star1);
            Assert.AreEqual(2, (int)Grade.Star2);
            Assert.AreEqual(3, (int)Grade.Star3);
        }

        [Test]
        public void RejectReason_HasAllR015Reasons()
        {
            CollectionAssert.AreEqual(
                new[] { "None", "RunEnded", "FoldInProgress", "UnknownModel", "Locked", "NoAttemptsLeft", "NotEnoughMoney", "NoFold", "InvalidAction" },
                Enum.GetNames(typeof(RejectReason)));
            Assert.AreEqual(0, (int)RejectReason.None);
        }

        [Test]
        public void RunEndReason_Members()
        {
            CollectionAssert.AreEqual(new[] { "None", "Completed", "Bankrupt" }, Enum.GetNames(typeof(RunEndReason)));
        }

        [Test]
        public void RuleConstants_MatchGameRules()
        {
            Assert.AreEqual(7, RuleConstants.TotalDays);
            Assert.AreEqual(20, RuleConstants.StartMoney);
            Assert.AreEqual(5, RuleConstants.DailyAttempts);
            Assert.AreEqual(3, RuleConstants.MistakeLimit);
            Assert.AreEqual(2, RuleConstants.MistakePenalty);
            Assert.AreEqual(1, RuleConstants.HintPenalty);
            Assert.AreEqual(150, RuleConstants.PopularPct);
            Assert.AreEqual(100, RuleConstants.NormalPct);
            Assert.AreEqual(3, RuleConstants.UnlockCount);
            Assert.AreEqual(2, RuleConstants.Star2MaxPenalty);
            Assert.AreEqual(150, RuleConstants.GradePctStar3);
            Assert.AreEqual(100, RuleConstants.GradePctStar2);
            Assert.AreEqual(60, RuleConstants.GradePctStar1);
            Assert.AreEqual(10000, RuleConstants.PriceDivisor);
            Assert.AreEqual(20, RuleConstants.EmptyHandedMoney);
            Assert.AreEqual(1000, RuleConstants.TycoonMoney);
            Assert.AreEqual(15, RuleConstants.CraneCount);
        }

        [Test]
        public void CommandResult_Factories()
        {
            var rej = CommandResult.Rejected(RejectReason.Locked);
            Assert.IsFalse(rej.IsAccepted);
            Assert.AreEqual(CommandOutcome.Rejected, rej.Outcome);
            Assert.AreEqual(RejectReason.Locked, rej.Reason);
            Assert.IsNull(rej.Settlement);

            var ok = CommandResult.Accepted(CommandOutcome.FoldStarted);
            Assert.IsTrue(ok.IsAccepted);
            Assert.AreEqual(RejectReason.None, ok.Reason);
            Assert.AreEqual(default(CompletedPiece), ok.Piece);

            var piece = new CompletedPiece("boat", Grade.Star3);
            var done = CommandResult.Completed(piece);
            Assert.AreEqual(CommandOutcome.FoldCompleted, done.Outcome);
            Assert.AreEqual(piece, done.Piece);

            var s = new SettlementResult(1, new List<SaleLine>(), 0, 20, RunEndReason.None, "boat");
            var settled = CommandResult.Settled(CommandOutcome.DayEnded, s);
            Assert.AreSame(s, settled.Settlement);

            Assert.IsFalse(default(CommandResult).IsAccepted, "default result reads as Rejected/None");
        }

        [Test]
        public void CompletedPiece_ValueEquality()
        {
            Assert.AreEqual(new CompletedPiece("boat", Grade.Star2), new CompletedPiece("boat", Grade.Star2));
            Assert.AreNotEqual(new CompletedPiece("boat", Grade.Star2), new CompletedPiece("boat", Grade.Star3));
            Assert.AreNotEqual(new CompletedPiece("boat", Grade.Star2), new CompletedPiece("frog", Grade.Star2));
            Assert.IsTrue(new CompletedPiece("plane", Grade.Star1) == new CompletedPiece("plane", Grade.Star1));
            Assert.AreEqual("boat ★3", new CompletedPiece("boat", Grade.Star3).ToString());
        }

        [Test]
        public void Snapshots_CopyInputLists()
        {
            var unlocked = new List<string> { "plane", "boat" };
            var pieces = new List<CompletedPiece> { new CompletedPiece("boat", Grade.Star3) };
            var steps = new List<FoldAction> { FoldAction.D };
            var fold = new FoldSnapshot("boat", 4, steps, 0, 1, FoldAction.U, 1, Grade.Star2);
            var snap = new RunSnapshot(1, 19, 4, "plane", unlocked, CompletionCounts.Empty, 0, 0, 1, pieces, fold,
                RunEndReason.None, null);
            unlocked.Add("frog");
            pieces.Clear();
            steps.Add(FoldAction.U);

            CollectionAssert.AreEqual(new[] { "plane", "boat" }, snap.UnlockedModelIds);
            Assert.AreEqual(1, snap.TodayPieces.Count);
            Assert.AreEqual(1, fold.Progress);
            Assert.IsTrue(snap.IsFolding);
            Assert.IsFalse(snap.IsRunEnded);
            Assert.IsTrue(snap.IsUnlocked("boat"));
            Assert.IsFalse(snap.IsUnlocked("frog"));
            Assert.IsFalse(snap.IsUnlocked(null));
            Assert.IsTrue(fold.IsCurrentStepRevealed);
            Assert.IsFalse(new FoldSnapshot("boat", 4, steps, 0, 0, default, 0, Grade.Star3).IsCurrentStepRevealed);
        }

        [Test]
        public void SettlementResult_RunEndedFollowsEndReason()
        {
            var lines = new List<SaleLine> { new SaleLine("boat", Grade.Star3, false, 9) };
            var s = new SettlementResult(7, lines, 9, 29, RunEndReason.Completed, null);
            lines.Clear();
            Assert.AreEqual(1, s.Lines.Count);
            Assert.IsTrue(s.RunEnded);
            Assert.IsFalse(new SettlementResult(1, new List<SaleLine>(), 0, 20, RunEndReason.None, "boat").RunEnded);
        }
    }
}
