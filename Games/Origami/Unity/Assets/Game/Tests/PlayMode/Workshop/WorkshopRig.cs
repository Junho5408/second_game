using Origami.Core.Catalog;
using Origami.Core.Folding;
using Origami.Core.Game;
using Origami.Core.Geometry;
using Origami.Core.Room;
using Origami.Core.Screens;
using Origami.Runtime.Folding;
using Origami.Runtime.Gameplay;
using Origami.Runtime.UI;
using UnityEngine;

namespace Origami.Tests.PlayMode.Workshop
{
    /// <summary>
    /// An independent <see cref="OrigamiApp"/> + <see cref="OrigamiGui"/> (not the runtime singleton) with its own camera, driven
    /// only by synchronous <see cref="OrigamiApp.ProcessFrame"/> calls with a fixed dt (CODING_CONVENTION PlayMode rules, A2-5).
    /// </summary>
    internal sealed class WorkshopRig
    {
        public const float Dt = 0.02f;

        public readonly GameObject Root;
        public readonly OrigamiApp App;
        public readonly OrigamiGui Gui;
        public readonly Camera Camera;
        private readonly GameObject _cameraGo;

        public WorkshopRig(bool ownCamera = true)
        {
            Root = new GameObject("[OrigamiTest]");
            App = Root.AddComponent<OrigamiApp>();
            Gui = Root.AddComponent<OrigamiGui>();
            Gui.Bind(App);
            if (ownCamera)
            {
                _cameraGo = new GameObject("TestCamera");
                Camera = _cameraGo.AddComponent<Camera>();
                App.CameraRig.UseCamera(Camera);
            }
        }

        public ScreenFlow Flow => App.Flow;
        public GameSnapshot Snap => App.Flow.Snapshot;
        public WorkshopMode Mode => App.Flow.Snapshot.Mode;

        public void Destroy()
        {
            if (Root != null) Object.Destroy(Root);
            if (_cameraGo != null) Object.Destroy(_cameraGo);
        }

        public void Frame(InputFrame f, float dt = Dt) => App.ProcessFrame(f, dt);

        public void Idle(float seconds)
        {
            int n = Mathf.RoundToInt(seconds / Dt);
            for (int i = 0; i < n; i++) Frame(new InputFrame());
        }

        /// <summary>Holds a move input for <paramref name="seconds"/> in frames of 0.02 s.</summary>
        public void Hold(float x, float y, float seconds)
        {
            int n = Mathf.RoundToInt(seconds / Dt);
            for (int i = 0; i < n; i++) Frame(new InputFrame { Move = new Vector2(x, y) });
        }

        /// <summary>One frame with <paramref name="keys"/> pressed.</summary>
        public void Key(GameKeys keys) => Frame(new InputFrame { Keys = keys });

        public void Place(double x, double z, float yaw = 90f) => App.SetCharacterPosition(new Vector2((float)x, (float)z), yaw);

        /// <summary>Stand 0.5 m south of the chair point and press E (F-003).</summary>
        public void Sit()
        {
            var p = RoomLayout.InteractPoint(InteractTargets.Table);
            Place(p.X, p.Y - 0.5, 0f);
            Key(GameKeys.Interact);
        }

        public static PointerFrame At(Vec2 p) =>
            new PointerFrame { HasPaperPoint = true, PaperPoint = new Vector2((float)p.X, (float)p.Y) };

        /// <summary>Drag on the table through the app: press at grab, move to drop, release at drop (Ctrl → crease).</summary>
        public void Drag(Vec2 grab, Vec2 drop, bool ctrl = false, bool shift = false)
        {
            var down = At(grab);
            down.LeftDown = true;
            down.LeftHeld = true;
            Frame(new InputFrame { Pointer = down });
            for (int i = 1; i <= 3; i++)
            {
                var mid = At(Vec2.Lerp(grab, drop, i / 3.0));
                mid.LeftHeld = true;
                mid.Ctrl = ctrl;
                mid.Shift = shift;
                Frame(new InputFrame { Pointer = mid });
            }
            var up = At(drop);
            up.LeftUp = true;
            up.Ctrl = ctrl;
            up.Shift = shift;
            Frame(new InputFrame { Pointer = up });
        }

        /// <summary>Lets any fold / crease / flip animation finish.</summary>
        public void Settle() => Idle(0.8f);

        /// <summary>Performs the visible tutorial guide step by dragging exactly from its grab to its drop.</summary>
        public void DoGuideStep()
        {
            var g = Snap.Sheet.Guide;
            Drag(g.Grab, g.Drop, g.Kind == FoldStepKind.Crease);
            Settle();
        }

        public bool Shows(string line)
        {
            foreach (var l in Gui.VisibleLines)
                if (l == line) return true;
            return false;
        }
    }
}
