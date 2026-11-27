using UnityEngine;

namespace Origami.Runtime.Gameplay
{
    /// <summary>
    /// The screen layer as seen by <see cref="OrigamiApp"/> (implemented by <c>Origami.Runtime.UI.OrigamiGui</c>). Keeps the
    /// dependency direction UI → Gameplay (ARCHITECTURE §2): the app only knows this internal interface, and the GUI registers
    /// itself with <c>OrigamiGui.Bind</c>. Not part of the public surface.
    /// </summary>
    internal interface IAppView
    {
        /// <summary>Called at the end of <see cref="OrigamiApp.ProcessFrame"/> (C-194 <c>Refresh(dt)</c>).</summary>
        void Refresh(float dt);

        /// <summary>The screen position (pixels, y up as <c>Input.mousePosition</c>) is over a button or the book.</summary>
        bool PointerOverGui(Vector2 screenPosition);
    }
}
