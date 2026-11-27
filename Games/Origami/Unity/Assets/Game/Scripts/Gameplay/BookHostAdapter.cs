using System;
using System.Collections.Generic;
using Origami.Core.Screens;
using Origami.Runtime.Book;

namespace Origami.Runtime.Gameplay
{
    /// <summary>
    /// <see cref="IBookHost"/> over the game's <see cref="ScreenFlow"/> (C-192). A rejected purchase (e.g. not enough money)
    /// becomes the flow's Reject message, which the GUI shows as a toast over the book (review ORIGAMI-013 W-3).
    /// </summary>
    internal sealed class BookHostAdapter : IBookHost
    {
        private readonly ScreenFlow _flow;

        public BookHostAdapter(ScreenFlow flow)
        {
            _flow = flow ?? throw new ArgumentNullException(nameof(flow));
        }

        public int CurrentPage => _flow.Snapshot.BookPage;
        public IReadOnlyList<string> OwnedModelIds => _flow.Snapshot.OwnedModelIds;
        public int Money => _flow.Snapshot.Money;
        public void TurnPage(int page) => _flow.TurnPage(page);
        public void BuyPage(string modelId) => _flow.BuyPage(modelId);
    }
}
