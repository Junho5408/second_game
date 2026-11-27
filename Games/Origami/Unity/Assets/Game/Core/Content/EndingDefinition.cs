using System;

namespace Origami.Core.Content
{
    /// <summary>One ending (CONTENT_SPECS "엔딩 표"). Conditions live in <c>Rules.EndingRules</c>, not here. Immutable.</summary>
    public sealed class EndingDefinition
    {
        public EndingDefinition(string id, int priority, string displayName, string text)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            Priority = priority;
            DisplayName = displayName ?? throw new ArgumentNullException(nameof(displayName));
            Text = text ?? throw new ArgumentNullException(nameof(text));
        }

        /// <summary>Ending ID (<see cref="EndingIds"/>).</summary>
        public string Id { get; }

        /// <summary>1 = checked first (R-014).</summary>
        public int Priority { get; }

        /// <summary>Korean display name, e.g. "종이 재벌".</summary>
        public string DisplayName { get; }

        /// <summary>Result-screen sentence.</summary>
        public string Text { get; }
    }
}
