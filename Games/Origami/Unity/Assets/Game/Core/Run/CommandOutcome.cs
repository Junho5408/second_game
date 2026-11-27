namespace Origami.Core.Run
{
    /// <summary>What an accepted command did (lets the UI pick a notice without diffing snapshots).</summary>
    public enum CommandOutcome
    {
        /// <summary>The command was rejected; see <see cref="CommandResult.Reason"/>.</summary>
        Rejected = 0,
        /// <summary>NewRun: fresh R-002 state.</summary>
        RunStarted = 1,
        /// <summary>Start: paper bought, folding at step 0.</summary>
        FoldStarted = 2,
        /// <summary>Fold: correct, more steps remain.</summary>
        StepCorrect = 3,
        /// <summary>Fold: wrong action, mistakes &lt; 3, same step.</summary>
        StepMistake = 4,
        /// <summary>Fold: last step correct; <see cref="CommandResult.Piece"/> holds the piece.</summary>
        FoldCompleted = 5,
        /// <summary>Fold: third mistake, paper ruined (R-007).</summary>
        FoldRuined = 6,
        /// <summary>Hint: current step revealed (also when it was already revealed; count unchanged then).</summary>
        HintRevealed = 7,
        /// <summary>Abandon (R-008).</summary>
        FoldAbandoned = 8,
        /// <summary>EndDay: settled, next day started (not bankrupt).</summary>
        DayEnded = 9,
        /// <summary>EndDay: settled and the run ended (Completed or Bankrupt; see Settlement.EndReason).</summary>
        RunFinished = 10,
    }
}
