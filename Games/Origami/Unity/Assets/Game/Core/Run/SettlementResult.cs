using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Origami.Core.Rules;

namespace Origami.Core.Run
{
    /// <summary>Result of one accepted EndDay (F-005, R-012). Immutable.</summary>
    public sealed class SettlementResult
    {
        public SettlementResult(int settledDay, IList<SaleLine> lines, int total, int moneyAfter,
            RunEndReason endReason, string nextPopularModelId)
        {
            if (lines == null) throw new ArgumentNullException(nameof(lines));
            SettledDay = settledDay;
            Lines = new ReadOnlyCollection<SaleLine>(new List<SaleLine>(lines));
            Total = total;
            MoneyAfter = moneyAfter;
            EndReason = endReason;
            NextPopularModelId = nextPopularModelId;
        }

        /// <summary>The day that was settled (1..7).</summary>
        public int SettledDay { get; }

        /// <summary>Sold pieces in completion order (today's list order).</summary>
        public IReadOnlyList<SaleLine> Lines { get; }

        /// <summary>Sum of line prices (0 when nothing was sold).</summary>
        public int Total { get; }

        /// <summary>Money after adding <see cref="Total"/>.</summary>
        public int MoneyAfter { get; }

        /// <summary>None if the run continues; Completed (day 7) or Bankrupt (new day with money 0).</summary>
        public RunEndReason EndReason { get; }

        public bool RunEnded => EndReason != RunEndReason.None;

        /// <summary>Popular model of the next day; null when the run ended (day 7 or bankrupt, F-007 settlement screen).</summary>
        public string NextPopularModelId { get; }
    }
}
