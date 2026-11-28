using System.Collections.Generic;
using Origami.Core.Room;
using Origami.Runtime.Systems;
using UnityEngine;
using UnityEngine.Rendering;

namespace Origami.Runtime.Gameplay
{
    /// <summary>
    /// The character (C-193, CONTENT §6): the Quaternius Crafter model (<see cref="ArtKeys.Crafter"/>, ORIGAMI-019) fitted
    /// to <see cref="RoomLayout.CharacterHeight"/> by its renderer bounds, animated with its Legacy clips by
    /// <see cref="CharacterClips"/> (Idle/Walk cross-fade by speed, SitDown held on the chair). If the model cannot be loaded
    /// the old code-built figure is drawn instead (capsule body, sphere head, apron, nose; walking bobs it, sitting lowers it).
    /// <para>
    /// Measured at build time from the model itself (ARCHITECTURE §14.7): the height scale (bounds at scale 1), the front
    /// direction (foot → toe bones in the Idle pose, snapped to 90°; <see cref="ArtKeys.CrafterFrontYawFallback"/> if the
    /// bones are missing), the Walk clip's ground speed (foot travel over one cycle) and the hips of the SitDown end pose,
    /// from which <see cref="SitOffset"/> moves the model so the hips rest on the chair seat (<see cref="WorkshopView.ChairSeatHeight"/>).
    /// </para>
    /// No Update — <see cref="OrigamiApp"/> calls <see cref="SetPose"/> and then <see cref="Tick"/> with the frame's dt.
    /// </summary>
    public sealed class CharacterView : MonoBehaviour
    {
        private const float BodyRadius = 0.26f, BodyHeight = 1.25f, HeadRadius = 0.19f;
        private const float BobHeight = 0.04f, SitDrop = 0.42f;

        /// <summary>Hip joint above the seat surface, and how far behind the chair centre (towards the backrest) the hips sit.</summary>
        internal const float HipAboveSeat = 0.1f, SeatBack = 0.05f;

        /// <summary>Limits of the sit correction (a pose that needs more is used as is, documented in the README).</summary>
        private const float MaxSitLift = 0.35f, MaxSitDrop = 0.35f, MaxSitShift = 0.3f;

        /// <summary>
        /// Plausible range of (mesh top ÷ Head_end height) in the Idle pose. Outside it the mesh measurement is not trusted and
        /// the height comes from the head bone × <see cref="HeadEndToTop"/> (Crafter.fbx: mesh top 3.307 / Head_end 2.77 units).
        /// </summary>
        internal const float MinTopToHead = 1.0f, MaxTopToHead = 1.5f, HeadEndToTop = 1.19f;

        /// <summary>While seated, the model only casts shadows when the camera is closer than this to the hips–head line.</summary>
        internal const float CameraClearance = 0.3f;

        private const int StrideSamples = 24;
        private const string HipsBone = "Hips", HeadBone = "Head", HeadEndBone = "Head_end";
        private static readonly string[] FootBones = { "Foot.L", "Foot.R" };

        private static readonly Color BodyColor = new Color(0.36f, 0.52f, 0.78f);
        private static readonly Color HeadColor = new Color(0.98f, 0.84f, 0.7f);
        private static readonly Color ApronColor = new Color(0.93f, 0.62f, 0.32f);
        private static readonly Color NoseColor = new Color(0.9f, 0.55f, 0.45f);

        private readonly List<Mesh> _meshes = new List<Mesh>();
        private readonly List<Material> _materials = new List<Material>();
        private readonly List<Renderer> _renderers = new List<Renderer>();
        private Transform _body;
        private GameObject _model;
        private CharacterClips _clips;
        private Transform _hips, _head;
        private float _walkPhase;
        private bool _built;

        /// <summary>Load the Crafter model (set before the component wakes, e.g. on an inactive GameObject; tests use false for the fallback).</summary>
        internal bool LoadModel { get; set; } = true;

        /// <summary>World (x, z) of the last pose.</summary>
        internal Vector2 Position { get; private set; }

        internal float Yaw { get; private set; }

        internal bool Sitting { get; private set; }

        /// <summary>The Crafter model is shown (false = code-built fallback).</summary>
        internal bool UsesModel => _model != null;

        /// <summary>The model wrapper (null for the fallback).</summary>
        internal GameObject Model => _model != null ? _model : null;

        /// <summary>The model's Legacy Animation (null without model or clips).</summary>
        internal Animation Animation => _clips != null ? _clips.Animation : null;

        /// <summary>Dominant clip ("Idle", "Walk", "SitDown"); null for the fallback or a model without clips.</summary>
        internal string CurrentClip => _clips != null ? _clips.Current : null;

        internal float WalkWeight => _clips != null ? _clips.WalkWeight : 0f;

        /// <summary>Measured front of the model (degrees about +y in the model's own frame; the wrapper turns by −FrontYaw).</summary>
        internal float FrontYaw { get; private set; }

        /// <summary>The front was measured from the foot bones (false = fallback constant).</summary>
        internal bool FrontMeasured { get; private set; }

        /// <summary>Bounds height of the model at scale 1 (file units) and the uniform scale applied.</summary>
        internal float NativeHeight { get; private set; }

        internal float ModelScale { get; private set; }

        /// <summary>Head_end height in the Idle pose at scale 1 (file units), and where the height came from ("mesh" / "head bone").</summary>
        internal float HeadNativeHeight { get; private set; }

        internal string HeightSource { get; private set; }

        /// <summary>Correction (view-local, metres) applied to the model at the end of SitDown so the hips rest on the seat.</summary>
        internal Vector3 SitOffset { get; private set; }

        /// <summary>Hips in the SitDown end pose and in the Idle pose, view-local, without the correction (diagnostics).</summary>
        internal Vector3 SitHips { get; private set; }

        internal Vector3 IdleHips { get; private set; }

        internal float ClipGroundSpeed => _clips != null ? _clips.ClipGroundSpeed : 0f;

        /// <summary>The model only casts shadows because the seated camera is inside it.</summary>
        internal bool CameraCutaway { get; private set; }

        internal Transform HipsBoneTransform => _hips != null ? _hips : null;

        internal Transform HeadBoneTransform => _head != null ? _head : null;

        /// <summary>Places the character: world (x, z), yaw in degrees (0 = north, 90 = east), sitting, walk phase (radians, fallback bob).</summary>
        public void SetPose(Vector2 xz, float yaw, bool sitting, float walkPhase)
        {
            Build();
            Position = xz;
            Yaw = yaw;
            Sitting = sitting;
            _walkPhase = walkPhase;
            transform.SetPositionAndRotation(new Vector3(xz.x, 0f, xz.y), Quaternion.Euler(0f, yaw, 0f));
            ApplyBody();
        }

        /// <summary>
        /// Advances the animation by <paramref name="dt"/> seconds at ground <paramref name="speed"/> (m/s) for the pose of the
        /// last <see cref="SetPose"/>. Deterministic (no Time.*); NaN/negative values count as 0. No effect for the fallback figure.
        /// </summary>
        public void Tick(float dt, float speed)
        {
            Build();
            if (_clips != null) _clips.Tick(dt, speed, Sitting);
            ApplyBody();
        }

        /// <summary>
        /// Seated only: hides the model (shadows only) while <paramref name="cameraPosition"/> (world) is within
        /// <see cref="CameraClearance"/> of the hips–head line, so an orbiting/zoomed folding view never looks out of the head.
        /// </summary>
        internal void ApplyCameraCutaway(Vector3 cameraPosition)
        {
            Build();
            bool hide = false;
            if (Sitting)
            {
                Vector3 a, b;
                if (_hips != null && _head != null)
                {
                    a = _hips.position;
                    b = _head.position;
                }
                else
                {
                    a = transform.position + Vector3.up * 0.5f;
                    b = transform.position + Vector3.up * ((float)RoomLayout.CharacterHeight - SitDrop);
                }
                hide = DistanceToSegment(cameraPosition, a, b) < CameraClearance;
            }
            if (hide == CameraCutaway) return;
            CameraCutaway = hide;
            foreach (var r in _renderers)
                if (r != null) r.shadowCastingMode = hide ? ShadowCastingMode.ShadowsOnly : ShadowCastingMode.On;
        }

        private void Awake() => Build();

        private void Build()
        {
            if (_built) return;
            _built = true;
            var body = new GameObject("Body");
            body.transform.SetParent(transform, false);
            _body = body.transform;
            if (!(LoadModel && BuildModel())) BuildCode();
        }

        private void ApplyBody()
        {
            if (_body == null) return;
            if (_model != null)
            {
                if (!Sitting)
                {
                    _body.localPosition = Vector3.zero;
                    return;
                }
                float k = _clips != null ? _clips.SitProgress : 1f;
                var o = SitOffset;
                _body.localPosition = new Vector3(o.x, o.y * k, o.z);
                return;
            }
            float y = Sitting ? -SitDrop : Mathf.Abs(Mathf.Sin(_walkPhase)) * BobHeight;
            _body.localPosition = new Vector3(0f, y, 0f);
        }

        // ---------------------------------------------------------------- model

        private bool BuildModel()
        {
            var model = ModelFit.Spawn(ArtKeys.Crafter, _body, "Crafter", FitSpec.Height((float)RoomLayout.CharacterHeight),
                Vector3.zero, 0f, anchorOrigin: true);
            if (model == null) return false;
            _model = model;
            ModelScale = model.transform.localScale.y;
            NativeHeight = ModelScale > 0f ? (float)RoomLayout.CharacterHeight / ModelScale : 0f;
            HeightSource = "mesh";
            foreach (var r in model.GetComponentsInChildren<Renderer>(true))
            {
                r.shadowCastingMode = ShadowCastingMode.On;
                r.receiveShadows = true;
                // The imported localBounds are 100x too large (F-1); one character, so let Unity recompute them from the pose.
                if (r is SkinnedMeshRenderer smr) smr.updateWhenOffscreen = true;
                _renderers.Add(r);
            }
            _hips = FindBone(model.transform, HipsBone);
            _head = FindBone(model.transform, HeadEndBone);
            if (_head == null) _head = FindBone(model.transform, HeadBone);

            _clips = CharacterClips.Create(model.GetComponentInChildren<Animation>(true));
            if (_clips != null) _clips.Pose(ArtKeys.ClipIdle, 0f);
            CheckHeightAgainstHead(model.transform);

            MeasureFront(model.transform);
            if (_clips != null)
            {
                MeasureStride(model.transform);
                MeasureSeat();
                _clips.Pose(ArtKeys.ClipIdle, 0f);
            }
            return true;
        }

        /// <summary>
        /// Cross-check of the bounds-based height with the posed skeleton (Idle t = 0): the mesh top must be 1.0–1.5× the
        /// Head_end height. Otherwise (degenerate bounds) the native height = Head_end height × <see cref="HeadEndToTop"/>.
        /// </summary>
        private void CheckHeightAgainstHead(Transform model)
        {
            var head = FindBone(model, HeadEndBone);
            if (head == null || !(ModelScale > 0f)) return;
            HeadNativeHeight = transform.InverseTransformPoint(head.position).y / ModelScale;
            if (!(HeadNativeHeight > 1e-4f)) return;
            float ratio = NativeHeight / HeadNativeHeight;
            if (ratio >= MinTopToHead && ratio <= MaxTopToHead) return;
            NativeHeight = HeadNativeHeight * HeadEndToTop;
            ModelScale = (float)RoomLayout.CharacterHeight / NativeHeight;
            model.localScale = Vector3.one * ModelScale;
            HeightSource = "head bone";
        }

        /// <summary>Front = mean foot → toe direction of the current (Idle) pose, in the model's frame, snapped to 90°.</summary>
        private void MeasureFront(Transform model)
        {
            FrontYaw = ArtKeys.CrafterFrontYawFallback;
            FrontMeasured = false;
            var sum = Vector3.zero;
            foreach (var name in FootBones)
            {
                var foot = FindBone(model, name);
                var toe = FindBone(model, name + "_end");
                if (foot == null || toe == null) continue;
                sum += model.InverseTransformDirection(toe.position - foot.position);
            }
            sum.y = 0f;
            if (sum.sqrMagnitude > 1e-10f)
            {
                float yaw = Mathf.Atan2(sum.x, sum.z) * Mathf.Rad2Deg;
                FrontYaw = Mathf.Repeat(Mathf.Round(yaw / 90f) * 90f, 360f);
                FrontMeasured = true;
            }
            model.localRotation = Quaternion.Euler(0f, -FrontYaw, 0f);
        }

        /// <summary>Walk ground speed ≈ 2 × the foot's forward travel over one cycle / clip length (in place clip).</summary>
        private void MeasureStride(Transform model)
        {
            var foot = FindBone(model, FootBones[0]);
            if (foot == null || !_clips.HasWalk) return;
            float min = float.PositiveInfinity, max = float.NegativeInfinity, length = 0f;
            for (int i = 0; i < StrideSamples; i++)
            {
                if (!_clips.Pose(ArtKeys.ClipWalk, i / (float)StrideSamples)) return;
                float z = transform.InverseTransformPoint(foot.position).z;
                min = Mathf.Min(min, z);
                max = Mathf.Max(max, z);
            }
            length = _clips.WalkLength;
            float travel = max - min;
            if (length > 0f && travel > 0.05f) _clips.ClipGroundSpeed = 2f * travel / length;
        }

        /// <summary>Sit correction from the SitDown end pose: hips onto the seat, slightly behind the chair centre.</summary>
        private void MeasureSeat()
        {
            SitOffset = new Vector3(0f, -SitDrop, 0f);
            if (_hips == null) return;
            _clips.Pose(ArtKeys.ClipIdle, 0f);
            IdleHips = transform.InverseTransformPoint(_hips.position);
            if (!_clips.HasSit) return;
            _clips.Pose(ArtKeys.ClipSitDown, 1f);
            SitHips = transform.InverseTransformPoint(_hips.position);
            var desired = new Vector3(0f, WorkshopView.ChairSeatHeight + HipAboveSeat, -SeatBack);
            var d = desired - SitHips;
            d.y = Mathf.Clamp(d.y, -MaxSitDrop, MaxSitLift);
            var flat = Vector2.ClampMagnitude(new Vector2(d.x, d.z), MaxSitShift);
            SitOffset = new Vector3(flat.x, d.y, flat.y);
        }

        private static Transform FindBone(Transform root, string name)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t;
            return null;
        }

        private static float DistanceToSegment(Vector3 p, Vector3 a, Vector3 b)
        {
            var ab = b - a;
            float len2 = ab.sqrMagnitude;
            float t = len2 > 1e-8f ? Mathf.Clamp01(Vector3.Dot(p - a, ab) / len2) : 0f;
            return (p - (a + ab * t)).magnitude;
        }

        // ---------------------------------------------------------------- fallback

        private void BuildCode()
        {
            Part("Torso", MeshFactory.Capsule(BodyRadius, BodyHeight), BodyColor, new Vector3(0f, BodyHeight * 0.5f, 0f));
            Part("Apron", MeshFactory.Box(new Vector3(BodyRadius * 1.7f, 0.5f, 0.06f)), ApronColor, new Vector3(0f, 0.55f, BodyRadius * 0.92f));
            float headY = (float)RoomLayout.CharacterHeight - HeadRadius;
            Part("Head", MeshFactory.Sphere(HeadRadius), HeadColor, new Vector3(0f, headY, 0f));
            Part("Nose", MeshFactory.Sphere(0.045f), NoseColor, new Vector3(0f, headY, HeadRadius * 0.95f));
        }

        private void Part(string name, Mesh mesh, Color color, Vector3 localPos)
        {
            _meshes.Add(mesh);
            var mat = Materials.Lit(color);
            _materials.Add(mat);
            var go = new GameObject(name);
            go.transform.SetParent(_body, false);
            go.transform.localPosition = localPos;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.On;
            _renderers.Add(r);
        }

        private void OnDestroy()
        {
            foreach (var m in _meshes)
                if (m != null) Destroy(m);
            foreach (var m in _materials)
                if (m != null) Destroy(m);
            _meshes.Clear();
            _materials.Clear();
        }
    }
}
