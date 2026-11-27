using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Origami.Core.Content;

namespace Origami.Core.Run
{
    /// <summary>
    /// The fold in progress (F-003 "접는 동안 조회 가능한 정보"). Immutable. Deliberately exposes only the
    /// already-correct steps and the hint-revealed current step — never the remaining answer.
    /// </summary>
    public sealed class FoldSnapshot
    {
        public FoldSnapshot(string modelId, int stepCount, IList<FoldAction> correctSteps, int mistakes, int hints,
            FoldAction revealedAction, int penalty, Grade expectedGrade)
        {
            if (correctSteps == null) throw new ArgumentNullException(nameof(correctSteps));
            ModelId = modelId;
            StepCount = stepCount;
            CorrectSteps = new ReadOnlyCollection<FoldAction>(new List<FoldAction>(correctSteps));
            Mistakes = mistakes;
            Hints = hints;
            RevealedAction = revealedAction;
            Penalty = penalty;
            ExpectedGrade = expectedGrade;
        }

        public string ModelId { get; }

        /// <summary>Total steps N of the model.</summary>
        public int StepCount { get; }

        /// <summary>Progress i (= number of correct steps so far, 0..N-1 while folding).</summary>
        public int Progress => CorrectSteps.Count;

        /// <summary>Symbols of the steps already folded correctly, in order.</summary>
        public IReadOnlyList<FoldAction> CorrectSteps { get; }

        /// <summary>Mistakes in this fold (0..2 while folding).</summary>
        public int Mistakes { get; }

        /// <summary>Number of steps on which a hint was used (R-006).</summary>
        public int Hints { get; }

        /// <summary>True if the current step's answer was revealed by Hint.</summary>
        public bool IsCurrentStepRevealed => FoldActions.IsValid(RevealedAction);

        /// <summary>The revealed answer of the current step, or default (0) if not revealed.</summary>
        public FoldAction RevealedAction { get; }

        /// <summary>Current penalty (mistakes × 2 + hints).</summary>
        public int Penalty { get; }

        /// <summary>Grade the piece would get with the current penalty (F-003 AC-13).</summary>
        public Grade ExpectedGrade { get; }
    }
}
