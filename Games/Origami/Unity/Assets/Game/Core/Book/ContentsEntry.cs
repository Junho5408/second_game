namespace Origami.Core.Book
{
    /// <summary>One line of the contents page (C-160, CONTENT §3: name, owned / "N원", cover page). Immutable.</summary>
    public sealed class ContentsEntry
    {
        internal ContentsEntry(string modelId, string name, bool owned, int pagePrice, int coverPage)
        {
            ModelId = modelId;
            Name = name;
            Owned = owned;
            PagePrice = pagePrice;
            CoverPage = coverPage;
        }

        public string ModelId { get; }
        public string Name { get; }
        public bool Owned { get; }
        public int PagePrice { get; }

        /// <summary>Page of the model's cover (a contents click turns to it, R-010).</summary>
        public int CoverPage { get; }
    }
}
