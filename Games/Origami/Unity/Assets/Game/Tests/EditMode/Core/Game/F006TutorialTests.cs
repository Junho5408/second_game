using NUnit.Framework;
using Origami.Core.Catalog;
using Origami.Core.Folding;
using Origami.Core.Game;
using Origami.Core.Recognition;
using Origami.Core.Tutorial;
using static Origami.Tests.Game.GameScenarios;

namespace Origami.Tests.Game
{
    // F-006 tutorial guide on the first sheet (R-008, C-150 "가이드 보임").
    public class F006TutorialTests
    {
        private static OrigamiGame Tutorial()
        {
            var g = new OrigamiGame();
            Sit(g);
            Take(g);
            return g;
        }

        [Test]
        public void F006_AC01_FirstGuideStep()
        {
            var s = Tutorial().Snapshot.Sheet;
            Assert.IsTrue(s.GuideVisible);
            Assert.AreSame(PlaneGuide.Steps[0], s.Guide);
            Assert.AreEqual(1, s.Guide.Number);
            Assert.AreEqual(FoldStepKind.Crease, s.Guide.Kind);
            Assert.AreEqual(V(-1, 1), s.Guide.Grab);
            Assert.AreEqual(V(1, 1), s.Guide.Drop);
        }

        [Test]
        public void F006_AC02_ExactStep1_ThenUndo()
        {
            var g = Tutorial();
            var r = Crease(g, V(-1, 1), V(1, 1));
            Assert.IsNotNull(r.GuideScore);
            Assert.AreEqual(100, r.GuideScore.Score);
            var s = g.Snapshot.Sheet;
            Assert.AreEqual(1, s.GuideScores.Count);
            Assert.AreSame(r.GuideScore, s.GuideScores[0]);
            Assert.AreEqual(2, s.Guide.Number);
            Assert.AreEqual(V(-1, 1), s.Guide.Grab);
            Assert.AreEqual(V(0, 0), s.Guide.Drop);
            Ok(g, g.Undo());
            Assert.AreEqual(1, g.Snapshot.Sheet.Guide.Number);
            Assert.AreEqual(0, g.Snapshot.Sheet.GuideScores.Count);
        }

        [Test]
        public void F006_AC02_WrongKindScoresZero()
        {
            var g = Tutorial();
            var r = Fold(g, V(-1, 1), V(1, 1));
            Assert.AreEqual(0, r.GuideScore.Score);
            Assert.IsTrue(r.GuideScore.KindMismatch);
            Assert.AreEqual(2, g.Snapshot.Sheet.Guide.Number);
        }

        [Test]
        public void F006_AC03_AllSixStepsExact_ThenFinish()
        {
            var g = Tutorial();
            var results = FoldSteps(g, ModelIds.Plane);
            foreach (var r in results) Assert.AreEqual(100, r.GuideScore.Score);
            var s = g.Snapshot.Sheet;
            Assert.IsFalse(s.GuideVisible);
            Assert.IsNull(s.Guide);
            Assert.AreEqual(6, s.GuideScores.Count);
            foreach (var sc in s.GuideScores) Assert.AreEqual(100, sc.Score);
            var f = Finish(g);
            Assert.AreEqual(ModelIds.Plane, f.Recognition.ModelId);
            var d = g.Snapshot.Display;
            Assert.AreEqual(1, d.Count);
            Assert.AreEqual(ModelIds.Plane, d[0].ModelId);
            Assert.AreEqual(Grade.Star3, d[0].Grade);
            Assert.AreEqual(100, d[0].Percent);   // exact reference state (J-01 is the only percent asserted)
            Assert.IsTrue(g.Snapshot.TutorialFinished);
            Take(g);
            Assert.AreEqual(18, g.Snapshot.Money);
            Assert.IsFalse(g.Snapshot.Sheet.GuideVisible);
        }

        [Test]
        public void F006_AC03_FlipDoesNotAdvanceOrScore()
        {
            var g = Tutorial();
            Crease(g, V(-1, 1), V(1, 1));
            var r = Ok(g, g.Flip());
            Assert.IsNull(r.GuideScore);
            Assert.AreEqual(2, g.Snapshot.Sheet.Guide.Number);
            Assert.AreEqual(1, g.Snapshot.Sheet.GuideScores.Count);
            Ok(g, g.Undo());   // undo the flip: scores stay
            Assert.AreEqual(1, g.Snapshot.Sheet.GuideScores.Count);
        }

        [Test]
        public void F006_AC04_SkipGuide()
        {
            var g = Tutorial();
            Ok(g, g.SkipGuide());
            Assert.IsFalse(g.Snapshot.Sheet.GuideVisible);
            Assert.IsNull(g.Snapshot.Sheet.Guide);
            Assert.IsTrue(g.Snapshot.Sheet.IsTutorial);
            Assert.AreEqual(20, g.Snapshot.Money);
            Rejected(g, x => x.SkipGuide(), RejectReason.NoGuide);
            // QA O-1: Unfold on an empty history is rejected — fold once first.
            var r = Fold(g, V(-1, 1), V(-1, -1));
            Assert.IsNull(r.GuideScore);
            Ok(g, g.Unfold());
            Assert.IsFalse(g.Snapshot.Sheet.GuideVisible);
            Assert.AreEqual(0, g.Snapshot.Sheet.GuideScores.Count);
        }

        [Test]
        public void F006_AC04_SkipGuideOrder()
        {
            var g = new OrigamiGame();
            Rejected(g, x => x.SkipGuide(), RejectReason.WrongMode);
            Sit(g);
            Rejected(g, x => x.SkipGuide(), RejectReason.NoGuide);   // no sheet (A2-10)
            Take(g);
            Stand(g);
            Rejected(g, x => x.SkipGuide(), RejectReason.WrongMode);
        }

        [Test]
        public void F006_AC05_DiscardEndsTheTutorial()
        {
            var g = Tutorial();
            Ok(g, g.Discard());
            Assert.IsTrue(g.Snapshot.TutorialFinished);
            Assert.AreEqual(1, g.Snapshot.FailureCount);
            Take(g);
            Assert.AreEqual(18, g.Snapshot.Money);
            Assert.IsFalse(g.Snapshot.Sheet.IsTutorial);
            Assert.IsFalse(g.Snapshot.Sheet.GuideVisible);
        }

        [Test]
        public void F006_AC06_HouseOnTheTutorialSheet()
        {
            var g = Tutorial();
            FoldSteps(g, ModelIds.House);
            var r = Finish(g);
            Assert.AreEqual(ModelIds.House, r.Recognition.ModelId);
            Assert.AreEqual(Grade.Star3, r.Recognition.Grade);
            Assert.IsTrue(g.Snapshot.TutorialFinished);
        }

        [Test]
        public void F006_AC06_CrumpledTutorialSheetAlsoEndsTheTutorial()
        {
            var g = Tutorial();
            Fold(g, V(-1, 1), V(-1, -1));
            var r = Finish(g);
            Assert.IsFalse(r.Recognition.IsPiece);
            Assert.IsTrue(g.Snapshot.TutorialFinished);
        }

        [Test]
        public void F006_AC07_UnfoldAfterThreeSteps()
        {
            var g = Tutorial();
            FoldSteps(g, ModelIds.Plane, 1, 3);
            Assert.AreEqual(4, g.Snapshot.Sheet.Guide.Number);
            Assert.AreEqual(3, g.Snapshot.Sheet.GuideScores.Count);
            Ok(g, g.Unfold());
            Assert.AreEqual(1, g.Snapshot.Sheet.Guide.Number);
            Assert.AreEqual(0, g.Snapshot.Sheet.State.Creases.Count);
            Assert.AreEqual(0, g.Snapshot.Sheet.GuideScores.Count);
        }

        [Test]
        public void F006_AC08_GameRecordsStepScoringResult()
        {
            var g = Tutorial();
            var r = Crease(g, V(-1, 1), V(1.25, 1));
            var expected = StepScoring.Evaluate(PlaneGuide.Steps[0], FoldStepKind.Crease, V(-1, 1), V(1.25, 1));
            Assert.AreEqual(expected.Score, r.GuideScore.Score);
            Assert.AreEqual(75, r.GuideScore.Score);
            Assert.AreEqual(expected.CreaseOffset, r.GuideScore.CreaseOffset);
        }

        [Test]
        public void R008_StepSixScoredThenGuideGone_NoScoreAfter()
        {
            var g = Tutorial();
            FoldSteps(g, ModelIds.Plane);
            var (grab, drop) = AnyValidFold(g.Snapshot.Sheet.State);
            var r = Fold(g, grab, drop);   // 7th action: guide no longer visible
            Assert.IsNull(r.GuideScore);
            Assert.AreEqual(6, g.Snapshot.Sheet.GuideScores.Count);
            Ok(g, g.Undo());
            Ok(g, g.Undo());   // back to 5 actions → guide step 6 visible again, 5 scores
            Assert.IsTrue(g.Snapshot.Sheet.GuideVisible);
            Assert.AreEqual(6, g.Snapshot.Sheet.Guide.Number);
            Assert.AreEqual(5, g.Snapshot.Sheet.GuideScores.Count);
        }

        private static (Origami.Core.Geometry.Vec2, Origami.Core.Geometry.Vec2) AnyValidFold(PaperState state)
        {
            foreach (var f in state.Facets)
                foreach (var p in f.World)
                    foreach (var d in new[] { V(0.3, 0), V(-0.3, 0), V(0, 0.3), V(0, -0.3) })
                        if (FoldPlanner.PlanFold(state, p, p + d, false, 0).Valid) return (p, p + d);
            Assert.Fail("no valid fold found");
            return default;
        }

        [Test]
        public void R008_InvalidFoldDuringTutorialRecordsNothing()
        {
            var g = Tutorial();
            Rejected(g, x => x.Crease(V(-1, 1), V(-1, 0.99), 0), RejectReason.InvalidFold);
            Assert.AreEqual(0, g.Snapshot.Sheet.GuideScores.Count);
            Assert.AreEqual(1, g.Snapshot.Sheet.Guide.Number);
        }

        [Test]
        public void R008_NewRunRestartsTheTutorial()
        {
            var g = Tutorial();
            Ok(g, g.Discard());
            Ok(g, g.NewRun());
            Sit(g);
            Take(g);
            Assert.IsTrue(g.Snapshot.Sheet.IsTutorial);
            Assert.AreEqual(20, g.Snapshot.Money);
        }
    }
}
