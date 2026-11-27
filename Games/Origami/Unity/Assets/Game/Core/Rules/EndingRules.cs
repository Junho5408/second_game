using System;
using Origami.Core.Content;

namespace Origami.Core.Rules
{
    /// <summary>R-014 ending evaluation. Uses only (end reason, final money, completion counts) — F-006.</summary>
    public static class EndingRules
    {
        /// <summary>
        /// Returns the ID (<c>EndingIds</c>) of the first satisfied ending in priority order:
        /// E-01 (Bankrupt or money ≤ 20) → E-02 (crane total ≥ 15) → E-03 (money ≥ 1000)
        /// → E-04 (every catalog model has ★3 ≥ 1) → E-05.
        /// <paramref name="reason"/> must be Completed or Bankrupt (else ArgumentOutOfRangeException);
        /// <paramref name="counts"/> null → ArgumentNullException.
        /// </summary>
        public static string Evaluate(RunEndReason reason, int finalMoney, CompletionCounts counts)
        {
            if (reason != RunEndReason.Completed && reason != RunEndReason.Bankrupt)
                throw new ArgumentOutOfRangeException(nameof(reason));
            if (counts == null) throw new ArgumentNullException(nameof(counts));

            if (reason == RunEndReason.Bankrupt || finalMoney <= RuleConstants.EmptyHandedMoney) return EndingIds.EmptyHanded;
            if (counts.Total(ModelIds.Crane) >= RuleConstants.CraneCount) return EndingIds.CraneWish;
            if (finalMoney >= RuleConstants.TycoonMoney) return EndingIds.Tycoon;
            if (AllModelsHaveStar3(counts)) return EndingIds.Master;
            return EndingIds.LocalShop;
        }

        private static bool AllModelsHaveStar3(CompletionCounts counts)
        {
            for (int i = 0; i < OrigamiCatalog.Models.Count; i++)
            {
                if (counts.Get(OrigamiCatalog.Models[i].Id, Grade.Star3) < 1) return false;
            }
            return true;
        }
    }
}
