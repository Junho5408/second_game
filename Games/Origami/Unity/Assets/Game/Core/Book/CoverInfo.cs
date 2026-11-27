using Origami.Core.Folding;

namespace Origami.Core.Book
{
    /// <summary>Data of a cover page (C-160, CONTENT §3). Covers are never sealed. Immutable.</summary>
    public sealed class CoverInfo
    {
        internal CoverInfo(string modelId, string name, PaperState silhouette, double rotationDeg, int difficulty, int actionCount,
            string techniques, int basePrice, int pagePrice, bool owned)
        {
            ModelId = modelId;
            Name = name;
            Silhouette = silhouette;
            RotationDeg = rotationDeg;
            Difficulty = difficulty;
            ActionCount = actionCount;
            Techniques = techniques;
            BasePrice = basePrice;
            PagePrice = pagePrice;
            Owned = owned;
        }

        public string ModelId { get; }
        public string Name { get; }

        /// <summary>Reference state of the model (drawn grey when not owned).</summary>
        public PaperState Silhouette { get; }

        /// <summary>Picture rotation (tulip, cicada 45°).</summary>
        public double RotationDeg { get; }

        /// <summary>Number of ★.</summary>
        public int Difficulty { get; }

        public int ActionCount { get; }
        public string Techniques { get; }
        public int BasePrice { get; }
        public int PagePrice { get; }
        public bool Owned { get; }

        /// <summary>= !Owned (the "사기" button is shown).</summary>
        public bool Buyable => !Owned;
    }
}
