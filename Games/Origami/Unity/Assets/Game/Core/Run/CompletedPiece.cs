using System;
using Origami.Core.Content;

namespace Origami.Core.Run
{
    /// <summary>A finished piece waiting in today's list (R-009). Value type; default = no piece (null ID, grade 0).</summary>
    public readonly struct CompletedPiece : IEquatable<CompletedPiece>
    {
        public CompletedPiece(string modelId, Grade grade)
        {
            ModelId = modelId;
            Grade = grade;
        }

        public string ModelId { get; }
        public Grade Grade { get; }

        public bool Equals(CompletedPiece other) =>
            string.Equals(ModelId, other.ModelId, StringComparison.Ordinal) && Grade == other.Grade;

        public override bool Equals(object obj) => obj is CompletedPiece other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                return ((ModelId == null ? 0 : StringComparer.Ordinal.GetHashCode(ModelId)) * 397) ^ (int)Grade;
            }
        }

        public static bool operator ==(CompletedPiece left, CompletedPiece right) => left.Equals(right);
        public static bool operator !=(CompletedPiece left, CompletedPiece right) => !left.Equals(right);

        /// <summary>Debug format "boat ★3".</summary>
        public override string ToString() => (ModelId ?? "-") + " ★" + (int)Grade;
    }
}
