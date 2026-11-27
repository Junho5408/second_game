using System.Linq;
using NUnit.Framework;
using Origami.Core.Folding;
using Origami.Core.Game;
using Origami.Core.Recognition;
using static Origami.Tests.Qa.WsDriver;

namespace Origami.Tests.Qa
{
    /// <summary>
    /// QA ORIGAMI-010: tutorial edge cases (R-008, F-006, C-150 "가이드 보임"). Guide targets are compared with the QA
    /// transcription of the plane sequence in CONTENT §2.
    /// </summary>
    [TestFixture]
    public class WorkshopTutorialQaTests
    {
        private static void AssertGuideStep(OrigamiGame g, int n)
        {
            var sh = g.Snapshot.Sheet;
            Assert.That(sh, Is.Not.Null);
            Assert.That(sh.GuideVisible, Is.True, "guide visible at step " + n);
            Assert.That(sh.Guide.Number, Is.EqualTo(n));
            var row = V2SpecBook.Book("plane")[n - 1];
            Assert.That(sh.Guide.Kind, Is.EqualTo(row.Kind), "step kind");
            Assert.That(sh.Guide.Grab.X, Is.EqualTo(row.Grab.X).Within(1e-6));
            Assert.That(sh.Guide.Grab.Y, Is.EqualTo(row.Grab.Y).Within(1e-6));
            Assert.That(sh.Guide.Drop.X, Is.EqualTo(row.Drop.X).Within(1e-6));
            Assert.That(sh.Guide.Drop.Y, Is.EqualTo(row.Drop.Y).Within(1e-6));
            Assert.That(sh.ActionCount, Is.EqualTo(n - 1));
        }

        private static OrigamiGame Tutorial()
        {
            var g = new OrigamiGame();
            Sit(g);
            Ok(g, x => x.TakeSheet());
            Assert.That(g.Snapshot.Sheet.IsTutorial, Is.True);
            return g;
        }

        [Test]
        public void ExactPlaneSteps_WalkTheGuide_1To6_AndUndoAfterTheLastBringsStep6Back()
        {
            var g = Tutorial();
            var book = V2SpecBook.Book("plane");
            for (int n = 1; n <= 6; n++)
            {
                AssertGuideStep(g, n);
                var r = Apply(g, book[n - 1], "plane " + n);
                Assert.That(r.GuideScore, Is.Not.Null, "score recorded for step " + n);
                Assert.That(r.GuideScore.Score, Is.EqualTo(100), "exact step " + n);
                Assert.That(r.GuideScore.KindMismatch, Is.False);
                Assert.That(g.Snapshot.Sheet.GuideScores.Count, Is.EqualTo(n));
            }
            Assert.That(g.Snapshot.Sheet.GuideVisible, Is.False);
            Assert.That(g.Snapshot.Sheet.Guide, Is.Null);
            Ok(g, x => x.Undo());
            AssertGuideStep(g, 6);   // ActionCount 5 < 6 → the guide is visible again
            Assert.That(g.Snapshot.Sheet.GuideScores.Count, Is.EqualTo(5));
            Apply(g, book[5], "redo 6");
            var extra = Ok(g, x => x.Fold(V(0, 1), V(0, -1), false, 0), "7th action");
            Assert.That(extra.GuideScore, Is.Null, "no score once the guide is hidden");
            Assert.That(g.Snapshot.Sheet.GuideScores.Count, Is.EqualTo(6));
        }

        [Test]
        public void Flip_DoesNotAdvanceTheGuide_AndRecordsNoScore()
        {
            var g = Tutorial();
            Apply(g, V2SpecBook.Book("plane")[0], "step 1");
            var r = Ok(g, x => x.Flip());
            Assert.That(r.GuideScore, Is.Null);
            AssertGuideStep(g, 2);
            Assert.That(g.Snapshot.Sheet.GuideScores.Count, Is.EqualTo(1));
            Ok(g, x => x.Undo());          // undo the flip only
            AssertGuideStep(g, 2);
            Assert.That(g.Snapshot.Sheet.GuideScores.Count, Is.EqualTo(1), "undoing a flip keeps the step score");
        }

        [Test]
        public void UndoAndUnfold_RewindGuideAndScores()
        {
            var g = Tutorial();
            var book = V2SpecBook.Book("plane");
            for (int n = 0; n < 4; n++) Apply(g, book[n], "step " + (n + 1));
            AssertGuideStep(g, 5);
            Ok(g, x => x.Undo());
            Ok(g, x => x.Undo());
            AssertGuideStep(g, 3);
            Assert.That(g.Snapshot.Sheet.GuideScores.Count, Is.EqualTo(2));
            Apply(g, book[2], "redo 3");
            AssertGuideStep(g, 4);
            Ok(g, x => x.Unfold());
            AssertGuideStep(g, 1);
            Assert.That(g.Snapshot.Sheet.GuideScores.Count, Is.EqualTo(0));
            Assert.That(g.Snapshot.Sheet.State.Creases.Count, Is.EqualTo(0), "unfold clears creases");
            Assert.That(g.Snapshot.Sheet.IsTutorial, Is.True);
        }

        [Test]
        public void WrongKind_ScoresZero_ButAdvances_AndMirroredStep1IsFull()
        {
            var g = Tutorial();
            var r = Ok(g, x => x.Fold(V(-1, 1), V(1, 1), false, 0));
            Assert.That(r.GuideScore.Score, Is.EqualTo(0));
            Assert.That(r.GuideScore.KindMismatch, Is.True);
            AssertGuideStep(g, 2);
            Ok(g, x => x.Undo());
            // Step 1 is EitherSide (A2-9): folding the right edge onto the left edge is the same crease.
            r = Ok(g, x => x.Crease(V(1, 1), V(-1, 1), 0));
            Assert.That(r.GuideScore.Score, Is.EqualTo(100));
            Assert.That(r.GuideScore.WrongSide, Is.False);
        }

        [Test]
        public void Step2_FoldedFromTheOtherSide_IsHalved()
        {
            // F-006 AC-08 "반대쪽 접기 절반": same ideal line, the big part folded onto the corner instead.
            var g = Tutorial();
            Apply(g, V2SpecBook.Book("plane")[0], "step 1");
            var r = Ok(g, x => x.Fold(V(0, 0), V(-1, 1), false, 0));
            Assert.That(r.GuideScore.WrongSide, Is.True);
            Assert.That(r.GuideScore.Score, Is.EqualTo(50));
        }

        [Test]
        public void SkipGuide_IsPermanentForTheSheet_ThroughUndoUnfoldAndStanding()
        {
            var g = Tutorial();
            Apply(g, V2SpecBook.Book("plane")[0], "step 1");
            Ok(g, x => x.SkipGuide());
            Assert.That(g.Snapshot.Sheet.GuideVisible, Is.False);
            Assert.That(g.Snapshot.Sheet.IsTutorial, Is.True, "still the (free) tutorial sheet");
            Assert.That(g.Snapshot.Money, Is.EqualTo(20));
            Rej(g, x => x.SkipGuide(), RejectReason.NoGuide);
            Ok(g, x => x.Undo());
            Assert.That(g.Snapshot.Sheet.GuideVisible, Is.False, "Undo does not re-enable");
            Ok(g, x => x.Fold(V(-1, 1), V(-1, -1), false, 0));
            var r = Ok(g, x => x.Unfold());
            Assert.That(r.GuideScore, Is.Null);
            Assert.That(g.Snapshot.Sheet.GuideVisible, Is.False, "Unfold does not re-enable");
            Stand(g);
            Sit(g);
            Assert.That(g.Snapshot.Sheet.GuideVisible, Is.False, "standing up does not re-enable");
            var f = Ok(g, x => x.Fold(V(-1, 1), V(-1, -1), false, 0));
            Assert.That(f.GuideScore, Is.Null, "no score after skip");
            var fin = Ok(g, x => x.Finish());
            Assert.That(fin.Recognition.IsPiece, Is.False);
            Assert.That(g.Snapshot.TutorialFinished, Is.True);
            Assert.That(g.Snapshot.FreeSheetAvailable, Is.False);
        }

        [Test]
        public void FirstSheetOnDay2_IsFreeTutorialSheet_AndDay2SheetCountDrops()
        {
            var g = new OrigamiGame();
            Night(g);
            Assert.That(g.Snapshot.FreeSheetAvailable, Is.True);
            Sit(g);
            Ok(g, x => x.TakeSheet());
            Assert.That(g.Snapshot.Money, Is.EqualTo(20));
            Assert.That(g.Snapshot.TotalPaperCost, Is.EqualTo(0));
            Assert.That(g.Snapshot.SheetsLeft, Is.EqualTo(4));
            Assert.That(g.Snapshot.FreeSheetAvailable, Is.False);
            Assert.That(g.Snapshot.Sheet.IsTutorial, Is.True);
            AssertGuideStep(g, 1);
        }

        [Test]
        public void AfterTutorialEnds_NoLaterSheetShowsAGuide_EvenOnLaterDays_UntilNewRun()
        {
            var g = new OrigamiGame();
            Sit(g);
            Ok(g, x => x.TakeSheet());
            int i = 0;
            foreach (var row in V2SpecBook.Book("house")) Apply(g, row, "house " + ++i);
            var fin = Ok(g, x => x.Finish());
            Assert.That(fin.Recognition.ModelId, Is.EqualTo("house"), "general recognition on the tutorial sheet");
            Assert.That(fin.Recognition.Grade, Is.EqualTo(Grade.Star3));
            Assert.That(g.Snapshot.TutorialFinished, Is.True);
            Ok(g, x => x.TakeSheet());
            Assert.That(g.Snapshot.Money, Is.EqualTo(18));
            Assert.That(g.Snapshot.Sheet.IsTutorial, Is.False);
            Assert.That(g.Snapshot.Sheet.GuideVisible, Is.False);
            var f = Ok(g, x => x.Crease(V(-1, 1), V(1, 1), 0));
            Assert.That(f.GuideScore, Is.Null);
            Assert.That(g.Snapshot.Sheet.GuideScores.Count, Is.EqualTo(0));
            Ok(g, x => x.Discard());
            Night(g);
            Sit(g);
            Ok(g, x => x.TakeSheet());
            Assert.That(g.Snapshot.Sheet.IsTutorial, Is.False);
            Assert.That(g.Snapshot.Sheet.GuideVisible, Is.False);
            Assert.That(g.Snapshot.Money, Is.EqualTo(20 - 2 + 12 - 2), "day 1 house ★3 normal 12 sold; day 2 sheet paid");

            Ok(g, x => x.NewRun());
            Assert.That(g.Snapshot.FreeSheetAvailable, Is.True);
            Assert.That(g.Snapshot.TutorialFinished, Is.False);
            Assert.That(g.Snapshot.Sheet, Is.Null);
            Sit(g);
            Ok(g, x => x.TakeSheet());
            AssertGuideStep(g, 1);
            Assert.That(g.Snapshot.Money, Is.EqualTo(20));
        }

        [Test]
        public void DiscardingTheTutorialSheet_EndsTheTutorial()
        {
            var g = Tutorial();
            Apply(g, V2SpecBook.Book("plane")[0], "step 1");
            Ok(g, x => x.Discard());
            Assert.That(g.Snapshot.TutorialFinished, Is.True);
            Assert.That(g.Snapshot.FailureCount, Is.EqualTo(1));
            Ok(g, x => x.TakeSheet());
            Assert.That(g.Snapshot.Money, Is.EqualTo(18));
            Assert.That(g.Snapshot.Sheet.GuideVisible, Is.False);
        }

        [Test]
        public void NewRunMidTutorial_ResetsTheGuide()
        {
            var g = Tutorial();
            var book = V2SpecBook.Book("plane");
            Apply(g, book[0], "1");
            Apply(g, book[1], "2");
            Ok(g, x => x.NewRun());
            Assert.That(g.Snapshot.Sheet, Is.Null);
            Assert.That(g.Snapshot.HasSat, Is.False);
            Sit(g);
            Ok(g, x => x.TakeSheet());
            AssertGuideStep(g, 1);
            Assert.That(g.Snapshot.Sheet.GuideScores.Count, Is.EqualTo(0));
        }

        [Test]
        public void FullTutorialPlane_FinishesAsPlaneStar3_AndSellsAtPopular13()
        {
            var g = Tutorial();
            foreach (var row in V2SpecBook.Book("plane")) Apply(g, row, "plane");
            var fin = Ok(g, x => x.Finish());
            Assert.That(fin.Recognition.ModelId, Is.EqualTo("plane"));
            Assert.That(fin.Recognition.Percent, Is.EqualTo(100));
            Assert.That(g.Snapshot.Display.Single().Grade, Is.EqualTo(Grade.Star3));
            Assert.That(g.Snapshot.Display.Single().Percent, Is.EqualTo(100));
            Assert.That(Night(g).Total, Is.EqualTo(13));
            Assert.That(g.Snapshot.Money, Is.EqualTo(33));
        }
    }
}
