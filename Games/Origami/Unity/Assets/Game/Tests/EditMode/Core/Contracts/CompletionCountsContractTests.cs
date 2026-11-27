using System;
using NUnit.Framework;
using Origami.Core.Content;
using Origami.Core.Rules;

namespace Origami.Tests.Contracts
{
    // ORIGAMI-002: CompletionCounts (C-005) is a finished immutable data type.
    public class CompletionCountsContractTests
    {
        [Test]
        public void Empty_IsAllZero()
        {
            foreach (var m in OrigamiCatalog.Models)
            {
                Assert.AreEqual(0, CompletionCounts.Empty.Total(m.Id));
                Assert.AreEqual(0, CompletionCounts.Empty.Get(m.Id, Grade.Star3));
            }
        }

        [Test]
        public void With_IsImmutableAndPerGrade()
        {
            var a = CompletionCounts.Empty.With("crane", Grade.Star1, 15);
            Assert.AreEqual(0, CompletionCounts.Empty.Get("crane", Grade.Star1), "Empty unchanged");
            Assert.AreEqual(15, a.Get("crane", Grade.Star1));
            Assert.AreEqual(0, a.Get("crane", Grade.Star3));
            Assert.AreEqual(15, a.Total("crane"));
            var b = a.Increment("crane", Grade.Star3).Increment("crane", Grade.Star3);
            Assert.AreEqual(2, b.Get("crane", Grade.Star3));
            Assert.AreEqual(17, b.Total("crane"));
            Assert.AreEqual(15, a.Total("crane"));
        }

        [Test]
        public void Get_InvalidInputs_ReturnZero()
        {
            var a = CompletionCounts.Empty.With("boat", Grade.Star2, 3);
            Assert.AreEqual(0, a.Get(null, Grade.Star2));
            Assert.AreEqual(0, a.Get("unicorn", Grade.Star2));
            Assert.AreEqual(0, a.Get("boat", default(Grade)));
            Assert.AreEqual(0, a.Total(null));
        }

        [Test]
        public void With_InvalidInputs_Throw()
        {
            Assert.Throws<ArgumentException>(() => CompletionCounts.Empty.With("unicorn", Grade.Star1, 1));
            Assert.Throws<ArgumentException>(() => CompletionCounts.Empty.With(null, Grade.Star1, 1));
            Assert.Throws<ArgumentException>(() => CompletionCounts.Empty.With("boat", (Grade)4, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => CompletionCounts.Empty.With("boat", Grade.Star1, -1));
        }

        [Test]
        public void Equality_IsByValue()
        {
            var a = CompletionCounts.Empty.With("frog", Grade.Star2, 2);
            var b = CompletionCounts.Empty.Increment("frog", Grade.Star2).Increment("frog", Grade.Star2);
            Assert.AreEqual(a, b);
            Assert.AreEqual(a.GetHashCode(), b.GetHashCode());
            Assert.AreNotEqual(a, CompletionCounts.Empty);
            Assert.AreNotEqual(a, CompletionCounts.Empty.With("frog", Grade.Star3, 2));
            Assert.IsFalse(a.Equals(null));
        }
    }
}
