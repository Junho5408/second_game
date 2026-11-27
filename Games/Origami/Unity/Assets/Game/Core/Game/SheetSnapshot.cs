using System.Collections.Generic;
using System.Collections.ObjectModel;
using Origami.Core.Folding;
using Origami.Core.Tutorial;

namespace Origami.Core.Game
{
    /// <summary>The sheet on the table (C-151, D-121). Immutable copy of the session values at snapshot time.</summary>
    public sealed class SheetSnapshot
    {
        internal SheetSnapshot(PaperState state, int foldCount, int creaseCount, bool canUndo, bool isTutorial,
            GuideStep guide, IList<StepScore> guideScores)
        {
            State = state;
            FoldCount = foldCount;
            CreaseCount = creaseCount;
            CanUndo = canUndo;
            IsTutorial = isTutorial;
            Guide = guide;
            GuideScores = new ReadOnlyCollection<StepScore>(new List<StepScore>(guideScores));
        }

        /// <summary>Current paper state (immutable; Runtime rebuilds meshes when the reference changes).</summary>
        public PaperState State { get; }

        public int FoldCount { get; }
        public int CreaseCount { get; }

        /// <summary>FoldCount + CreaseCount (Flip excluded).</summary>
        public int ActionCount => FoldCount + CreaseCount;

        /// <summary>Undo history not empty (Undo/Unfold allowed).</summary>
        public bool CanUndo { get; }

        /// <summary>The run's first sheet (free).</summary>
        public bool IsTutorial { get; }

        /// <summary>Tutorial sheet ∧ guide not skipped ∧ ActionCount &lt; 6.</summary>
        public bool GuideVisible => Guide != null;

        /// <summary>PlaneGuide.Steps[ActionCount] when the guide is visible, else null.</summary>
        public GuideStep Guide { get; }

        /// <summary>Step scores of the current actions (truncated by Undo, cleared by Unfold).</summary>
        public IReadOnlyList<StepScore> GuideScores { get; }
    }
}
