using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Origami.Core.Rules;

namespace Origami.Core.Run
{
    /// <summary>Read-only copy of the whole run state (F-001 AC-01 fields). Immutable; safe to keep.</summary>
    public sealed class RunSnapshot
    {
        public RunSnapshot(int day, int money, int attemptsLeft, string popularModelId, IList<string> unlockedModelIds,
            CompletionCounts completions, int failureCount, int totalSales, int totalPaperCost,
            IList<CompletedPiece> todayPieces, FoldSnapshot fold, RunEndReason endReason, string endingId)
        {
            if (unlockedModelIds == null) throw new ArgumentNullException(nameof(unlockedModelIds));
            if (todayPieces == null) throw new ArgumentNullException(nameof(todayPieces));
            Day = day;
            Money = money;
            AttemptsLeft = attemptsLeft;
            PopularModelId = popularModelId;
            UnlockedModelIds = new ReadOnlyCollection<string>(new List<string>(unlockedModelIds));
            Completions = completions ?? throw new ArgumentNullException(nameof(completions));
            FailureCount = failureCount;
            TotalSales = totalSales;
            TotalPaperCost = totalPaperCost;
            TodayPieces = new ReadOnlyCollection<CompletedPiece>(new List<CompletedPiece>(todayPieces));
            Fold = fold;
            EndReason = endReason;
            EndingId = endingId;
        }

        /// <summary>Current day 1..7 (stays 7 after completion; the new day after bankruptcy).</summary>
        public int Day { get; }

        public int Money { get; }

        /// <summary>Attempts left today (0..5).</summary>
        public int AttemptsLeft { get; }

        /// <summary>Today's popular model ID (catalog table).</summary>
        public string PopularModelId { get; }

        /// <summary>Unlocked model IDs in catalog order.</summary>
        public IReadOnlyList<string> UnlockedModelIds { get; }

        /// <summary>Completions per model and grade in this run.</summary>
        public CompletionCounts Completions { get; }

        /// <summary>Ruined + abandoned folds in this run.</summary>
        public int FailureCount { get; }

        /// <summary>Sum of all settlement totals in this run.</summary>
        public int TotalSales { get; }

        /// <summary>Sum of all paper costs paid in this run.</summary>
        public int TotalPaperCost { get; }

        /// <summary>Pieces completed today and not yet sold, in completion order.</summary>
        public IReadOnlyList<CompletedPiece> TodayPieces { get; }

        /// <summary>The fold in progress, or null.</summary>
        public FoldSnapshot Fold { get; }

        public bool IsFolding => Fold != null;

        /// <summary>None while the run is in progress.</summary>
        public RunEndReason EndReason { get; }

        public bool IsRunEnded => EndReason != RunEndReason.None;

        /// <summary>Ending ID (<c>EndingIds</c>) once the run ended, else null.</summary>
        public string EndingId { get; }

        /// <summary>True if <paramref name="modelId"/> is in <see cref="UnlockedModelIds"/> (ordinal).</summary>
        public bool IsUnlocked(string modelId)
        {
            if (modelId == null) return false;
            for (int i = 0; i < UnlockedModelIds.Count; i++)
            {
                if (string.Equals(UnlockedModelIds[i], modelId, StringComparison.Ordinal)) return true;
            }
            return false;
        }
    }
}
