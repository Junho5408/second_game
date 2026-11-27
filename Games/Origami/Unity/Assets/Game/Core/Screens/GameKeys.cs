using System;

namespace Origami.Core.Screens
{
    /// <summary>
    /// Keys pressed in one frame, as read by the Runtime (C-171). <see cref="ScreenFlow.ProcessKeys"/> handles only the highest-priority
    /// key: Escape &gt; Enter &gt; Interact &gt; TakeSheet &gt; Discard &gt; Undo &gt; Unfold &gt; Flip &gt; Book &gt; SkipGuide &gt; StandUp &gt; PageLeft &gt; PageRight.
    /// </summary>
    [Flags]
    public enum GameKeys
    {
        None = 0,
        /// <summary>Esc.</summary>
        Escape = 1 << 0,
        /// <summary>Enter.</summary>
        Enter = 1 << 1,
        /// <summary>E.</summary>
        Interact = 1 << 2,
        /// <summary>Space.</summary>
        TakeSheet = 1 << 3,
        /// <summary>X.</summary>
        Discard = 1 << 4,
        /// <summary>Z / Backspace.</summary>
        Undo = 1 << 5,
        /// <summary>R.</summary>
        Unfold = 1 << 6,
        /// <summary>F.</summary>
        Flip = 1 << 7,
        /// <summary>B.</summary>
        Book = 1 << 8,
        /// <summary>G.</summary>
        SkipGuide = 1 << 9,
        /// <summary>Q.</summary>
        StandUp = 1 << 10,
        /// <summary>←.</summary>
        PageLeft = 1 << 11,
        /// <summary>→.</summary>
        PageRight = 1 << 12,
    }
}
