using System;

namespace Origami.Core.Rules
{
    /// <summary>
    /// R-010 unlock rules. Stateless: because completions only grow within a run, "unlocked" is a pure
    /// function of the current <see cref="CompletionCounts"/>.
    /// </summary>
    public static class Unlocking
    {
        /// <summary>
        /// True for initially unlocked models (no prerequisite) or when Total(prerequisite) ≥ UnlockCount.
        /// False for null/unknown IDs. <paramref name="counts"/> null → ArgumentNullException.
        /// </summary>
        public static bool IsUnlocked(string modelId, CompletionCounts counts)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Unlock progress shown on the workbench ("(1/3)"): min(Total(prerequisite), UnlockCount);
        /// UnlockCount for initially unlocked models; 0 for null/unknown IDs.
        /// <paramref name="counts"/> null → ArgumentNullException.
        /// </summary>
        public static int Progress(string modelId, CompletionCounts counts)
        {
            throw new NotImplementedException();
        }
    }
}
