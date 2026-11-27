using System;
using NUnit.Framework;
using Origami.Core.Content;
using Origami.Core.Rules;

namespace Origami.Tests.Rules
{
    // ORIGAMI-003: C-008 Unlocking (R-010, F-004).
    public class UnlockingTests
    {
        private static readonly CompletionCounts Empty = CompletionCounts.Empty;

        [Test]
        public void R010_InitialModels_AreUnlocked()
        {
            Assert.IsTrue(Unlocking.IsUnlocked(ModelIds.Plane, Empty));
            Assert.IsTrue(Unlocking.IsUnlocked(ModelIds.Boat, Empty));
            Assert.IsFalse(Unlocking.IsUnlocked(ModelIds.Frog, Empty));
            Assert.IsFalse(Unlocking.IsUnlocked(ModelIds.Crane, Empty));
            Assert.IsFalse(Unlocking.IsUnlocked(ModelIds.Dragon, Empty));
        }

        [Test]
        public void R010_ThreeCompletionsOfPrerequisite_AnyGrade_Unlock()
        {
            var two = Empty.With(ModelIds.Boat, Grade.Star3, 1).With(ModelIds.Boat, Grade.Star1, 1);
            Assert.IsFalse(Unlocking.IsUnlocked(ModelIds.Frog, two));
            var three = two.With(ModelIds.Boat, Grade.Star2, 1);
            Assert.IsTrue(Unlocking.IsUnlocked(ModelIds.Frog, three));
            Assert.IsTrue(Unlocking.IsUnlocked(ModelIds.Frog, Empty.With(ModelIds.Boat, Grade.Star1, 3)), "F-004 AC-04 ★1 counts");
            Assert.IsFalse(Unlocking.IsUnlocked(ModelIds.Crane, three), "chain needs frog");
        }

        [Test]
        public void R010_Chain_FrogCraneDragon()
        {
            var c = Empty.With(ModelIds.Frog, Grade.Star3, 3);
            Assert.IsTrue(Unlocking.IsUnlocked(ModelIds.Crane, c));
            Assert.IsFalse(Unlocking.IsUnlocked(ModelIds.Dragon, c));
            c = c.With(ModelIds.Crane, Grade.Star2, 3);
            Assert.IsTrue(Unlocking.IsUnlocked(ModelIds.Dragon, c));
        }

        [Test]
        public void F004_AC05_PlaneDoesNotUnlockAnything()
        {
            var c = Empty.With(ModelIds.Plane, Grade.Star3, 5);
            Assert.IsFalse(Unlocking.IsUnlocked(ModelIds.Frog, c));
            Assert.IsFalse(Unlocking.IsUnlocked(ModelIds.Crane, c));
            Assert.IsFalse(Unlocking.IsUnlocked(ModelIds.Dragon, c));
        }

        [Test]
        public void C008_UnknownIds_AreNotUnlocked()
        {
            Assert.IsFalse(Unlocking.IsUnlocked(null, Empty));
            Assert.IsFalse(Unlocking.IsUnlocked("", Empty));
            Assert.IsFalse(Unlocking.IsUnlocked("unicorn", Empty));
            Assert.IsFalse(Unlocking.IsUnlocked("Plane", Empty), "ordinal, case-sensitive");
        }

        [Test]
        public void C008_Progress()
        {
            Assert.AreEqual(0, Unlocking.Progress(ModelIds.Frog, Empty));
            Assert.AreEqual(1, Unlocking.Progress(ModelIds.Frog, Empty.With(ModelIds.Boat, Grade.Star1, 1)));
            Assert.AreEqual(3, Unlocking.Progress(ModelIds.Frog, Empty.With(ModelIds.Boat, Grade.Star2, 7)), "capped");
            Assert.AreEqual(2, Unlocking.Progress(ModelIds.Crane, Empty.With(ModelIds.Frog, Grade.Star3, 2)));
            Assert.AreEqual(3, Unlocking.Progress(ModelIds.Plane, Empty));
            Assert.AreEqual(3, Unlocking.Progress(ModelIds.Boat, Empty));
            Assert.AreEqual(0, Unlocking.Progress(null, Empty));
            Assert.AreEqual(0, Unlocking.Progress("unicorn", Empty));
        }

        [Test]
        public void C008_NullCounts_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => Unlocking.IsUnlocked(ModelIds.Frog, null));
            Assert.Throws<ArgumentNullException>(() => Unlocking.IsUnlocked(ModelIds.Plane, null));
            Assert.Throws<ArgumentNullException>(() => Unlocking.Progress(ModelIds.Frog, null));
            Assert.Throws<ArgumentNullException>(() => Unlocking.Progress(null, null));
        }
    }
}
