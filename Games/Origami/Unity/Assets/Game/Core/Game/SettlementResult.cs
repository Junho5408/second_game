using System.Collections.Generic;
using System.Collections.ObjectModel;
using Origami.Core.Economy;

namespace Origami.Core.Game
{
    /// <summary>Evening settlement (C-151, D-123, R-016). Immutable.</summary>
    public sealed class SettlementResult
    {
        internal SettlementResult(int settledDay, IList<SaleLine> lines, int total, int moneyAfter, RunEndReason endReason, string nextPopularModelId)
        {
            SettledDay = settledDay;
            Lines = new ReadOnlyCollection<SaleLine>(new List<SaleLine>(lines));
            Total = total;
            MoneyAfter = moneyAfter;
            EndReason = endReason;
            NextPopularModelId = nextPopularModelId;
        }

        /// <summary>The day that was settled (1..7).</summary>
        public int SettledDay { get; }

        /// <summary>Sold pieces in display order (empty when nothing was displayed).</summary>
        public IReadOnlyList<SaleLine> Lines { get; }

        /// <summary>Σ prices.</summary>
        public int Total { get; }

        /// <summary>Money after adding <see cref="Total"/>.</summary>
        public int MoneyAfter { get; }

        /// <summary>None when the run continues, Completed after day 7, Bankrupt when the new day starts with money &lt; 2.</summary>
        public RunEndReason EndReason { get; }

        /// <summary>Popular model of the next day; null when the run ended.</summary>
        public string NextPopularModelId { get; }
    }
}
