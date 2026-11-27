using Origami.Core.Folding;

namespace Origami.Core.Screens
{
    /// <summary>Latest change of the sheet on the table (C-171); the Runtime animates it. A new change has a larger <see cref="Serial"/>.</summary>
    public readonly struct SheetChange
    {
        internal SheetChange(int serial, SheetChangeKind kind, FoldPlan plan, PaperState previous)
        {
            Serial = serial;
            Kind = kind;
            Plan = plan;
            Previous = previous;
        }

        /// <summary>0 before the first change, then +1 per change.</summary>
        public int Serial { get; }

        public SheetChangeKind Kind { get; }

        /// <summary>Plan of a Folded/Creased change; otherwise null.</summary>
        public FoldPlan Plan { get; }

        /// <summary>Sheet state right before the command; null when there was no sheet.</summary>
        public PaperState Previous { get; }
    }
}
