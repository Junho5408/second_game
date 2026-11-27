using System;
using Origami.Core.Content;

namespace Origami.Core.Run
{
    /// <summary>
    /// The single mutable game state of one run plus all player commands (F-001..F-006, R-001..R-015).
    /// Deterministic: no clock, no randomness, no static mutable state. Commands never throw for rule
    /// violations — they return a rejection and leave state untouched (R-015). Not thread-safe.
    /// See INTERFACE_CONTRACTS.md C-010.
    /// </summary>
    public sealed class OrigamiRun
    {
        /// <summary>Creates a run already in the R-002 start state (same as calling <see cref="NewRun"/>).</summary>
        public OrigamiRun()
        {
            throw new NotImplementedException();
        }

        /// <summary>Always accepted (RunStarted). Discards any fold and today's pieces; no ending is evaluated.</summary>
        public CommandResult NewRun()
        {
            throw new NotImplementedException();
        }

        /// <summary>Starts folding <paramref name="modelId"/> (R-004). Accepted → FoldStarted.</summary>
        public CommandResult Start(string modelId)
        {
            throw new NotImplementedException();
        }

        /// <summary>One fold action (R-005, R-007, R-009, R-010). Accepted → StepCorrect / StepMistake / FoldCompleted / FoldRuined.</summary>
        public CommandResult Fold(FoldAction action)
        {
            throw new NotImplementedException();
        }

        /// <summary>Reveals the current step (R-006). Accepted → HintRevealed.</summary>
        public CommandResult Hint()
        {
            throw new NotImplementedException();
        }

        /// <summary>Gives up the current fold (R-008). Accepted → FoldAbandoned.</summary>
        public CommandResult Abandon()
        {
            throw new NotImplementedException();
        }

        /// <summary>Evening settlement and day advance (R-012, R-013, R-014). Accepted → DayEnded / RunFinished with Settlement.</summary>
        public CommandResult EndDay()
        {
            throw new NotImplementedException();
        }

        /// <summary>A new immutable snapshot of the current state.</summary>
        public RunSnapshot GetSnapshot()
        {
            throw new NotImplementedException();
        }
    }
}
