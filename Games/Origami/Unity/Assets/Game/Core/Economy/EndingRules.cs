using System;
using Origami.Core.Catalog;
using Origami.Core.Recognition;

namespace Origami.Core.Economy
{
    /// <summary>R-018 ending evaluation (C-130). Pure function of (end reason, final money, completion counts).</summary>
    public static class EndingRules
    {
        /// <summary>
        /// First match in priority order: E-01 (Bankrupt or money ≤ 20) → E-02 (tulip total ≥ 12, any grade) →
        /// E-03 (every catalogue model ★3 ≥ 1) → E-04 (money ≥ 600) → E-05. Returns an <see cref="EndingIds"/> value.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">reason is <see cref="RunEndReason.None"/> or undefined.</exception>
        /// <exception cref="ArgumentNullException">counts is null.</exception>
        public static string Evaluate(RunEndReason reason, int finalMoney, CompletionCounts counts)
        {
            if (reason != RunEndReason.Completed && reason != RunEndReason.Bankrupt)
                throw new ArgumentOutOfRangeException(nameof(reason), reason, "the run has not ended");
            if (counts == null) throw new ArgumentNullException(nameof(counts));

            if (reason == RunEndReason.Bankrupt || finalMoney <= RuleConstants.EmptyHandedMoney) return EndingIds.EmptyHanded;
            if (counts.Total(ModelIds.Tulip) >= RuleConstants.TulipGardenCount) return EndingIds.TulipGarden;

            bool master = true;
            foreach (var m in ModelCatalog.Models)
            {
                if (counts.Get(m.Id, Grade.Star3) < 1) { master = false; break; }
            }
            if (master) return EndingIds.Master;

            if (finalMoney >= RuleConstants.TycoonMoney) return EndingIds.Tycoon;
            return EndingIds.LocalWorkshop;
        }
    }
}
