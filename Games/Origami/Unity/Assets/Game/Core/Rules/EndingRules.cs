using System;

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
            throw new NotImplementedException();
        }
    }
}
