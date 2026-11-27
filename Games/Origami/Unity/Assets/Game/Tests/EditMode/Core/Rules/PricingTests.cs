using System;
using NUnit.Framework;
using Origami.Core.Content;
using Origami.Core.Rules;

namespace Origami.Tests.Rules
{
    // ORIGAMI-003: C-007 Pricing (R-011, F-005 AC-04: all 30 values of the CONTENT_SPECS price table).
    public class PricingTests
    {
        [Test]
        public void R011_DemandPct()
        {
            Assert.AreEqual(150, Pricing.DemandPct(true));
            Assert.AreEqual(100, Pricing.DemandPct(false));
        }

        // 30 values: 5 models × 3 grades × (normal, popular).
        [TestCase("plane", Grade.Star3, false, 6)]
        [TestCase("plane", Grade.Star2, false, 4)]
        [TestCase("plane", Grade.Star1, false, 2)]
        [TestCase("plane", Grade.Star3, true, 9)]
        [TestCase("plane", Grade.Star2, true, 6)]
        [TestCase("plane", Grade.Star1, true, 3)]
        [TestCase("boat", Grade.Star3, false, 9)]
        [TestCase("boat", Grade.Star2, false, 6)]
        [TestCase("boat", Grade.Star1, false, 3)]
        [TestCase("boat", Grade.Star3, true, 13)]
        [TestCase("boat", Grade.Star2, true, 9)]
        [TestCase("boat", Grade.Star1, true, 5)]
        [TestCase("frog", Grade.Star3, false, 15)]
        [TestCase("frog", Grade.Star2, false, 10)]
        [TestCase("frog", Grade.Star1, false, 6)]
        [TestCase("frog", Grade.Star3, true, 22)]
        [TestCase("frog", Grade.Star2, true, 15)]
        [TestCase("frog", Grade.Star1, true, 9)]
        [TestCase("crane", Grade.Star3, false, 27)]
        [TestCase("crane", Grade.Star2, false, 18)]
        [TestCase("crane", Grade.Star1, false, 10)]
        [TestCase("crane", Grade.Star3, true, 40)]
        [TestCase("crane", Grade.Star2, true, 27)]
        [TestCase("crane", Grade.Star1, true, 16)]
        [TestCase("dragon", Grade.Star3, false, 60)]
        [TestCase("dragon", Grade.Star2, false, 40)]
        [TestCase("dragon", Grade.Star1, false, 24)]
        [TestCase("dragon", Grade.Star3, true, 90)]
        [TestCase("dragon", Grade.Star2, true, 60)]
        [TestCase("dragon", Grade.Star1, true, 36)]
        public void F005_AC04_SalePriceTable(string modelId, Grade grade, bool popular, int expected)
        {
            int basePrice = OrigamiCatalog.FindModel(modelId).BasePrice;
            Assert.AreEqual(expected, Pricing.SalePrice(basePrice, grade, popular), "by base price");
            Assert.AreEqual(expected, Pricing.SalePrice(modelId, grade, DayWhere(modelId, popular)), "by model and day");
        }

        // A day on which modelId is (popular = true) or is not (false) the popular model.
        private static int DayWhere(string modelId, bool popular)
        {
            for (int day = 1; day <= RuleConstants.TotalDays; day++)
            {
                bool isPopular = OrigamiCatalog.PopularModelId(day) == modelId;
                if (isPopular == popular) return day;
            }
            throw new InvalidOperationException("no such day for " + modelId);
        }

        [Test]
        public void R011_Examples_CraneStar3()
        {
            Assert.AreEqual(27, Pricing.SalePrice(ModelIds.Crane, Grade.Star3, 1));
            Assert.AreEqual(40, Pricing.SalePrice(ModelIds.Crane, Grade.Star3, 5));
        }

        [Test]
        public void R011_PopularityFollowsDayTable()
        {
            // Day 2 and 4: boat popular; day 1: plane popular.
            Assert.AreEqual(13, Pricing.SalePrice(ModelIds.Boat, Grade.Star3, 2));
            Assert.AreEqual(13, Pricing.SalePrice(ModelIds.Boat, Grade.Star3, 4));
            Assert.AreEqual(9, Pricing.SalePrice(ModelIds.Boat, Grade.Star3, 1));
            Assert.AreEqual(9, Pricing.SalePrice(ModelIds.Plane, Grade.Star3, 1));
            // Dragon popular on day 7 even though nothing guarantees it is unlocked (R-011 exception).
            Assert.AreEqual(90, Pricing.SalePrice(ModelIds.Dragon, Grade.Star3, 7));
        }

        [Test]
        public void C007_SalePrice_InvalidArguments_Throw()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => Pricing.SalePrice(10, (Grade)0, false));
            Assert.Throws<ArgumentException>(() => Pricing.SalePrice("unicorn", Grade.Star3, 1));
            Assert.Throws<ArgumentException>(() => Pricing.SalePrice((string)null, Grade.Star3, 1));
            Assert.Throws<ArgumentException>(() => Pricing.SalePrice("", Grade.Star3, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => Pricing.SalePrice(ModelIds.Plane, Grade.Star3, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => Pricing.SalePrice(ModelIds.Plane, Grade.Star3, 8));
            Assert.Throws<ArgumentOutOfRangeException>(() => Pricing.SalePrice(ModelIds.Plane, (Grade)0, 1));
        }
    }
}
