using Origami.Core.Screens;
using Origami.Runtime.Folding;
using UnityEngine;

namespace Origami.Runtime.Gameplay
{
    /// <summary>
    /// Input of one frame for <see cref="OrigamiApp.ProcessFrame"/> (C-193). Filled by <see cref="InputReader.Read"/> in the
    /// game, built directly by PlayMode tests (batch mode has no real input).
    /// </summary>
    public struct InputFrame
    {
        /// <summary>x = east (D/→ +1, A/← −1), y = north (W/↑ +1, S/↓ −1); opposite keys together = 0. (0, 0) without window focus.</summary>
        public Vector2 Move;

        /// <summary>Keys pressed this frame (GetKeyDown): Esc, Return/KeypadEnter, E, Space, X, Z/Backspace, R, F, B, G, Q, ←, →.</summary>
        public GameKeys Keys;

        /// <summary>Mouse for the folding table (paper point from <see cref="FoldTable.ScreenToPaper"/>).</summary>
        public PointerFrame Pointer;
    }
}
