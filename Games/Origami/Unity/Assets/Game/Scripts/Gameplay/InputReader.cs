using Origami.Core.Screens;
using Origami.Runtime.Folding;
using UnityEngine;

namespace Origami.Runtime.Gameplay
{
    /// <summary>
    /// The only place that reads the legacy Input Manager (C-193, CODING_CONVENTION): keyboard and mouse → <see cref="InputFrame"/>.
    /// Reading is separate from processing, so tests call <see cref="OrigamiApp.ProcessFrame"/> with hand-made frames.
    /// </summary>
    public static class InputReader
    {
        /// <summary>
        /// Reads this frame's input for <paramref name="app"/>. The paper point comes from <c>app.Table.ScreenToPaper</c> with the
        /// rig's camera; the right-drag delta is the mouse movement since the previous call for this app. Null app → empty frame.
        /// </summary>
        public static InputFrame Read(OrigamiApp app)
        {
            var frame = new InputFrame();
            if (app == null) return frame;

            if (Application.isFocused)
            {
                float x = Axis(KeyCode.D, KeyCode.RightArrow) - Axis(KeyCode.A, KeyCode.LeftArrow);
                float y = Axis(KeyCode.W, KeyCode.UpArrow) - Axis(KeyCode.S, KeyCode.DownArrow);
                frame.Move = new Vector2(x, y);
            }

            var keys = GameKeys.None;
            if (Input.GetKeyDown(KeyCode.Escape)) keys |= GameKeys.Escape;
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) keys |= GameKeys.Enter;
            if (Input.GetKeyDown(KeyCode.E)) keys |= GameKeys.Interact;
            if (Input.GetKeyDown(KeyCode.Space)) keys |= GameKeys.TakeSheet;
            if (Input.GetKeyDown(KeyCode.X)) keys |= GameKeys.Discard;
            if (Input.GetKeyDown(KeyCode.Z) || Input.GetKeyDown(KeyCode.Backspace)) keys |= GameKeys.Undo;
            if (Input.GetKeyDown(KeyCode.R)) keys |= GameKeys.Unfold;
            if (Input.GetKeyDown(KeyCode.F)) keys |= GameKeys.Flip;
            if (Input.GetKeyDown(KeyCode.B)) keys |= GameKeys.Book;
            if (Input.GetKeyDown(KeyCode.G)) keys |= GameKeys.SkipGuide;
            if (Input.GetKeyDown(KeyCode.Q)) keys |= GameKeys.StandUp;
            if (Input.GetKeyDown(KeyCode.LeftArrow)) keys |= GameKeys.PageLeft;
            if (Input.GetKeyDown(KeyCode.RightArrow)) keys |= GameKeys.PageRight;
            frame.Keys = keys;

            Vector2 mouse = Input.mousePosition;
            var p = new PointerFrame();
            var table = app.Table;
            var rig = app.CameraRig;
            if (table != null && rig != null && table.ScreenToPaper(rig.Camera, mouse, out var paper))
            {
                p.HasPaperPoint = true;
                p.PaperPoint = paper;
            }
            p.LeftDown = Input.GetMouseButtonDown(0);
            p.LeftHeld = Input.GetMouseButton(0);
            p.LeftUp = Input.GetMouseButtonUp(0);
            p.RightHeld = Input.GetMouseButton(1);
            p.RightDragDelta = app.LastMouse.HasValue && p.RightHeld ? mouse - app.LastMouse.Value : Vector2.zero;
            app.LastMouse = mouse;
            p.Wheel = Input.mouseScrollDelta.y;
            p.LayerUp = Input.GetKeyDown(KeyCode.RightBracket);
            p.LayerDown = Input.GetKeyDown(KeyCode.LeftBracket);
            p.Shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            p.Ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
            p.Escape = (keys & GameKeys.Escape) != 0;
            var view = app.View;
            p.OverGui = view != null && view.PointerOverGui(mouse);
            frame.Pointer = p;
            return frame;
        }

        private static float Axis(KeyCode a, KeyCode b) => Input.GetKey(a) || Input.GetKey(b) ? 1f : 0f;
    }
}
