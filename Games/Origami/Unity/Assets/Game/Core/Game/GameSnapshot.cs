using System.Collections.Generic;
using Origami.Core.Economy;

namespace Origami.Core.Game
{
    /// <summary>
    /// Immutable view of the whole game state (C-151, D-120). A new object is made for every accepted command;
    /// a rejected command leaves the same instance in <see cref="OrigamiGame.Snapshot"/>.
    /// </summary>
    public sealed class GameSnapshot
    {
        internal GameSnapshot(int day, int money, int sheetsLeft, WorkshopMode mode, bool bookOpen, int bookPage,
            IReadOnlyList<string> ownedModelIds, bool freeSheetAvailable, bool tutorialFinished, bool hasSat,
            SheetSnapshot sheet, IReadOnlyList<DisplayedPiece> display, CompletionCounts completions, int failureCount,
            int totalSales, int totalPaperCost, int totalBookCost, string popularModelId, RunEndReason endReason, string endingId)
        {
            Day = day;
            Money = money;
            SheetsLeft = sheetsLeft;
            Mode = mode;
            BookOpen = bookOpen;
            BookPage = bookPage;
            OwnedModelIds = ownedModelIds;
            FreeSheetAvailable = freeSheetAvailable;
            TutorialFinished = tutorialFinished;
            HasSat = hasSat;
            Sheet = sheet;
            Display = display;
            Completions = completions;
            FailureCount = failureCount;
            TotalSales = totalSales;
            TotalPaperCost = totalPaperCost;
            TotalBookCost = totalBookCost;
            PopularModelId = popularModelId;
            EndReason = endReason;
            EndingId = endingId;
        }

        /// <summary>1..7.</summary>
        public int Day { get; }

        /// <summary>≥ 0; = 20 + TotalSales − TotalPaperCost − TotalBookCost.</summary>
        public int Money { get; }

        /// <summary>0..5.</summary>
        public int SheetsLeft { get; }

        public WorkshopMode Mode { get; }

        /// <summary>Seated "book open" flag (false in every other mode).</summary>
        public bool BookOpen { get; }

        /// <summary>Bookshelf, or Seated with the book open.</summary>
        public bool BookVisible => Mode == WorkshopMode.Bookshelf || (Mode == WorkshopMode.Seated && BookOpen);

        /// <summary>Current (left, even) page; kept while the book is closed, 0 after NewRun.</summary>
        public int BookPage { get; }

        /// <summary>Owned pages in catalogue order.</summary>
        public IReadOnlyList<string> OwnedModelIds { get; }

        /// <summary>The run's first sheet (free tutorial sheet) has not been taken yet.</summary>
        public bool FreeSheetAvailable { get; }

        /// <summary>The tutorial sheet was finished or discarded.</summary>
        public bool TutorialFinished { get; }

        /// <summary>Interact(table) was accepted at least once in this run (F-013 walk hint).</summary>
        public bool HasSat { get; }

        /// <summary>Sheet on the table, or null.</summary>
        public SheetSnapshot Sheet { get; }

        /// <summary>Today's display in completion order.</summary>
        public IReadOnlyList<DisplayedPiece> Display { get; }

        public CompletionCounts Completions { get; }

        /// <summary>Crumpled + discarded sheets.</summary>
        public int FailureCount { get; }

        public int TotalSales { get; }
        public int TotalPaperCost { get; }
        public int TotalBookCost { get; }

        /// <summary>Today's popular model ID.</summary>
        public string PopularModelId { get; }

        /// <summary>None while the run is in progress.</summary>
        public RunEndReason EndReason { get; }

        /// <summary>EndingIds value once ended, else null.</summary>
        public string EndingId { get; }

        public bool IsEnded => EndReason != RunEndReason.None;
    }
}
