using Origami.Core.Game;
using UnityEngine;

namespace Origami.Runtime.Gameplay
{
    /// <summary>
    /// Walking follow camera and the seated transition (C-193, F-002 AC-06, F-003 AC-06, ARCHITECTURE §6–7, A2-14).
    /// <para>
    /// The camera is obtained lazily on the first <see cref="Tick"/> (review ORIGAMI-008 N-2): <c>Camera.main</c> (the
    /// Bootstrap camera, made perspective here) or, if there is none, a new camera tagged MainCamera — so the order of the
    /// AfterSceneLoad hooks does not matter. The rig keeps its own pose (<see cref="Position"/>/<see cref="Rotation"/>) and copies
    /// it to the camera, so tests can check the pose without a camera.
    /// </para>
    /// Walking: target = character + <see cref="WalkOffset"/>, fixed look direction from the offset to the head
    /// (<see cref="LookOffset"/>), position += (target − position)·(1 − e^(−8·dt)); no rotation by input.
    /// Seated: a <see cref="SitTransition"/> (0.5 s) smoothstep from the pose at the moment of sitting to
    /// <c>FoldTable.GetSeatedCameraPose</c>, then exactly that pose. No own Update — <see cref="OrigamiApp"/> calls <see cref="Tick"/>.
    /// </summary>
    public sealed class CameraRig : MonoBehaviour
    {
        public static readonly Vector3 WalkOffset = new Vector3(0f, 6.0f, -5.5f), LookOffset = new Vector3(0f, 0.8f, 0f);
        public const float FollowSharpness = 8f, SitTransition = 0.5f, WalkNearClip = 0.1f, SeatedNearClip = 0.01f;

        private const float FieldOfView = 50f;
        private const float FarClip = 60f;
        private static readonly Color Background = new Color(0.11f, 0.12f, 0.16f);

        private OrigamiApp _app;
        private Camera _camera;
        private bool _ownsCamera;
        private bool _hasPose;
        private bool _seated;
        private float _sitTime;
        private Vector3 _sitFromPos;
        private Quaternion _sitFromRot = Quaternion.identity;

        /// <summary>Transition finished: the rig is at the folding view.</summary>
        public bool InSeatedView => _seated && _sitTime >= SitTransition;

        /// <summary>Current pose of the rig (world).</summary>
        internal Vector3 Position { get; private set; }

        internal Quaternion Rotation { get; private set; } = Quaternion.identity;

        /// <summary>The controlled camera (null before the first Tick unless set with <see cref="UseCamera"/>).</summary>
        internal Camera Camera => _camera != null ? _camera : null;

        /// <summary>Near clip plane the rig applies (walking 0.1, seated 0.01).</summary>
        internal float NearClip => _seated ? SeatedNearClip : WalkNearClip;

        internal void Bind(OrigamiApp app)
        {
            _app = app;
        }

        /// <summary>Uses <paramref name="camera"/> instead of acquiring <c>Camera.main</c> (tests; null = acquire lazily again).</summary>
        internal void UseCamera(Camera camera)
        {
            _camera = camera;
            _ownsCamera = false;
            if (camera != null) Setup(camera);
        }

        /// <summary>Next Tick jumps straight to the walking target (teleport: new day, new run).</summary>
        internal void Snap()
        {
            _hasPose = false;
        }

        /// <summary>One frame (dt seconds, NaN/negative → 0). See the class remarks.</summary>
        public void Tick(float dt)
        {
            if (float.IsNaN(dt) || dt < 0f) dt = 0f;
            if (_app == null) return;
            EnsureCamera();

            bool seated = _app.Flow.Snapshot.Mode == WorkshopMode.Seated;
            if (seated && !_seated)
            {
                _seated = true;
                _sitTime = 0f;
                _sitFromPos = _hasPose ? Position : WalkTarget();
                _sitFromRot = _hasPose ? Rotation : WalkRotation;
            }
            else if (!seated && _seated)
            {
                _seated = false;
            }

            if (_seated)
            {
                _sitTime = Mathf.Min(SitTransition, _sitTime + dt);
                var table = _app.Table;
                Vector3 pos;
                Quaternion rot;
                if (table != null) table.GetSeatedCameraPose(out pos, out rot);
                else
                {
                    pos = WalkTarget();
                    rot = WalkRotation;
                }
                float k = SitTransition <= 0f ? 1f : Mathf.Clamp01(_sitTime / SitTransition);
                k = k * k * (3f - 2f * k);
                Position = Vector3.Lerp(_sitFromPos, pos, k);
                Rotation = Quaternion.Slerp(_sitFromRot, rot, k);
            }
            else if (!_hasPose)
            {
                Position = WalkTarget();
                Rotation = WalkRotation;
            }
            else
            {
                float f = 1f - Mathf.Exp(-FollowSharpness * dt);
                Position += (WalkTarget() - Position) * f;
                Rotation = Quaternion.Slerp(Rotation, WalkRotation, f);
            }
            _hasPose = true;
            Apply();
        }

        private static Quaternion WalkRotation => Quaternion.LookRotation(LookOffset - WalkOffset, Vector3.up);

        private Vector3 WalkTarget()
        {
            var c = _app.CharacterPosition;
            return new Vector3(c.x, 0f, c.y) + WalkOffset;
        }

        private void EnsureCamera()
        {
            if (_camera != null) return;
            var main = Camera.main;
            if (main != null)
            {
                _camera = main;
                _ownsCamera = false;
            }
            else
            {
                var go = new GameObject("Main Camera");
                go.tag = "MainCamera";
                _camera = go.AddComponent<Camera>();
                _ownsCamera = true;
            }
            Setup(_camera);
        }

        private static void Setup(Camera cam)
        {
            cam.orthographic = false;
            cam.fieldOfView = FieldOfView;
            cam.farClipPlane = FarClip;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Background;
        }

        private void Apply()
        {
            if (_camera == null) return;
            var t = _camera.transform;
            t.SetPositionAndRotation(Position, Rotation);
            _camera.nearClipPlane = NearClip;
        }

        private void OnDestroy()
        {
            if (_ownsCamera && _camera != null) Destroy(_camera.gameObject);
        }
    }
}
