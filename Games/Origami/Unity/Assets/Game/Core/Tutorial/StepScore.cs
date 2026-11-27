namespace Origami.Core.Tutorial
{
    /// <summary>Accuracy of one guided action (C-140; Fold3DSpike StepScore + <see cref="KindMismatch"/>). Feedback only. Immutable.</summary>
    public sealed class StepScore
    {
        internal StepScore(int score, double creaseOffset, double angleErrorDeg, double grabError, double dropError, bool wrongSide, bool kindMismatch)
        {
            Score = score;
            CreaseOffset = creaseOffset;
            AngleErrorDeg = angleErrorDeg;
            GrabError = grabError;
            DropError = dropError;
            WrongSide = wrongSide;
            KindMismatch = kindMismatch;
        }

        /// <summary>0..100 (0 when <see cref="KindMismatch"/>).</summary>
        public int Score { get; }

        /// <summary>Mean distance of the ideal line ends (LineA, LineB) from the player's line (paper units).</summary>
        public double CreaseOffset { get; }

        /// <summary>Angle between the player's and the ideal line, 0..90°.</summary>
        public double AngleErrorDeg { get; }

        /// <summary>Distance of the grab point from the target grab (mirrored pair for EitherSide steps).</summary>
        public double GrabError { get; }

        /// <summary>Distance of the drop point from the target drop (mirrored pair for EitherSide steps).</summary>
        public double DropError { get; }

        /// <summary>The other part of the paper was moved (score halved). Never set on EitherSide steps.</summary>
        public bool WrongSide { get; }

        /// <summary>Action kind differs from the step kind (Fold vs Crease); score 0.</summary>
        public bool KindMismatch { get; }
    }
}
