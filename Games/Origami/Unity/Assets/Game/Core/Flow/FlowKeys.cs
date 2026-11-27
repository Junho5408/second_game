using System;

namespace Origami.Core.Flow
{
    /// <summary>
    /// Keys pressed in one frame, already decoded by the Unity controller (F-007 / GAME_RULES key table):
    /// ↑/W FoldUp, ↓/S FoldDown, ←/A FoldLeft, →/D FoldRight, Space Flip, H Hint, Esc Escape, Enter/KeypadEnter Enter.
    /// </summary>
    [Flags]
    public enum FlowKeys
    {
        None = 0,
        FoldUp = 1 << 0,
        FoldDown = 1 << 1,
        FoldLeft = 1 << 2,
        FoldRight = 1 << 3,
        Flip = 1 << 4,
        Hint = 1 << 5,
        Escape = 1 << 6,
        Enter = 1 << 7,
    }
}
