namespace Origami.Core.Run
{
    /// <summary>Result of one <see cref="OrigamiRun"/> command. Immutable value type. Commands never throw for rule violations (R-015).</summary>
    public readonly struct CommandResult
    {
        private CommandResult(CommandOutcome outcome, RejectReason reason, CompletedPiece piece, SettlementResult settlement)
        {
            Outcome = outcome;
            Reason = reason;
            Piece = piece;
            Settlement = settlement;
        }

        /// <summary>A rejection with <paramref name="reason"/> (must not be None).</summary>
        public static CommandResult Rejected(RejectReason reason) =>
            new CommandResult(CommandOutcome.Rejected, reason, default, null);

        /// <summary>An accepted command without piece or settlement.</summary>
        public static CommandResult Accepted(CommandOutcome outcome) =>
            new CommandResult(outcome, RejectReason.None, default, null);

        /// <summary>Accepted Fold that completed <paramref name="piece"/> (outcome FoldCompleted).</summary>
        public static CommandResult Completed(CompletedPiece piece) =>
            new CommandResult(CommandOutcome.FoldCompleted, RejectReason.None, piece, null);

        /// <summary>Accepted EndDay (outcome DayEnded or RunFinished).</summary>
        public static CommandResult Settled(CommandOutcome outcome, SettlementResult settlement) =>
            new CommandResult(outcome, RejectReason.None, default, settlement);

        public bool IsAccepted => Outcome != CommandOutcome.Rejected;

        /// <summary>None when accepted.</summary>
        public RejectReason Reason { get; }

        public CommandOutcome Outcome { get; }

        /// <summary>The finished piece for FoldCompleted; default otherwise.</summary>
        public CompletedPiece Piece { get; }

        /// <summary>The settlement for an accepted EndDay; null otherwise.</summary>
        public SettlementResult Settlement { get; }

        public override string ToString() => IsAccepted ? Outcome.ToString() : "Rejected(" + Reason + ")";
    }
}
