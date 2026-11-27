using NUnit.Framework;
using Origami.Core.Content;
using Origami.Core.Run;
using static Origami.Core.Content.FoldAction;
using static Origami.Tests.Run.RunScenarios;

namespace Origami.Tests.Run
{
    // ORIGAMI-003: F-004 unlocking inside a run (R-010).
    public class F004UnlockTests
    {
        [Test]
        public void F004_AC01_ThreeBoats_FrogUnlockedSameDay()
        {
            var run = new OrigamiRun();
            Make(run, ModelIds.Boat, 2);
            Assert.IsFalse(run.GetSnapshot().IsUnlocked(ModelIds.Frog));
            Make(run, ModelIds.Boat);
            var s = run.GetSnapshot();
            Assert.IsTrue(s.IsUnlocked(ModelIds.Frog));
            CollectionAssert.AreEqual(new[] { ModelIds.Plane, ModelIds.Boat, ModelIds.Frog }, s.UnlockedModelIds);
            Accept(run.Start(ModelIds.Frog), CommandOutcome.FoldStarted, "Start(frog)");
        }

        [Test]
        public void F004_AC02_WasteDoesNotCount()
        {
            var run = new OrigamiRun();
            Make(run, ModelIds.Boat, 2);
            Waste(run, ModelIds.Boat);
            Assert.IsFalse(run.GetSnapshot().IsUnlocked(ModelIds.Frog));
            // A ruined boat does not count either.
            run.Start(ModelIds.Boat);
            Input(run, L, L, L);
            Assert.IsFalse(run.GetSnapshot().IsUnlocked(ModelIds.Frog));
            AssertRejected(run, x => x.Start(ModelIds.Frog), RejectReason.Locked);
        }

        [Test]
        public void F004_AC03_AccumulatesAcrossDays()
        {
            var run = new OrigamiRun();
            Make(run, ModelIds.Boat);
            EndDay(run);
            Make(run, ModelIds.Boat);
            EndDay(run);
            Assert.IsFalse(run.GetSnapshot().IsUnlocked(ModelIds.Frog));
            Make(run, ModelIds.Boat);
            Assert.IsTrue(run.GetSnapshot().IsUnlocked(ModelIds.Frog));
        }

        [Test]
        public void F004_AC04_Star1BoatsUnlockFrog()
        {
            var run = new OrigamiRun();
            // Boat D U F U: hints on three different steps (penalty 3 → ★1) each time.
            int[][] hintSteps = { new[] { 0, 1, 2 }, new[] { 1, 2, 3 }, new[] { 0, 2, 3 } };
            var steps = OrigamiCatalog.FindModel(ModelIds.Boat).Steps;
            foreach (var hinted in hintSteps)
            {
                run.Start(ModelIds.Boat);
                CommandResult r = default;
                for (int i = 0; i < steps.Count; i++)
                {
                    if (System.Array.IndexOf(hinted, i) >= 0) run.Hint();
                    r = run.Fold(steps[i]);
                }
                Assert.AreEqual(new CompletedPiece(ModelIds.Boat, Grade.Star1), r.Piece);
            }
            var s = run.GetSnapshot();
            Assert.AreEqual(3, s.Completions.Get(ModelIds.Boat, Grade.Star1));
            Assert.IsTrue(s.IsUnlocked(ModelIds.Frog));
        }

        [Test]
        public void F004_AC05_PlanesUnlockNothing()
        {
            var run = new OrigamiRun();
            Make(run, ModelIds.Plane, 5);
            var s = run.GetSnapshot();
            Assert.IsFalse(s.IsUnlocked(ModelIds.Frog));
            CollectionAssert.AreEqual(new[] { ModelIds.Plane, ModelIds.Boat }, s.UnlockedModelIds);
        }

        [Test]
        public void F004_AC06_FrogUnlocksCrane_CraneUnlocksDragon()
        {
            var run = new OrigamiRun();
            Make(run, ModelIds.Boat, 3);
            Make(run, ModelIds.Frog, 2);
            EndDay(run);
            Assert.IsFalse(run.GetSnapshot().IsUnlocked(ModelIds.Crane));
            Make(run, ModelIds.Frog);
            Assert.IsTrue(run.GetSnapshot().IsUnlocked(ModelIds.Crane));
            Assert.IsFalse(run.GetSnapshot().IsUnlocked(ModelIds.Dragon));
            Make(run, ModelIds.Crane, 3);
            var s = run.GetSnapshot();
            Assert.IsTrue(s.IsUnlocked(ModelIds.Dragon));
            CollectionAssert.AreEqual(
                new[] { ModelIds.Plane, ModelIds.Boat, ModelIds.Frog, ModelIds.Crane, ModelIds.Dragon }, s.UnlockedModelIds);
            Accept(run.Start(ModelIds.Dragon), CommandOutcome.FoldStarted, "Start(dragon) on day 2");
        }

        [Test]
        public void F004_AC07_NewRunRelocks()
        {
            var run = new OrigamiRun();
            Make(run, ModelIds.Boat, 3);
            Assert.IsTrue(run.GetSnapshot().IsUnlocked(ModelIds.Frog));
            run.NewRun();
            CollectionAssert.AreEqual(new[] { ModelIds.Plane, ModelIds.Boat }, run.GetSnapshot().UnlockedModelIds);
            AssertRejected(run, x => x.Start(ModelIds.Frog), RejectReason.Locked);
        }
    }
}
