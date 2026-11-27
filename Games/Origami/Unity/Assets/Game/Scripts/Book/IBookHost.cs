using System.Collections.Generic;

namespace Origami.Runtime.Book
{
    /// <summary>
    /// What the book view needs from the game (C-192). The game implementation (ORIGAMI-014) forwards to
    /// <c>ScreenFlow</c>; tests use a fake. The panel never changes game state itself — it only calls
    /// <see cref="TurnPage"/> and <see cref="BuyPage"/>.
    /// </summary>
    public interface IBookHost
    {
        /// <summary>Left (even) page of the open spread (<c>GameSnapshot.BookPage</c>).</summary>
        int CurrentPage { get; }

        /// <summary>Owned model IDs (<c>GameSnapshot.OwnedModelIds</c>). Null is treated as "none owned".</summary>
        IReadOnlyList<string> OwnedModelIds { get; }

        /// <summary>Current money (shown on covers that can be bought).</summary>
        int Money { get; }

        /// <summary><c>ScreenFlow.TurnPage</c> (Core clamps to a spread, R-010).</summary>
        void TurnPage(int page);

        /// <summary><c>ScreenFlow.BuyPage</c> (Core decides acceptance, F-008).</summary>
        void BuyPage(string modelId);
    }
}
