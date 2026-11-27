using System;
using System.Collections.Generic;
using Origami.Core.Folding;

namespace Origami.Core.Recognition
{
    /// <summary>R-011..R-013 recognition as a pure function (C-110, F-009).</summary>
    public static class Recognizer
    {
        /// <summary>
        /// Candidates = catalogue models (catalogue order) that are in <paramref name="ownedModelIds"/> (unknown and
        /// duplicate IDs ignored). The best is replaced only by a strictly greater score (tie → earlier model, R-012).
        /// It is a piece iff Grading.FromScore(best) ≠ None. The player outline is scanned once.
        /// </summary>
        /// <exception cref="ArgumentNullException">any argument is null.</exception>
        public static RecognitionResult Recognize(PaperState state, IEnumerable<string> ownedModelIds, RecognitionCatalog catalogue)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (ownedModelIds == null) throw new ArgumentNullException(nameof(ownedModelIds));
            if (catalogue == null) throw new ArgumentNullException(nameof(catalogue));
            var owned = new HashSet<string>(StringComparer.Ordinal);
            foreach (var id in ownedModelIds)
                if (id != null) owned.Add(id);

            Outline player = null;
            string bestId = null;
            double bestScore = 0;
            var ids = catalogue.ModelIds;
            for (int i = 0; i < ids.Count; i++)
            {
                if (!owned.Contains(ids[i])) continue;
                if (player == null) player = RecognitionCatalog.CenteredOutline(state);
                double s = catalogue.Score(player, i);
                if (bestId == null || s > bestScore)
                {
                    bestId = ids[i];
                    bestScore = s;
                }
            }
            var grade = bestId == null ? Grade.None : Grading.FromScore(bestScore);
            return new RecognitionResult(grade == Grade.None ? null : bestId, grade, bestId, bestScore);
        }
    }
}
