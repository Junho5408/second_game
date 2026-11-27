namespace Origami.Core.Catalog
{
    /// <summary>Ending display data (C-120, D-114). Conditions live in code (<c>EndingRules</c>, C-130). Immutable.</summary>
    public sealed class EndingDefinition
    {
        internal EndingDefinition(string id, int priority, string displayName, string text)
        {
            Id = id;
            Priority = priority;
            DisplayName = displayName;
            Text = text;
        }

        public string Id { get; }

        /// <summary>1 = checked first (R-018).</summary>
        public int Priority { get; }
        public string DisplayName { get; }
        public string Text { get; }
    }
}
