using System.Linq;
using NUnit.Framework;
using Origami.Core.Folding;
using static Origami.Tests.Folding.FoldTestUtil;

namespace Origami.Tests.Folding
{
    // C-104 FoldSession — supporting engine tests for F-005 AC-01/02/04/05/10 (owned by ORIGAMI-010 at command level),
    // D-104 replay determinism.
    public class FoldSessionTests
    {
        [Test]
        public void NewSession_IsInitialAndEmpty()
        {
            var s = new FoldSession();
            Assert.AreSame(PaperState.Initial, s.State);
            Assert.AreEqual(0, s.FoldCount);
            Assert.AreEqual(0, s.CreaseCount);
            Assert.AreEqual(0, s.ActionCount);
            Assert.IsFalse(s.CanUndo);
            Assert.AreEqual(0, s.AppliedSteps.Count);
            Assert.IsFalse(s.Undo());
            Assert.IsFalse(s.Unfold());
            Assert.AreSame(PaperState.Initial, s.State);
        }

        [Test]
        public void F005_AC01_AC02_FoldThenUndo()
        {
            var s = new FoldSession();
            var plan = s.Fold(V(-1, 1), V(-1, -1), false, 0);
            Assert.IsTrue(plan.Valid);
            Assert.AreSame(plan.Result, s.State);
            Assert.AreEqual(1, s.FoldCount);
            Assert.That(OutlineArea(s.State), Is.EqualTo(2.0).Within(Eps));
            Assert.That(s.State.TotalArea(), Is.EqualTo(4.0).Within(Eps));
            Assert.IsTrue(s.Undo());
            Assert.AreEqual(0, s.FoldCount);
            Assert.AreSame(PaperState.Initial, s.State);
            Assert.That(OutlineArea(s.State), Is.EqualTo(4.0).Within(Eps));
            Assert.IsFalse(s.Undo(), "second Undo: nothing to undo");
        }

        [Test]
        public void F005_AC03_InvalidFoldLeavesSessionUnchanged()
        {
            var s = new FoldSession();
            Assert.AreEqual(FoldError.DragTooShort, s.Fold(V(-1, 1), V(-1, 0.98), false, 0).Error);
            Assert.AreEqual(FoldError.LineOutsidePaper, s.Fold(V(-1, 1), V(-3, 3), false, 0).Error);
            Assert.AreEqual(FoldError.InvalidLayers, s.Fold(V(-1, 1), V(0, 0), false, -1).Error);
            Assert.AreEqual(FoldError.InvalidLayers, s.Crease(V(-1, 1), V(0, 0), -1).Error);
            Assert.AreEqual(0, s.FoldCount);
            Assert.AreEqual(0, s.CreaseCount);
            Assert.IsFalse(s.CanUndo);
            Assert.AreSame(PaperState.Initial, s.State);
        }

        [Test]
        public void F005_AC04_UndoCancelsOnlyTheFlip()
        {
            var s = new FoldSession();
            s.Fold(V(-1, 1), V(-1, -1), false, 0);
            var beforeFlip = s.State;
            s.Flip();
            Assert.AreEqual(1, s.FoldCount);
            Assert.AreEqual(1, s.ActionCount, "flip is not an action");
            Assert.That(OutlineArea(s.State), Is.EqualTo(2.0).Within(Eps));
            Assert.AreNotSame(beforeFlip, s.State);
            Assert.IsTrue(s.Undo());
            Assert.AreSame(beforeFlip, s.State, "same coordinates as before the flip");
            Assert.AreEqual(1, s.FoldCount);
        }

        [Test]
        public void F005_AC05_UnfoldResetsEverything()
        {
            var s = new FoldSession();
            s.Fold(V(-1, 1), V(-1, -1), false, 0);
            s.Fold(V(-1, 0), V(-0.6, -0.8), false, 0);
            s.Fold(V(1, 0), V(0.6, -0.8), false, 0);
            Assert.IsTrue(s.Crease(V(-1, -1), V(-1, 0), 0).Valid);
            Assert.AreEqual(3, s.FoldCount);
            Assert.AreEqual(1, s.CreaseCount);
            Assert.IsTrue(s.Unfold());
            Assert.AreEqual(0, s.FoldCount);
            Assert.AreEqual(0, s.CreaseCount);
            Assert.AreSame(PaperState.Initial, s.State);
            Assert.AreEqual(1, s.State.Facets.Count);
            Assert.AreEqual(0, s.State.Creases.Count);
            Assert.That(OutlineArea(s.State), Is.EqualTo(4.0).Within(Eps));
            Assert.IsFalse(s.Undo());
            Assert.IsFalse(s.Unfold());

            var flipOnly = new FoldSession();
            flipOnly.Flip();
            Assert.IsTrue(flipOnly.CanUndo);
            Assert.IsTrue(flipOnly.State.Facets[0].FrontUp);
            Assert.IsTrue(flipOnly.Unfold(), "a flip-only sheet can be unfolded (R-007)");
            Assert.IsFalse(flipOnly.State.Facets[0].FrontUp, "white side up again");
            Assert.AreEqual(0, flipOnly.AppliedSteps.Count);
            Assert.IsFalse(flipOnly.Unfold());
        }

        [Test]
        public void F005_AC10_CreaseCountsAndUndo()
        {
            var s = new FoldSession();
            var plan = s.Crease(V(-1, 1), V(1, 1), 0);
            Assert.IsTrue(plan.Valid);
            Assert.AreEqual(0, s.FoldCount);
            Assert.AreEqual(1, s.CreaseCount);
            Assert.AreEqual(1, s.ActionCount);
            Assert.AreEqual(1, s.State.Facets.Count);
            Assert.AreEqual(1, s.State.Creases.Count);
            Assert.That(s.State.Creases[0].Length, Is.EqualTo(2.0).Within(Eps));
            Assert.IsTrue(s.Undo());
            Assert.AreEqual(0, s.State.Creases.Count);
            Assert.AreEqual(0, s.CreaseCount);
        }

        [Test]
        public void CreaseCount_IsOnePerActionEvenWithSeveralSegments()
        {
            var s = new FoldSession();
            s.Fold(V(-1, 1), V(-1, -1), false, 0);
            s.Crease(V(-1, 0), V(1, 0), 0);
            Assert.AreEqual(2, s.State.Creases.Count);
            Assert.AreEqual(1, s.CreaseCount);
            Assert.AreEqual(2, s.ActionCount);
        }

        [Test]
        public void D104_AppliedStepsReplayToCurrentState()
        {
            var s = new FoldSession();
            s.Fold(V(1, 1), C, false, 0);
            s.Crease(V(-1, 1), V(1, -1), 0);
            s.Flip();
            Assert.IsTrue(s.Fold(V(1, 1), V(1.4, 0.0), true, 1).Valid);
            s.Fold(V(-1, 1), V(-1, 0.99), false, 0);   // rejected, not recorded
            var steps = s.AppliedSteps;
            CollectionAssert.AreEqual(new[] { FoldStepKind.Fold, FoldStepKind.Crease, FoldStepKind.Flip, FoldStepKind.Fold },
                steps.Select(x => x.Kind));
            Assert.IsTrue(steps[3].Mountain);
            Assert.AreEqual(1, steps[3].Layers);
            var replay = FoldPlanner.ApplyAll(steps);
            Assert.AreEqual(s.State.Facets.Count, replay.Facets.Count);
            for (int i = 0; i < replay.Facets.Count; i++)
            {
                CollectionAssert.AreEqual(s.State.Facets[i].World, replay.Facets[i].World);
                Assert.AreEqual(s.State.Facets[i].Layer, replay.Facets[i].Layer);
            }
            CollectionAssert.AreEqual(s.State.Creases, replay.Creases);
            // AppliedSteps is a copy
            Assert.IsTrue(s.Undo());
            Assert.AreEqual(4, steps.Count);
            Assert.AreEqual(3, s.AppliedSteps.Count);
        }
    }
}
