using Origami.Core.Folding;
using Origami.Core.Recognition;

namespace Origami.Core.Game
{
    /// <summary>A finished piece on today's display (C-151, D-122). Immutable.</summary>
    public sealed class DisplayedPiece
    {
        internal DisplayedPiece(string modelId, Grade grade, double score, int percent, PaperState shape)
        {
            ModelId = modelId;
            Grade = grade;
            Score = score;
            Percent = percent;
            Shape = shape;
        }

        public string ModelId { get; }

        /// <summary>★1~★3.</summary>
        public Grade Grade { get; }

        /// <summary>Match score s (0..1).</summary>
        public double Score { get; }

        /// <summary>Grading.Percent(Score).</summary>
        public int Percent { get; }

        /// <summary>The player's paper state at the moment of Finish (drawn on the counter).</summary>
        public PaperState Shape { get; }
    }
}
