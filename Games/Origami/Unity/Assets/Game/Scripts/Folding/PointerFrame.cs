using UnityEngine;

namespace Origami.Runtime.Folding
{
    /// <summary>
    /// Mouse/key input of one frame for <see cref="FoldTable.ProcessFrame"/> (C-191). Filled by <c>InputReader</c> in the
    /// game (task 014) or built directly by tests (batch mode has no real input).
    /// </summary>
    public struct PointerFrame
    {
        /// <summary>The pointer hits the paper plane; <see cref="PaperPoint"/> is valid (paper coordinates, see
        /// <see cref="FoldTable.ScreenToPaper"/>).</summary>
        public bool HasPaperPoint;
        public Vector2 PaperPoint;

        /// <summary>Left button pressed this frame / held / released this frame.</summary>
        public bool LeftDown, LeftHeld, LeftUp;

        /// <summary>Right button held (camera orbit) and the mouse movement this frame in pixels.</summary>
        public bool RightHeld;
        public Vector2 RightDragDelta;

        /// <summary><c>Input.mouseScrollDelta.y</c>. While dragging only its sign counts (±1 layer per frame, review N-3).</summary>
        public float Wheel;

        /// <summary><c>]</c> / <c>[</c> pressed this frame (layer choice +1 / −1, A2-8).</summary>
        public bool LayerUp, LayerDown;

        /// <summary>Shift held = mountain fold, Ctrl held = crease on release, Escape pressed = cancel the drag.</summary>
        public bool Shift, Ctrl, Escape;

        /// <summary>The pointer is over an IMGUI button or the book (no grab, no zoom).</summary>
        public bool OverGui;
    }
}
