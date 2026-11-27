using System;
using NUnit.Framework;
using Origami.Core.Catalog;
using Origami.Core.Economy;
using Origami.Core.Recognition;

namespace Origami.Tests.Economy
{
    // C-130 CompletionCounts (v1 structure, v2 catalogue 5 × ★1~★3).
    public class CompletionCountsTests
    {
        [Test]
        public void Empty_AllZero()
        {
            foreach (var m in ModelCatalog.Models)
            {
                foreach (Grade g in new[] { Grade.Star1, Grade.Star2, Grade.Star3 }) Assert.AreEqual(0, CompletionCounts.Empty.Get(m.Id, g));
                Assert.AreEqual(0, CompletionCounts.Empty.Total(m.Id));
            }
        }

        [Test]
        public void Get_UnknownOrNone_IsZero()
        {
            var c = CompletionCounts.Empty.With(ModelIds.Tulip, Grade.Star2, 3);
            Assert.AreEqual(0, c.Get("unicorn", Grade.Star2));
            Assert.AreEqual(0, c.Get(null, Grade.Star2));
            Assert.AreEqual(0, c.Get(ModelIds.Tulip, Grade.None));
            Assert.AreEqual(0, c.Get(ModelIds.Tulip, (Grade)9));
            Assert.AreEqual(0, c.Total(null));
            Assert.AreEqual(0, c.Total("unicorn"));
        }

        [Test]
        public void With_Increment_Total_AreImmutable()
        {
            var a = CompletionCounts.Empty.With(ModelIds.Tulip, Grade.Star1, 2);
            var b = a.Increment(ModelIds.Tulip, Grade.Star3).Increment(ModelIds.Dog, Grade.Star2);
            Assert.AreEqual(0, CompletionCounts.Empty.Get(ModelIds.Tulip, Grade.Star1));
            Assert.AreEqual(2, a.Get(ModelIds.Tulip, Grade.Star1));
            Assert.AreEqual(0, a.Get(ModelIds.Tulip, Grade.Star3));
            Assert.AreEqual(1, b.Get(ModelIds.Tulip, Grade.Star3));
            Assert.AreEqual(3, b.Total(ModelIds.Tulip));
            Assert.AreEqual(1, b.Total(ModelIds.Dog));
            Assert.AreEqual(0, b.Total(ModelIds.Cicada));
            Assert.AreEqual(5, b.With(ModelIds.Cicada, Grade.Star3, 5).Get(ModelIds.Cicada, Grade.Star3));
            Assert.AreEqual(0, b.With(ModelIds.Dog, Grade.Star2, 0).Get(ModelIds.Dog, Grade.Star2));
        }

        [Test]
        public void With_InvalidArguments()
        {
            Assert.Throws<ArgumentException>(() => CompletionCounts.Empty.With("unicorn", Grade.Star1, 1));
            Assert.Throws<ArgumentException>(() => CompletionCounts.Empty.With(null, Grade.Star1, 1));
            Assert.Throws<ArgumentException>(() => CompletionCounts.Empty.With(ModelIds.Plane, Grade.None, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => CompletionCounts.Empty.With(ModelIds.Plane, Grade.Star1, -1));
            Assert.Throws<ArgumentException>(() => CompletionCounts.Empty.Increment("unicorn", Grade.Star1));
            Assert.Throws<ArgumentException>(() => CompletionCounts.Empty.Increment(ModelIds.Plane, Grade.None));
        }

        [Test]
        public void Equality_IsByValue()
        {
            var a = CompletionCounts.Empty.Increment(ModelIds.House, Grade.Star3);
            var b = CompletionCounts.Empty.With(ModelIds.House, Grade.Star3, 1);
            Assert.AreNotSame(a, b);
            Assert.IsTrue(a.Equals(b));
            Assert.IsTrue(a.Equals((object)b));
            Assert.AreEqual(a.GetHashCode(), b.GetHashCode());
            Assert.IsFalse(a.Equals(CompletionCounts.Empty));
            Assert.IsFalse(a.Equals(null));
            Assert.IsFalse(a.Equals((object)"x"));
            Assert.IsTrue(CompletionCounts.Empty.Equals(CompletionCounts.Empty.With(ModelIds.Plane, Grade.Star1, 0)));
        }
    }
}
