using System;
using NUnit.Framework;
using Origami.Core.Catalog;
using Origami.Core.Economy;
using Origami.Core.Recognition;

namespace Origami.Tests.Economy
{
    // C-130 RuleConstants / Pricing — R-015, GAME_RULES 기본 수치, CONTENT_SPECS §5 (F-011 AC-04).
    public class PricingTests
    {
        [Test]
        public void RuleConstants_MatchGameRules()
        {
            Assert.AreEqual(7, RuleConstants.TotalDays);
            Assert.AreEqual(20, RuleConstants.StartMoney);
            Assert.AreEqual(2, RuleConstants.PaperCost);
            Assert.AreEqual(5, RuleConstants.DailySheets);
            Assert.AreEqual(150, RuleConstants.GradePctStar3);
            Assert.AreEqual(100, RuleConstants.GradePctStar2);
            Assert.AreEqual(60, RuleConstants.GradePctStar1);
            Assert.AreEqual(150, RuleConstants.PopularPct);
            Assert.AreEqual(100, RuleConstants.NormalPct);
            Assert.AreEqual(10000, RuleConstants.PriceDivisor);
            Assert.AreEqual(20, RuleConstants.EmptyHandedMoney);
            Assert.AreEqual(12, RuleConstants.TulipGardenCount);
            Assert.AreEqual(600, RuleConstants.TycoonMoney);
        }

        [Test]
        public void R015_GradePct()
        {
            Assert.AreEqual(150, Pricing.GradePct(Grade.Star3));
            Assert.AreEqual(100, Pricing.GradePct(Grade.Star2));
            Assert.AreEqual(60, Pricing.GradePct(Grade.Star1));
            Assert.Throws<ArgumentOutOfRangeException>(() => Pricing.GradePct(Grade.None));
            Assert.Throws<ArgumentOutOfRangeException>(() => Pricing.GradePct((Grade)4));
            Assert.Throws<ArgumentOutOfRangeException>(() => Pricing.GradePct((Grade)(-1)));
        }

        [Test]
        public void R015_Examples()
        {
            Assert.AreEqual(40, Pricing.SalePrice(18, Grade.Star3, true));   // 강아지 ★3 인기
            Assert.AreEqual(26, Pricing.SalePrice(26, Grade.Star2, false));  // 매미 ★2 일반
            Assert.AreEqual(0, Pricing.SalePrice(0, Grade.Star3, true));
            Assert.Throws<ArgumentOutOfRangeException>(() => Pricing.SalePrice(6, Grade.None, false));
            Assert.Throws<ArgumentOutOfRangeException>(() => Pricing.SalePrice(-1, Grade.Star1, false));
        }

        // CONTENT §5 table: ★3/★2/★1 normal, ★3/★2/★1 popular.
        private static readonly (string Id, int[] Prices)[] Table =
        {
            (ModelIds.Plane, new[] { 9, 6, 3, 13, 9, 5 }),
            (ModelIds.House, new[] { 12, 8, 4, 18, 12, 7 }),
            (ModelIds.Tulip, new[] { 18, 12, 7, 27, 18, 10 }),
            (ModelIds.Dog, new[] { 27, 18, 10, 40, 27, 16 }),
            (ModelIds.Cicada, new[] { 39, 26, 15, 58, 39, 23 }),
        };

        private static readonly Grade[] Grades = { Grade.Star3, Grade.Star2, Grade.Star1 };

        [Test]
        public void F011_AC04_PriceTable30Cells_FromBasePrice()
        {
            int cells = 0;
            foreach (var (id, prices) in Table)
            {
                int basePrice = ModelCatalog.Find(id).BasePrice;
                for (int g = 0; g < 3; g++)
                {
                    Assert.AreEqual(prices[g], Pricing.SalePrice(basePrice, Grades[g], false), id + " normal " + Grades[g]);
                    Assert.AreEqual(prices[g + 3], Pricing.SalePrice(basePrice, Grades[g], true), id + " popular " + Grades[g]);
                    cells += 2;
                }
            }
            Assert.AreEqual(30, cells);
        }

        [Test]
        public void F011_AC04_PriceTable_ByModelAndDay()
        {
            for (int day = 1; day <= 7; day++)
            {
                string popular = ModelCatalog.PopularModelId(day);
                foreach (var (id, prices) in Table)
                {
                    bool isPopular = id == popular;
                    for (int g = 0; g < 3; g++)
                        Assert.AreEqual(prices[g + (isPopular ? 3 : 0)], Pricing.SalePrice(id, Grades[g], day), id + " day " + day);
                }
            }
        }

        [Test]
        public void F011_AC04_PopularByDay()
        {
            CollectionAssert.AreEqual(new[] { "plane", "house", "tulip", "house", "dog", "tulip", "cicada" }, ModelCatalog.PopularByDay);
        }

        [Test]
        public void SalePriceById_InvalidArguments()
        {
            Assert.Throws<ArgumentException>(() => Pricing.SalePrice("unicorn", Grade.Star3, 1));
            Assert.Throws<ArgumentException>(() => Pricing.SalePrice((string)null, Grade.Star3, 1));
            Assert.Throws<ArgumentException>(() => Pricing.SalePrice("", Grade.Star3, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => Pricing.SalePrice(ModelIds.Plane, Grade.Star3, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => Pricing.SalePrice(ModelIds.Plane, Grade.Star3, 8));
            Assert.Throws<ArgumentOutOfRangeException>(() => Pricing.SalePrice(ModelIds.Plane, Grade.None, 1));
        }
    }
}
