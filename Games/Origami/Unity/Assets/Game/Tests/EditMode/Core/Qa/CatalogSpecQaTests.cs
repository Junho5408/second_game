using System.Linq;
using NUnit.Framework;
using Origami.Core.Content;
using Origami.Core.Rules;

namespace Origami.Tests.Qa
{
    // QA ORIGAMI-002: independent checks of the code catalog against CONTENT_SPECS.md.
    // - Pins the ending result texts (review ORIGAMI-002 W-3). If the content-designer revises a text,
    //   CONTENT_SPECS.md, OrigamiCatalog.cs and this test change together (DATA_STRUCTURE.md "콘텐츠 정의 방식").
    // - Recomputes the CONTENT_SPECS price table (30 values) from catalog base prices and RuleConstants
    //   only (no Pricing call), so a catalog/constant drift is caught before ORIGAMI-003 exists.
    // - Pins the catalog invariants that C-010's bankruptcy check relies on.
    public class CatalogSpecQaTests
    {
        [TestCase("E-01", "종이는 다 떨어졌지만, 종이접기는 취미로 남았다.")]
        [TestCase("E-02", "가게 천장을 가득 메운 학들. 작은 소원 하나가 이루어졌다.")]
        [TestCase("E-03", "종이 한 장으로 시작해 거리의 명물 가게가 되었다.")]
        [TestCase("E-04", "어떤 작품이든 도안 없이 완벽하게. 사람들은 당신을 명인이라 부른다.")]
        [TestCase("E-05", "소박하지만 단골이 생긴 동네 가게. 다음 주도 문을 연다.")]
        public void EndingText_MatchesContentSpecs(string id, string text)
        {
            var e = OrigamiCatalog.FindEnding(id);
            Assert.IsNotNull(e, id);
            Assert.AreEqual(text, e.Text, id);
        }

        // Columns: ★3 normal, ★2 normal, ★1 normal, ★3 popular, ★2 popular, ★1 popular (CONTENT_SPECS "판매가 표").
        [TestCase("plane", 6, 4, 2, 9, 6, 3)]
        [TestCase("boat", 9, 6, 3, 13, 9, 5)]
        [TestCase("frog", 15, 10, 6, 22, 15, 9)]
        [TestCase("crane", 27, 18, 10, 40, 27, 16)]
        [TestCase("dragon", 60, 40, 24, 90, 60, 36)]
        public void PriceTable_FollowsFromCatalogAndConstants(string id, int n3, int n2, int n1, int p3, int p2, int p1)
        {
            int b = OrigamiCatalog.FindModel(id).BasePrice;
            int Price(int gradePct, int demandPct) => b * gradePct * demandPct / RuleConstants.PriceDivisor;
            Assert.AreEqual(n3, Price(RuleConstants.GradePctStar3, RuleConstants.NormalPct), "★3 normal");
            Assert.AreEqual(n2, Price(RuleConstants.GradePctStar2, RuleConstants.NormalPct), "★2 normal");
            Assert.AreEqual(n1, Price(RuleConstants.GradePctStar1, RuleConstants.NormalPct), "★1 normal");
            Assert.AreEqual(p3, Price(RuleConstants.GradePctStar3, RuleConstants.PopularPct), "★3 popular");
            Assert.AreEqual(p2, Price(RuleConstants.GradePctStar2, RuleConstants.PopularPct), "★2 popular");
            Assert.AreEqual(p1, Price(RuleConstants.GradePctStar1, RuleConstants.PopularPct), "★1 popular");
        }

        [Test]
        public void Catalog_IdsUniqueAndPopularTableUsesCatalogIds()
        {
            var ids = OrigamiCatalog.Models.Select(m => m.Id).ToArray();
            Assert.AreEqual(ids.Length, ids.Distinct().Count());
            Assert.AreEqual(RuleConstants.TotalDays, OrigamiCatalog.PopularByDay.Count);
            Assert.IsTrue(OrigamiCatalog.PopularByDay.All(p => OrigamiCatalog.FindModel(p) != null));
            var endingIds = OrigamiCatalog.Endings.Select(e => e.Id).ToArray();
            Assert.AreEqual(endingIds.Length, endingIds.Distinct().Count());
        }

        [Test]
        public void UnlockChain_PrerequisitesAreEarlierCatalogModels()
        {
            foreach (var m in OrigamiCatalog.Models.Where(m => !m.IsInitiallyUnlocked))
            {
                var pre = OrigamiCatalog.FindModel(m.UnlockPrerequisiteId);
                Assert.IsNotNull(pre, m.Id);
                Assert.Less(pre.Order, m.Order, m.Id);
            }
        }

        [Test]
        public void BankruptcyEquivalence_CheapestInitialPaperIsOneAndAllCostsPositive()
        {
            // C-010 checks "money < cheapest unlocked paper"; R-013 says "money 0". Equivalent only while an
            // initially unlocked model costs 1 and no paper costs less than 1.
            Assert.IsTrue(OrigamiCatalog.Models.All(m => m.PaperCost >= 1 && m.BasePrice >= 1));
            Assert.AreEqual(1, OrigamiCatalog.Models.Where(m => m.IsInitiallyUnlocked).Min(m => m.PaperCost));
        }

        [Test]
        public void StepSequences_UseOnlyValidActions()
        {
            Assert.IsTrue(OrigamiCatalog.Models.All(m => m.Steps.Count > 0 && m.Steps.All(FoldActions.IsValid)));
        }
    }
}
