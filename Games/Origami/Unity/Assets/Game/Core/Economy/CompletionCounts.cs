using System;
using Origami.Core.Catalog;
using Origami.Core.Recognition;

namespace Origami.Core.Economy
{
    /// <summary>
    /// Immutable completion counts per (model, grade ★1~★3) for the 5 catalogue models (C-130; ported from v1
    /// <c>Rules/CompletionCounts</c>, ARCHITECTURE §10). Input of the ending rules (R-018) and part of the game snapshot. Value equality.
    /// </summary>
    public sealed class CompletionCounts : IEquatable<CompletionCounts>
    {
        private const int GradeCount = 3;

        /// <summary>All counts 0.</summary>
        public static readonly CompletionCounts Empty = new CompletionCounts(new int[ModelCatalog.Models.Count * GradeCount]);

        // index = catalogue index × 3 + (grade − 1)
        private readonly int[] _counts;

        private CompletionCounts(int[] counts)
        {
            _counts = counts;
        }

        /// <summary>Count for (model, grade). 0 for null/unknown IDs, <see cref="Grade.None"/> or undefined grades. Never throws.</summary>
        public int Get(string modelId, Grade grade)
        {
            int i = Index(modelId, grade);
            return i < 0 ? 0 : _counts[i];
        }

        /// <summary>Sum over ★1~★3 for the model ("등급 무관"). 0 for null/unknown IDs.</summary>
        public int Total(string modelId)
        {
            return Get(modelId, Grade.Star1) + Get(modelId, Grade.Star2) + Get(modelId, Grade.Star3);
        }

        /// <summary>Copy with (model, grade) set to <paramref name="count"/>.</summary>
        /// <exception cref="ArgumentException">unknown/null model ID, or grade not ★1~★3.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> &lt; 0.</exception>
        public CompletionCounts With(string modelId, Grade grade, int count)
        {
            int i = Index(modelId, grade);
            if (i < 0) throw new ArgumentException("unknown model ID or invalid grade: " + (modelId ?? "null") + " / " + (int)grade);
            if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
            var copy = (int[])_counts.Clone();
            copy[i] = count;
            return new CompletionCounts(copy);
        }

        /// <summary>Copy with (model, grade) increased by 1. Same exceptions as <see cref="With"/>.</summary>
        public CompletionCounts Increment(string modelId, Grade grade)
        {
            return With(modelId, grade, Get(modelId, grade) + 1);
        }

        public bool Equals(CompletionCounts other)
        {
            if (ReferenceEquals(other, null)) return false;
            for (int i = 0; i < _counts.Length; i++)
            {
                if (_counts[i] != other._counts[i]) return false;
            }
            return true;
        }

        public override bool Equals(object obj) => Equals(obj as CompletionCounts);

        public override int GetHashCode()
        {
            unchecked
            {
                int h = 17;
                for (int i = 0; i < _counts.Length; i++) h = h * 31 + _counts[i];
                return h;
            }
        }

        private static int Index(string modelId, Grade grade)
        {
            int m = ModelCatalog.IndexOf(modelId);
            if (m < 0 || grade < Grade.Star1 || grade > Grade.Star3) return -1;
            return m * GradeCount + ((int)grade - 1);
        }
    }
}
