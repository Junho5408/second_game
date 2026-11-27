using NUnit.Framework;
using Origami.Core.Content;
using Origami.Core.Run;
using static Origami.Core.Content.FoldAction;
using static Origami.Tests.Run.RunScenarios;

namespace Origami.Tests.Run
{
    // ORIGAMI-003: F-003 fold input (R-005..R-009, R-015). plane = U L R, boat = D U F U.
    public class F003FoldTests
    {
        private static OrigamiRun Started(string modelId)
        {
            var run = new OrigamiRun();
            Accept(run.Start(modelId), CommandOutcome.FoldStarted, "Start");
            return run;
        }

        [Test]
        public void F003_AC01_CorrectSequence_CompletesStar3()
        {
            var run = Started(ModelIds.Boat);
            Accept(run.Fold(D), CommandOutcome.StepCorrect, "D");
            Accept(run.Fold(U), CommandOutcome.StepCorrect, "U");
            Accept(run.Fold(F), CommandOutcome.StepCorrect, "F");
            var r = run.Fold(U);
            Accept(r, CommandOutcome.FoldCompleted, "last U");
            Assert.AreEqual(new CompletedPiece(ModelIds.Boat, Grade.Star3), r.Piece);
            var s = run.GetSnapshot();
            Assert.IsFalse(s.IsFolding);
            CollectionAssert.AreEqual(Pieces(ModelIds.Boat, Grade.Star3), s.TodayPieces);
            Assert.AreEqual(1, s.Completions.Get(ModelIds.Boat, Grade.Star3));
            Assert.AreEqual(1, s.Completions.Total(ModelIds.Boat));
            Assert.AreEqual(19, s.Money, "money unchanged until settlement");
            Assert.AreEqual(0, s.TotalSales);
        }

        [Test]
        public void F003_AC02_ProgressAndCorrectSymbols()
        {
            var run = Started(ModelIds.Boat);
            run.Fold(D);
            var f = run.GetSnapshot().Fold;
            Assert.AreEqual(1, f.Progress);
            Assert.AreEqual(4, f.StepCount);
            CollectionAssert.AreEqual(new[] { D }, f.CorrectSteps);
        }

        [Test]
        public void F003_AC03_OneMistake_Star2()
        {
            var run = Started(ModelIds.Boat);
            Accept(run.Fold(L), CommandOutcome.StepMistake, "L");
            var f = run.GetSnapshot().Fold;
            Assert.AreEqual(1, f.Mistakes);
            Assert.AreEqual(0, f.Progress);
            Assert.AreEqual(2, f.Penalty);
            var r = Input(run, D, U, F, U);
            Assert.AreEqual(new CompletedPiece(ModelIds.Boat, Grade.Star2), r.Piece);
            CollectionAssert.AreEqual(Pieces(ModelIds.Boat, Grade.Star2), run.GetSnapshot().TodayPieces);
        }

        [Test]
        public void F003_AC04_TwoMistakes_Star1_NotRuined()
        {
            var run = Started(ModelIds.Boat);
            Input(run, D, L, U, R, F);
            Assert.AreEqual(2, run.GetSnapshot().Fold.Mistakes);
            Assert.AreEqual(4, run.GetSnapshot().Fold.Penalty);
            var r = run.Fold(U);
            Accept(r, CommandOutcome.FoldCompleted, "last step with 2 mistakes");
            Assert.AreEqual(new CompletedPiece(ModelIds.Boat, Grade.Star1), r.Piece);
            Assert.AreEqual(0, run.GetSnapshot().FailureCount);
        }

        [Test]
        public void F003_AC05_ThreeMistakes_Ruined()
        {
            var run = Started(ModelIds.Boat);
            Accept(run.Fold(L), CommandOutcome.StepMistake, "1st");
            Accept(run.Fold(L), CommandOutcome.StepMistake, "2nd");
            Accept(run.Fold(L), CommandOutcome.FoldRuined, "3rd");
            var s = run.GetSnapshot();
            Assert.IsFalse(s.IsFolding);
            Assert.AreEqual(1, s.FailureCount);
            Assert.AreEqual(0, s.TodayPieces.Count);
            Assert.AreEqual(0, s.Completions.Total(ModelIds.Boat));
            Assert.AreEqual(19, s.Money);
            Assert.AreEqual(4, s.AttemptsLeft);
        }

        [Test]
        public void F003_AC06_RuinedAtThirdMistakeMidFold()
        {
            var run = Started(ModelIds.Boat);
            Input(run, D, L, U, R, F);
            Assert.AreEqual(3, run.GetSnapshot().Fold.Progress);
            Accept(run.Fold(L), CommandOutcome.FoldRuined, "3rd mistake");
            var s = run.GetSnapshot();
            Assert.AreEqual(1, s.FailureCount);
            Assert.AreEqual(0, s.TodayPieces.Count);
            Assert.IsFalse(s.IsFolding);
        }

        [Test]
        public void F003_AC07_HintCountsOncePerStep()
        {
            var run = Started(ModelIds.Boat);
            Accept(run.Hint(), CommandOutcome.HintRevealed, "Hint");
            var f = run.GetSnapshot().Fold;
            Assert.IsTrue(f.IsCurrentStepRevealed);
            Assert.AreEqual(D, f.RevealedAction);
            Assert.AreEqual(1, f.Hints);
            Accept(run.Hint(), CommandOutcome.HintRevealed, "Hint again");
            Assert.AreEqual(1, run.GetSnapshot().Fold.Hints);
            Assert.AreEqual(D, run.GetSnapshot().Fold.RevealedAction);
            run.Fold(D);
            Assert.IsFalse(run.GetSnapshot().Fold.IsCurrentStepRevealed, "next step not revealed");
            Assert.AreEqual((FoldAction)0, run.GetSnapshot().Fold.RevealedAction);
            run.Hint();
            Assert.AreEqual(U, run.GetSnapshot().Fold.RevealedAction);
            Assert.AreEqual(2, run.GetSnapshot().Fold.Hints);
            var r = Input(run, U, F, U);
            Assert.AreEqual(new CompletedPiece(ModelIds.Boat, Grade.Star2), r.Piece);
        }

        [Test]
        public void F003_AC08_ThreeHints_Star1()
        {
            var run = Started(ModelIds.Plane);
            run.Hint(); run.Fold(U);
            run.Hint(); run.Fold(L);
            run.Hint();
            Assert.AreEqual(3, run.GetSnapshot().Fold.Hints);
            var r = run.Fold(R);
            Assert.AreEqual(new CompletedPiece(ModelIds.Plane, Grade.Star1), r.Piece);
        }

        [Test]
        public void F003_AC09_MistakePlusHint_Penalty3_Star1()
        {
            var run = Started(ModelIds.Plane);
            run.Hint();
            Accept(run.Fold(D), CommandOutcome.StepMistake, "D wrong");
            Input(run, U, L);
            var f = run.GetSnapshot().Fold;
            Assert.AreEqual(1, f.Mistakes);
            Assert.AreEqual(1, f.Hints);
            Assert.AreEqual(3, f.Penalty);
            var r = run.Fold(R);
            Assert.AreEqual(new CompletedPiece(ModelIds.Plane, Grade.Star1), r.Piece);
        }

        [Test]
        public void F003_AC10_RevealStaysAfterMistake()
        {
            var run = Started(ModelIds.Plane);
            run.Hint();
            run.Fold(D);
            var f = run.GetSnapshot().Fold;
            Assert.IsTrue(f.IsCurrentStepRevealed);
            Assert.AreEqual(U, f.RevealedAction);
            Assert.AreEqual(1, f.Mistakes);
            Assert.AreEqual(1, f.Hints);
        }

        [Test]
        public void F003_AC11_AbandonAtStep0()
        {
            var run = Started(ModelIds.Plane);
            Accept(run.Abandon(), CommandOutcome.FoldAbandoned, "Abandon");
            var s = run.GetSnapshot();
            Assert.IsFalse(s.IsFolding);
            Assert.AreEqual(1, s.FailureCount);
            Assert.AreEqual(19, s.Money);
            Assert.AreEqual(4, s.AttemptsLeft);
            Assert.AreEqual(1, s.TotalPaperCost);
            Assert.AreEqual(0, s.TodayPieces.Count);
        }

        [Test]
        public void F003_AC12_NoFold_WhenNotFolding()
        {
            var run = new OrigamiRun();
            AssertRejected(run, x => x.Fold(U), RejectReason.NoFold, "In(U)");
            AssertRejected(run, x => x.Hint(), RejectReason.NoFold, "Hint");
            AssertRejected(run, x => x.Abandon(), RejectReason.NoFold, "Abandon");
            Make(run, ModelIds.Plane);
            AssertRejected(run, x => x.Fold(U), RejectReason.NoFold, "In(U) after completion");
        }

        [Test]
        public void F003_AC12_NoFold_AfterRuinAndAbandon()
        {
            var run = Started(ModelIds.Boat);
            Input(run, L, L, L);
            AssertRejected(run, x => x.Fold(D), RejectReason.NoFold, "after ruin");
            run.Start(ModelIds.Boat);
            run.Abandon();
            AssertRejected(run, x => x.Fold(D), RejectReason.NoFold, "after abandon");
            AssertRejected(run, x => x.Abandon(), RejectReason.NoFold, "abandon twice");
        }

        [Test]
        public void F003_AC13_ExpectedGrade()
        {
            var run = Started(ModelIds.Boat);
            Assert.AreEqual(Grade.Star3, run.GetSnapshot().Fold.ExpectedGrade);
            run.Hint();
            Assert.AreEqual(Grade.Star2, run.GetSnapshot().Fold.ExpectedGrade);
            run.Fold(L);
            Assert.AreEqual(Grade.Star1, run.GetSnapshot().Fold.ExpectedGrade);
            Assert.AreEqual(3, run.GetSnapshot().Fold.Penalty);
        }

        [Test]
        public void F003_AC14_AllModels_Star3_StepCounts()
        {
            var expected = new[] { 3, 4, 6, 8, 10 };
            for (int i = 0; i < expected.Length; i++)
                Assert.AreEqual(expected[i], OrigamiCatalog.Models[i].StepCount, OrigamiCatalog.Models[i].Id);

            var run = new OrigamiRun();
            Assert.AreEqual(Grade.Star3, Make(run, ModelIds.Plane).Grade);
            EndDay(run);
            Make(run, ModelIds.Boat, 3);
            Assert.AreEqual(Grade.Star3, Make(run, ModelIds.Frog).Grade);
            Make(run, ModelIds.Frog);
            EndDay(run);
            Make(run, ModelIds.Frog);
            Make(run, ModelIds.Crane, 3);
            Assert.AreEqual(new CompletedPiece(ModelIds.Dragon, Grade.Star3), Make(run, ModelIds.Dragon));
            var c = run.GetSnapshot().Completions;
            foreach (var m in OrigamiCatalog.Models)
                Assert.GreaterOrEqual(c.Get(m.Id, Grade.Star3), 1, m.Id);
        }

        [Test]
        public void R005_FoldSnapshotNeverShowsRemainingSteps()
        {
            var run = Started(ModelIds.Boat);
            run.Fold(D);
            var f = run.GetSnapshot().Fold;
            Assert.AreEqual(1, f.CorrectSteps.Count);
            Assert.IsFalse(f.IsCurrentStepRevealed);
        }

        [Test]
        public void F003_AC15_InvalidAction_NotAMistake()
        {
            var run = Started(ModelIds.Boat);
            for (int i = 0; i < 3; i++)
            {
                AssertRejected(run, x => x.Fold((FoldAction)0), RejectReason.InvalidAction, "0 #" + i);
                AssertRejected(run, x => x.Fold((FoldAction)99), RejectReason.InvalidAction, "99 #" + i);
                AssertRejected(run, x => x.Fold((FoldAction)(-1)), RejectReason.InvalidAction, "-1 #" + i);
            }
            var f = run.GetSnapshot().Fold;
            Assert.IsNotNull(f, "still folding");
            Assert.AreEqual(0, f.Progress);
            Assert.AreEqual(4, f.StepCount);
            Assert.AreEqual(0, f.Mistakes);
            Assert.AreEqual(0, f.Hints);

            var fresh = new OrigamiRun();
            AssertRejected(fresh, x => x.Fold((FoldAction)0), RejectReason.NoFold, "not folding");
        }
    }
}
