using Origami.Core.Folding;
using Origami.Core.Recognition;
using Origami.Core.Tutorial;

namespace Origami.Core.Game
{
    /// <summary>Outcome of one <see cref="OrigamiGame"/> command (C-151). Immutable.</summary>
    public sealed class CommandResult
    {
        private CommandResult(bool accepted, RejectReason reason, FoldError foldError, FoldPlan plan,
            RecognitionResult recognition, StepScore guideScore, SettlementResult settlement)
        {
            Accepted = accepted;
            Reason = reason;
            FoldError = foldError;
            Plan = plan;
            Recognition = recognition;
            GuideScore = guideScore;
            Settlement = settlement;
        }

        internal static CommandResult Accept(FoldPlan plan = null, RecognitionResult recognition = null,
            StepScore guideScore = null, SettlementResult settlement = null) =>
            new CommandResult(true, RejectReason.None, FoldError.None, plan, recognition, guideScore, settlement);

        internal static CommandResult Reject(RejectReason reason, FoldError foldError = FoldError.None) =>
            new CommandResult(false, reason, foldError, null, null, null, null);

        public bool Accepted { get; }

        /// <summary><see cref="RejectReason.None"/> when accepted.</summary>
        public RejectReason Reason { get; }

        /// <summary>Engine reason for <see cref="RejectReason.InvalidFold"/> (layers &lt; 0 → InvalidLayers); otherwise None.</summary>
        public FoldError FoldError { get; }

        /// <summary>Plan of an accepted Fold/Crease; otherwise null.</summary>
        public FoldPlan Plan { get; }

        /// <summary>Recognition of an accepted Finish; otherwise null.</summary>
        public RecognitionResult Recognition { get; }

        /// <summary>Tutorial step score recorded by an accepted Fold/Crease while the guide was visible; otherwise null.</summary>
        public StepScore GuideScore { get; }

        /// <summary>Settlement of an accepted Sleep(true); otherwise null.</summary>
        public SettlementResult Settlement { get; }
    }
}
