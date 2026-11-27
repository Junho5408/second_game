using System.Collections.Generic;
using Origami.Core.Catalog;
using Origami.Core.Screens;
using Origami.Runtime.Book;

namespace Origami.Tests.PlayMode.BookTests
{
    /// <summary>Test-only <see cref="IBookHost"/> that records calls; TurnPage clamps like Core (R-010).</summary>
    internal sealed class FakeBookHost : IBookHost
    {
        public int Page;
        public readonly List<string> Owned = new List<string> { "plane", "house" };
        public int Cash = 20;
        public readonly List<int> TurnCalls = new List<int>();
        public readonly List<string> BuyCalls = new List<string>();

        public int CurrentPage => Page;
        public IReadOnlyList<string> OwnedModelIds => Owned;
        public int Money => Cash;

        public void TurnPage(int page)
        {
            TurnCalls.Add(page);
            Page = BookLayout.ClampToSpread(page);
        }

        public void BuyPage(string modelId) => BuyCalls.Add(modelId);
    }

    /// <summary>Test-only <see cref="IBookHost"/> over a real <see cref="ScreenFlow"/> (what 014's game host forwards to).</summary>
    internal sealed class FlowBookHost : IBookHost
    {
        public readonly ScreenFlow Flow = new ScreenFlow();

        public int CurrentPage => Flow.Snapshot.BookPage;
        public IReadOnlyList<string> OwnedModelIds => Flow.Snapshot.OwnedModelIds;
        public int Money => Flow.Snapshot.Money;
        public void TurnPage(int page) => Flow.TurnPage(page);
        public void BuyPage(string modelId) => Flow.BuyPage(modelId);
    }
}
