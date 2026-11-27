using System;
using Origami.Core.Content;

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
            if (counts == null) throw new ArgumentNullException(nameof(counts));
            var model = OrigamiCatalog.FindModel(modelId);
            if (model == null) return false;
            if (model.IsInitiallyUnlocked) return true;
            return counts.Total(model.UnlockPrerequisiteId) >= RuleConstants.UnlockCount;
        }

        /// <summary>
        /// Unlock progress shown on the workbench ("(1/3)"): min(Total(prerequisite), UnlockCount);
        /// UnlockCount for initially unlocked models; 0 for null/unknown IDs.
        /// <paramref name="counts"/> null → ArgumentNullException.
        /// </summary>
        public static int Progress(string modelId, CompletionCounts counts)
        {
            if (counts == null) throw new ArgumentNullException(nameof(counts));
            var model = OrigamiCatalog.FindModel(modelId);
            if (model == null) return 0;
            if (model.IsInitiallyUnlocked) return RuleConstants.UnlockCount;
            return Math.Min(counts.Total(model.UnlockPrerequisiteId), RuleConstants.UnlockCount);
        }
    }
}
