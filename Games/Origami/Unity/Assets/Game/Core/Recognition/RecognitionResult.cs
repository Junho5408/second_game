namespace Origami.Core.Recognition
{
    /// <summary>Result of <see cref="Recognizer.Recognize"/> (C-110). Immutable.</summary>
    public sealed class RecognitionResult
    {
        internal RecognitionResult(string modelId, Grade grade, string bestModelId, double bestScore)
        {
            ModelId = modelId;
            Grade = grade;
            BestModelId = bestModelId;
            BestScore = bestScore;
        }

        /// <summary>Recognised model, or null for crumpled paper.</summary>
        public string ModelId { get; }

        /// <summary>None for crumpled paper.</summary>
        public Grade Grade { get; }

        public bool IsPiece => ModelId != null;

        /// <summary>= <see cref="BestScore"/>.</summary>
        public double Score => BestScore;

        /// <summary>= Grading.Percent(BestScore).</summary>
        public int Percent => Grading.Percent(BestScore);

        /// <summary>Most similar candidate (null when there is no candidate).</summary>
        public string BestModelId { get; }

        /// <summary>0 when there is no candidate.</summary>
        public double BestScore { get; }
    }
}
