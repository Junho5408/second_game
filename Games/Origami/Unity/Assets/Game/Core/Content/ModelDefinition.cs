using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Origami.Core.Content
{
    /// <summary>One origami model (CONTENT_SPECS "작품 표"). Immutable.</summary>
    public sealed class ModelDefinition
    {
        public ModelDefinition(int order, string id, string displayName, FoldAction[] steps,
            int paperCost, int basePrice, string unlockPrerequisiteId)
        {
            if (id == null) throw new ArgumentNullException(nameof(id));
            if (displayName == null) throw new ArgumentNullException(nameof(displayName));
            if (steps == null) throw new ArgumentNullException(nameof(steps));
            Order = order;
            Id = id;
            DisplayName = displayName;
            Steps = new ReadOnlyCollection<FoldAction>((FoldAction[])steps.Clone());
            PaperCost = paperCost;
            BasePrice = basePrice;
            UnlockPrerequisiteId = unlockPrerequisiteId;
        }

        /// <summary>1-based position in the catalog / workbench list.</summary>
        public int Order { get; }

        /// <summary>Model ID (<see cref="ModelIds"/>).</summary>
        public string Id { get; }

        /// <summary>Korean display name, e.g. "종이배".</summary>
        public string DisplayName { get; }

        /// <summary>The correct fold sequence (the "도안"). Views may show it only on the preview screen (F-007).</summary>
        public IReadOnlyList<FoldAction> Steps { get; }

        public int StepCount => Steps.Count;

        /// <summary>Paper cost paid on Start (R-004).</summary>
        public int PaperCost { get; }

        /// <summary>Base price used by R-011.</summary>
        public int BasePrice { get; }

        /// <summary>
        /// ID of the model whose cumulative completions (any grade) unlock this one (R-010),
        /// or null if unlocked from the start.
        /// </summary>
        public string UnlockPrerequisiteId { get; }

        public bool IsInitiallyUnlocked => UnlockPrerequisiteId == null;
    }
}
