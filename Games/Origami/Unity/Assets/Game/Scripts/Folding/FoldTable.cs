using System;
using System.Collections.Generic;
using Origami.Core.Folding;
using Origami.Core.Geometry;
using Origami.Runtime.Systems;
using UnityEngine;
using UnityEngine.Rendering;

namespace Origami.Runtime.Folding
{
    /// <summary>
    /// The folding table view (C-191, F-005, F-006 AC-09; ported from Fold3DSpike <c>FoldSpikeController</c>): paper
    /// meshes on the table, drag input with grab/drop magnets (<see cref="FoldGesture"/>), layer choice, preview (lifted
    /// moving facets + ghost of the result), fold / crease / flip animations, crease lines, magnet highlights, guide
    /// markers and the seated orbit camera pose.
    /// <para>
    /// No <c>Update</c>/<c>OnGUI</c>: the owner (<c>OrigamiApp</c>, task 014; tests directly) calls
    /// <see cref="ProcessFrame"/> with the frame's input and dt (A2-5). Commands go only through
    /// <see cref="IFoldTableHost"/> (A2-7). A fold is applied by the host the moment the button is released; the view only
    /// animates the plan afterwards (A2-4), so the host state is always the truth.
    /// </para>
    /// <para>
    /// Space: a child "PaperSpace" at the table top (default (2.5, 0.75, 1.6)) with uniform scale <see cref="PaperScale"/>;
    /// paper (x, y) → local (x, layer height, y). No colliders, primitives or physics: picking is ray–plane math
    /// (<see cref="ScreenToPaper"/>).
    /// </para>
    /// </summary>
    public sealed class FoldTable : MonoBehaviour
    {
        public const string ObjectName = "FoldTable";
        public const float PaperScale = 0.1f;
        public const float FoldDuration = 0.4f, CreaseDuration = 0.6f, FlipDuration = 0.6f;

        private static readonly Vector3 DefaultTablePose = new Vector3(2.5f, 0.75f, 1.6f);

        // Camera orbit (paper units, Fold3DSpike values).
        private const float DefaultPitch = 58f, MinPitch = 10f, MaxPitch = 89f, OrbitDegreesPerPixel = 0.3f;
        private const float DefaultDistance = 4.4f, MinDistance = 1.5f, MaxDistance = 10f, ZoomPerNotch = 0.1f, MaxZoomNotches = 3f;

        // Preview / animation.
        private const float PreviewLiftAngle = 24f;
        private const float GhostOffset = 0.004f;
        private const float LiftClearance = 0.0008f;
        private const float CreaseHalfWidth = 0.006f;
        private const float OverlayAbovePaper = 0.003f;
        private const float PulseSpeed = 5f;
        private const float TimeWrap = 1000f;

        private static readonly Color FrontColor = new Color(0.2f, 0.5f, 0.93f);
        private static readonly Color BackColor = new Color(0.97f, 0.96f, 0.93f);
        private static readonly Color EdgeColor = new Color(0.12f, 0.13f, 0.2f, 0.6f);
        private static readonly Color CreaseMarkColor = new Color(0.45f, 0.45f, 0.48f, 0.85f);
        private static readonly Color GhostColor = new Color(1f, 0.88f, 0.3f, 0.32f);
        private static readonly Color GhostEdgeColor = new Color(1f, 0.7f, 0.15f, 0.9f);
        private static readonly Color ValleyColor = new Color(1f, 0.5f, 0.05f, 1f);
        private static readonly Color MountainColor = new Color(0.62f, 0.3f, 1f, 1f);
        private static readonly Color CreasePreviewColor = new Color(0.85f, 0.85f, 0.9f, 1f);
        private static readonly Color GuideCreaseColor = new Color(0.95f, 0.2f, 0.35f, 0.9f);
        private static readonly Color GuideArrowColor = new Color(1f, 0.72f, 0.05f, 0.95f);
        private static readonly Color TargetColor = new Color(0.15f, 0.85f, 0.4f, 0.95f);
        private static readonly Color DragLineColor = new Color(1f, 1f, 1f, 0.7f);

        private enum AnimKind { Fold, Crease, Flip }

        private sealed class Anim
        {
            public AnimKind Kind;
            public IReadOnlyList<Facet> Moving;
            public IReadOnlyList<Facet> Static;
            public PaperState StaticCreases;   // crease lines drawn on the static part (null = none)
            public FoldLine Line;
            public float Y0, Sigma, From, Duration, T;
        }

        private static readonly IReadOnlyList<Facet> NoFacets = new Facet[0];

        private bool _built;
        private Transform _space, _paperRoot;
        private Mesh _paperMesh, _flapMesh, _ghostMesh, _overlayMesh, _creaseMesh, _sphereMesh;
        private GameObject _flapGo, _ghostGo;
        private Transform _guideBall, _hoverBall, _cursorBall;
        private Material _hoverMat, _cursorMat;
        private readonly List<Material> _materials = new List<Material>();
        private readonly OverlayBuilder _overlay = new OverlayBuilder();
        private readonly OverlayBuilder _creaseOverlay = new OverlayBuilder();

        private IFoldTableHost _host;
        private int _seenSerial;
        private PaperState _state;          // host sheet currently represented (null = no sheet)

        private FoldGesture _gesture;
        private PaperState _dragSheet;
        private FoldPlan _plan;
        private bool _shift, _ctrl;
        private FoldPlan _ownPlan;          // set only while our own host call is being synced
        private Anim _anim;

        private FoldPlan _previewCreasePlan;
        private PaperState _previewCreaseState;

        private object _staticKey;
        private PaperState _staticCreaseKey;
        private bool _staticCached;
        private float _staticMinY;

        private bool _hoverValid, _hoverCorner;
        private Vec2 _hoverPoint;

        private float _yaw;
        private float _pitch = DefaultPitch;
        private float _dist = DefaultDistance;
        private float _time;

        /// <summary>A drag is in progress (<see cref="Gesture"/> is not null).</summary>
        public bool IsDragging => _gesture != null;

        /// <summary>A fold / crease / flip animation is playing (no new drag starts).</summary>
        public bool IsAnimating => _anim != null;

        /// <summary>The current drag, null when not dragging.</summary>
        public FoldGesture Gesture => _gesture;

        /// <summary>While dragging: Ctrl → <see cref="FoldTexts.CreaseLabel"/>, else <see cref="FoldTexts.LayerLabel"/>. Otherwise "".</summary>
        public string CursorLabel =>
            _gesture == null ? "" : (_ctrl ? FoldTexts.CreaseLabel : FoldTexts.LayerLabel(_gesture.Layers, _gesture.StackCount));

        /// <summary>Drop magnet kind of the current drag (green highlight), None when not dragging.</summary>
        public SnapKind CurrentSnap => _gesture != null ? _gesture.Drop.Kind : SnapKind.None;

        /// <summary>The yellow ball / green ring / red dashes were rendered in the last update (host guide shown, not animating).</summary>
        public bool GuideMarkersVisible { get; private set; }

        /// <summary>Vertex count of the static paper mesh (for tests).</summary>
        public int PaperVertexCount => _paperMesh != null ? _paperMesh.vertexCount : 0;

        /// <summary>Crease line strips drawn on the static paper (visible parts of <see cref="DropSnap.MarkPieces"/>).</summary>
        public int CreaseLineCount { get; private set; }

        /// <summary>Reason of the last release that did not fold (toast), None after a successful fold or a new drag.</summary>
        public FoldError LastError { get; private set; }

        // ------------------------------------------------------------------ public API

        /// <summary>Connects the host (null = disconnect). Cancels any drag/animation and shows the host's sheet at once.</summary>
        public void Bind(IFoldTableHost host)
        {
            EnsureBuilt();
            CancelDrag();
            _anim = null;
            _ownPlan = null;
            _host = host;
            _seenSerial = host != null ? host.ChangeSerial : 0;
            _state = host != null ? host.Sheet : null;
            LastError = FoldError.None;
            _hoverValid = false;
            Render();
            RefreshOverlay();
        }

        /// <summary>World position of the paper centre on the table top (default (2.5, 0.75, 1.6)).</summary>
        public void SetTablePose(Vector3 tableTopCenter)
        {
            EnsureBuilt();
            _space.position = tableTopCenter;
        }

        /// <summary>
        /// One frame: host sync, camera orbit/zoom, animation (dt), drag input, preview, meshes and overlay.
        /// Negative/NaN dt counts as 0.
        /// </summary>
        public void ProcessFrame(PointerFrame pointer, float dt)
        {
            EnsureBuilt();
            if (float.IsNaN(dt) || dt < 0f) dt = 0f;
            if (float.IsInfinity(dt)) dt = TimeWrap;
            _time += dt;
            if (_time > TimeWrap) _time %= TimeWrap;

            SyncHost();
            UpdateCamera(pointer);

            if (_anim != null)
            {
                _anim.T += dt;
                if (_anim.T >= _anim.Duration) _anim = null;
            }

            bool enabled = _host != null && _host.InputEnabled && _state != null;
            if (!enabled)
            {
                CancelDrag();
                _hoverValid = false;
            }
            else if (_gesture != null)
            {
                DragFrame(pointer);
            }
            else if (_anim == null)
            {
                IdleFrame(pointer);
            }
            else
            {
                _hoverValid = false;
            }

            Render();
            RefreshOverlay();
        }

        /// <summary>
        /// Paper point under <paramref name="screenPosition"/> (pixels): ray–plane intersection with the plane at the top
        /// layer's height. False when there is no camera or the ray does not go down onto the plane.
        /// </summary>
        public bool ScreenToPaper(Camera camera, Vector2 screenPosition, out Vector2 paperPoint)
        {
            paperPoint = default;
            EnsureBuilt();
            if (camera == null) return false;
            var ray = camera.ScreenPointToRay(new Vector3(screenPosition.x, screenPosition.y, 0f));
            var up = _space.up;
            if (Vector3.Dot(ray.direction, up) > -1e-4f) return false;
            float h = PaperMeshBuilder.LayerTop(_state != null ? _state.MaxLayer() : 0);
            var plane = new Plane(up, _space.TransformPoint(new Vector3(0f, h, 0f)));
            if (!plane.Raycast(ray, out float enter)) return false;
            var local = _space.InverseTransformPoint(ray.GetPoint(enter));
            paperPoint = new Vector2(local.x, local.z);
            return true;
        }

        /// <summary>World pose of the seated camera: orbit (right drag) and zoom (wheel when not dragging) around the paper centre.</summary>
        public void GetSeatedCameraPose(out Vector3 position, out Quaternion rotation)
        {
            EnsureBuilt();
            rotation = Quaternion.Euler(_pitch, _yaw, 0f);
            position = _space.position + rotation * new Vector3(0f, 0f, -_dist * PaperScale);
        }

        /// <summary>Ends a running (or just reported) animation and shows the host's current sheet.</summary>
        public void FinishAnimationNow()
        {
            EnsureBuilt();
            SyncHost();
            _anim = null;
            Render();
            RefreshOverlay();
        }

        // ------------------------------------------------------------------ setup

        private void Awake()
        {
            EnsureBuilt();
        }

        private void OnDestroy()
        {
            foreach (var m in _materials)
                if (m != null) Destroy(m);
            _materials.Clear();
            DestroyMesh(_paperMesh);
            DestroyMesh(_flapMesh);
            DestroyMesh(_ghostMesh);
            DestroyMesh(_overlayMesh);
            DestroyMesh(_creaseMesh);
            DestroyMesh(_sphereMesh);
        }

        private static void DestroyMesh(Mesh m)
        {
            if (m != null) Destroy(m);
        }

        private void EnsureBuilt()
        {
            if (_built) return;
            _built = true;

            var space = new GameObject("PaperSpace");
            space.transform.SetParent(transform, false);
            _space = space.transform;
            _space.position = DefaultTablePose;
            _space.localScale = Vector3.one * PaperScale;

            var root = new GameObject("Paper");
            root.transform.SetParent(_space, false);
            _paperRoot = root.transform;

            var paperMats = new[] { Track(Materials.Lit(FrontColor)), Track(Materials.Lit(BackColor)), Track(Materials.Unlit(Color.white)) };
            _paperMesh = PaperMeshBuilder.NewMesh("PaperStatic");
            MakeRenderer("Static", _paperRoot, _paperMesh, paperMats, true);
            _flapMesh = PaperMeshBuilder.NewMesh("PaperFlap");
            _flapGo = MakeRenderer("Flap", _paperRoot, _flapMesh, paperMats, true);
            _flapGo.SetActive(false);
            _creaseMesh = PaperMeshBuilder.NewMesh("Creases");
            MakeRenderer("Creases", _paperRoot, _creaseMesh, new[] { Track(Materials.Unlit(Color.white)) }, false);

            var ghostMat = Track(Materials.Unlit(GhostColor));
            _ghostMesh = PaperMeshBuilder.NewMesh("Ghost");
            _ghostGo = MakeRenderer("Ghost", _space, _ghostMesh, new[] { ghostMat, ghostMat, Track(Materials.Unlit(Color.white)) }, false);
            _ghostGo.SetActive(false);

            _overlayMesh = PaperMeshBuilder.NewMesh("Overlay");
            MakeRenderer("Overlay", _space, _overlayMesh, new[] { Track(Materials.Unlit(Color.white)) }, false);

            _sphereMesh = SphereMesh.Create();
            _guideBall = MakeBall("GuideBall", Track(Materials.Lit(GuideArrowColor)));
            _hoverMat = Track(Materials.Lit(Color.white));
            _hoverBall = MakeBall("HoverBall", _hoverMat);
            _cursorMat = Track(Materials.Lit(ValleyColor));
            _cursorBall = MakeBall("CursorBall", _cursorMat);

            _staticCached = false;
            Render();
        }

        private Material Track(Material m)
        {
            _materials.Add(m);
            return m;
        }

        private static GameObject MakeRenderer(string name, Transform parent, Mesh mesh, Material[] mats, bool shadows)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterials = mats;
            r.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            r.receiveShadows = shadows;
            return go;
        }

        private Transform MakeBall(string name, Material mat)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_space, false);
            go.AddComponent<MeshFilter>().sharedMesh = _sphereMesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            go.SetActive(false);
            return go.transform;
        }

        // ------------------------------------------------------------------ host sync

        private void SyncHost()
        {
            if (_host == null)
            {
                if (_state != null || _anim != null)
                {
                    _state = null;
                    _anim = null;
                }
                return;
            }

            var sheet = _host.Sheet;
            int serial = _host.ChangeSerial;
            if (serial != _seenSerial)
            {
                _seenSerial = serial;
                var old = _state;
                _state = sheet;
                _anim = null;   // a new change ends the running animation (ARCHITECTURE §6)
                if (_gesture != null && !ReferenceEquals(sheet, _dragSheet)) CancelDrag();
                if (sheet == null) return;

                var kind = _host.ChangeKind;
                if (_ownPlan != null && kind == (_ownPlan.IsCrease ? FoldTableChangeKind.Creased : FoldTableChangeKind.Folded))
                {
                    StartPlanAnimation(_ownPlan, old);
                }
                else if (kind == FoldTableChangeKind.Flipped && _host.ChangePrevious != null)
                {
                    StartFlipAnimation(_host.ChangePrevious);
                }
                return;
            }

            if (!ReferenceEquals(sheet, _state))
            {
                // Replaced without a serial change: show it at once.
                _state = sheet;
                _anim = null;
                if (_gesture != null && !ReferenceEquals(sheet, _dragSheet)) CancelDrag();
            }
        }

        // ------------------------------------------------------------------ input

        private void UpdateCamera(PointerFrame p)
        {
            if (p.RightHeld)
            {
                _yaw += p.RightDragDelta.x * OrbitDegreesPerPixel;
                _pitch = Mathf.Clamp(_pitch - p.RightDragDelta.y * OrbitDegreesPerPixel, MinPitch, MaxPitch);
                if (_yaw > 360f || _yaw < -360f) _yaw %= 360f;
            }
            if (_gesture == null && !p.OverGui && (p.Wheel > 0f || p.Wheel < 0f))
            {
                float notches = Mathf.Clamp(p.Wheel, -MaxZoomNotches, MaxZoomNotches);
                _dist = Mathf.Clamp(_dist * (1f - notches * ZoomPerNotch), MinDistance, MaxDistance);
            }
        }

        private void IdleFrame(PointerFrame p)
        {
            var sheet = _state;
            _hoverValid = p.HasPaperPoint && !p.OverGui &&
                          sheet.TrySnapGrab(ToVec2(p.PaperPoint), FoldGesture.GrabCornerRadius, FoldGesture.GrabEdgeRadius,
                              out _hoverPoint, out _hoverCorner);
            if (!(p.LeftDown && p.HasPaperPoint && !p.OverGui)) return;

            var gesture = FoldGesture.TryBegin(sheet, ToVec2(p.PaperPoint));
            if (gesture == null) return;
            _gesture = gesture;
            _dragSheet = sheet;
            _plan = null;
            _shift = p.Shift;
            _ctrl = p.Ctrl;
            _hoverValid = false;
            LastError = FoldError.None;
        }

        private void DragFrame(PointerFrame p)
        {
            var sheet = _state;
            if (!ReferenceEquals(sheet, _dragSheet) || p.Escape)
            {
                CancelDrag();
                return;
            }

            // Layer choice: one step per frame by the sign of the wheel / the bracket keys (A2-8, review N-3).
            int wheel = p.Wheel > 0f ? 1 : (p.Wheel < 0f ? -1 : 0);   // NaN → 0
            int notches = wheel + (p.LayerUp ? 1 : 0) - (p.LayerDown ? 1 : 0);
            if (notches > 1) notches = 1;
            if (notches < -1) notches = -1;
            if (notches != 0) _gesture.CycleLayers(notches);

            if (p.HasPaperPoint)
            {
                var pt = ToVec2(p.PaperPoint);
                if (_host.TryGetGuide(out var guide)) _gesture.MoveTo(sheet, pt, guide.Drop);
                else _gesture.MoveTo(sheet, pt);
            }
            _shift = p.Shift;
            _ctrl = p.Ctrl;
            _plan = _gesture.Preview(sheet, _shift, _ctrl);

            if (p.LeftUp || !p.LeftHeld) Release();
        }

        private void Release()
        {
            var plan = _plan;
            CancelDrag();
            if (plan == null) return;
            if (!plan.Valid)
            {
                LastError = plan.Error;
                return;
            }

            // Same arguments as the preview (C-107), so the host's result equals the preview.
            FoldError error;
            bool ok = plan.IsCrease
                ? _host.Crease(plan.Grab, plan.Drop, plan.Layers, out error)
                : _host.Fold(plan.Grab, plan.Drop, plan.Mountain, plan.Layers, out error);
            if (!ok)
            {
                LastError = error;
                return;
            }
            LastError = FoldError.None;
            _ownPlan = plan;
            try
            {
                SyncHost();
            }
            finally
            {
                _ownPlan = null;
            }
        }

        private void CancelDrag()
        {
            _gesture = null;
            _dragSheet = null;
            _plan = null;
            _shift = false;
            _ctrl = false;
            _previewCreasePlan = null;
            _previewCreaseState = null;
        }

        private static Vec2 ToVec2(Vector2 v) => new Vec2(v.x, v.y);

        private static Vector3 V3(Vec2 p, float y) => new Vector3((float)p.X, y, (float)p.Y);

        // ------------------------------------------------------------------ animation

        private void StartPlanAnimation(FoldPlan plan, PaperState before)
        {
            bool crease = plan.IsCrease;
            bool mountain = plan.Mountain && !crease;
            _anim = new Anim
            {
                Kind = crease ? AnimKind.Crease : AnimKind.Fold,
                Moving = plan.Moving,
                Static = plan.Static,
                StaticCreases = before != null && before.Creases.Count > 0 && plan.Static.Count > 0
                    ? new PaperState(plan.Static, before.Creases)
                    : null,
                Line = plan.Line,
                Y0 = AxisHeight(plan.Static, plan.Moving, mountain),
                Sigma = mountain ? -1f : 1f,
                From = crease ? 0f : PreviewLiftAngle,
                Duration = crease ? CreaseDuration : FoldDuration,
            };
        }

        private void StartFlipAnimation(PaperState previous)
        {
            float cx = (float)previous.FlipAxisX();
            int max = previous.MaxLayer();
            _anim = new Anim
            {
                Kind = AnimKind.Flip,
                Moving = previous.Facets,
                Static = NoFacets,
                StaticCreases = null,
                Line = new FoldLine(new Vec2(cx, 0), new Vec2(1, 0)),
                Y0 = 0.5f * (PaperMeshBuilder.LayerTop(max) + PaperMeshBuilder.LayerBottom(0)),
                Sigma = 1f,
                From = 0f,
                Duration = FlipDuration,
            };
        }

        private static float AnimAngle(Anim a)
        {
            float k = a.Duration <= 0f ? 1f : Mathf.Clamp01(a.T / a.Duration);
            if (a.Kind == AnimKind.Crease)
            {
                float tri = k < 0.5f ? 2f * k : 2f - 2f * k;   // fold up, then open again
                return 180f * Smooth(tri);
            }
            return Mathf.Lerp(a.From, 180f, Smooth(k));
        }

        private static float Smooth(float k) => k * k * (3f - 2f * k);

        private static float Lift(float minY) => Mathf.Max(0f, LiftClearance - minY);

        /// <summary>Hinge height: just above the stack for valley folds, just below it for mountain folds.</summary>
        private static float AxisHeight(IReadOnlyList<Facet> stat, IReadOnlyList<Facet> moving, bool mountain)
        {
            int max = int.MinValue, min = int.MaxValue;
            foreach (var f in stat)
            {
                max = Math.Max(max, f.Layer);
                min = Math.Min(min, f.Layer);
            }
            foreach (var f in moving)
            {
                max = Math.Max(max, f.Layer);
                min = Math.Min(min, f.Layer);
            }
            if (max == int.MinValue) return 0f;
            return mountain
                ? PaperMeshBuilder.LayerBottom(min) - PaperMeshBuilder.LayerGap * 0.5f
                : PaperMeshBuilder.LayerTop(max) + PaperMeshBuilder.LayerGap * 0.5f;
        }

        /// <summary>
        /// Rotation of the flap about the crease (at height <paramref name="y0"/>) by <paramref name="angleDeg"/>.
        /// sigma +1 = valley (swings up), −1 = mountain (swings under). At 180° the result is the mirror image across
        /// the crease with the layer order reversed.
        /// </summary>
        private static VertexXform Rotation(FoldLine line, float y0, float sigma, float angleDeg)
        {
            double rad = angleDeg * Math.PI / 180.0;
            double c = Math.Cos(rad), sn = Math.Sin(rad);
            var normal = line.Normal;
            return (x, z, y) =>
            {
                var p = new Vec2(x, z);
                double s = line.RawSide(p);
                var foot = p - normal * s;
                double v = y - y0;
                double s2 = s * c + sigma * v * sn;
                double v2 = -sigma * s * sn + v * c;
                var q = foot + normal * s2;
                return new Vector3((float)q.X, (float)(y0 + v2), (float)q.Y);
            };
        }

        // ------------------------------------------------------------------ rendering

        private void Render()
        {
            if (!_built) return;
            if (_anim != null)
            {
                SetStatic(_anim.Static, _anim.StaticCreases);
                float flapMin = PaperMeshBuilder.Build(_flapMesh, _anim.Moving,
                    Rotation(_anim.Line, _anim.Y0, _anim.Sigma, AnimAngle(_anim)), false, true, 0f, EdgeColor);
                _flapGo.SetActive(true);
                _ghostGo.SetActive(false);
                _paperRoot.localPosition = new Vector3(0f, Lift(Mathf.Min(flapMin, _staticMinY)), 0f);
                return;
            }

            if (_gesture != null && _plan != null && _plan.Valid)
            {
                if (!ReferenceEquals(_previewCreasePlan, _plan))
                {
                    _previewCreasePlan = _plan;
                    _previewCreaseState = _state.Creases.Count > 0 ? new PaperState(_plan.Static, _state.Creases) : null;
                }
                SetStatic(_plan.Static, _previewCreaseState);
                bool mountain = _plan.Mountain && !_plan.IsCrease;
                float sigma = mountain ? -1f : 1f;
                float y0 = AxisHeight(_plan.Static, _plan.Moving, mountain);
                float flapMin = PaperMeshBuilder.Build(_flapMesh, _plan.Moving, Rotation(_plan.Line, y0, sigma, PreviewLiftAngle),
                    false, true, 0f, EdgeColor);
                _flapGo.SetActive(true);
                if (_plan.Moved.Count > 0)
                {
                    PaperMeshBuilder.Build(_ghostMesh, _plan.Moved, null, true, true, GhostOffset, GhostEdgeColor);
                    _ghostGo.SetActive(true);
                }
                else
                {
                    _ghostGo.SetActive(false);
                }
                _paperRoot.localPosition = new Vector3(0f, Lift(Mathf.Min(flapMin, _staticMinY)), 0f);
                return;
            }

            SetStatic(_state != null ? _state.Facets : NoFacets, _state);
            _flapGo.SetActive(false);
            _ghostGo.SetActive(false);
            _paperRoot.localPosition = Vector3.zero;
        }

        /// <summary>Rebuilds the static paper and crease meshes only when their source changed.</summary>
        private void SetStatic(IReadOnlyList<Facet> facets, PaperState creases)
        {
            if (_staticCached && ReferenceEquals(facets, _staticKey) && ReferenceEquals(creases, _staticCreaseKey)) return;
            _staticCached = true;
            _staticKey = facets;
            _staticCreaseKey = creases;
            float min = PaperMeshBuilder.Build(_paperMesh, facets, null, false, true, 0f, EdgeColor);
            _staticMinY = facets.Count == 0 ? float.MaxValue : min;
            _creaseOverlay.Clear();
            CreaseLineCount = creases != null
                ? PaperMeshBuilder.AddCreaseLinesCounted(_creaseOverlay, creases, CreaseHalfWidth, CreaseMarkColor)
                : 0;
            _creaseOverlay.Apply(_creaseMesh);
        }

        private void RefreshOverlay()
        {
            if (!_built) return;
            _overlay.Clear();
            GuideMarkersVisible = false;
            bool showGuideBall = false, showHover = false, showCursor = false;

            if (_state != null)
            {
                float top = PaperMeshBuilder.LayerTop(_state.MaxLayer()) + OverlayAbovePaper;
                float pulse = 0.5f + 0.5f * Mathf.Sin(_time * PulseSpeed);

                FoldTableGuide guide = default;
                bool hasGuide = _host != null && _anim == null && _host.TryGetGuide(out guide);
                if (hasGuide)
                {
                    GuideMarkersVisible = true;
                    _overlay.Dashed(V3(guide.LineA, top), V3(guide.LineB, top), 0.07f, 0.045f, 0.008f, GuideCreaseColor);
                    var g = V3(guide.Grab, top + 0.001f);
                    var t = V3(guide.Drop, top + 0.001f);
                    var dir = t - g;
                    float len = dir.magnitude;
                    if (len > 0.2f)
                    {
                        var tip = t - dir / len * 0.09f;
                        _overlay.Dashed(g + dir / len * 0.08f, tip, 0.03f, 0.03f, 0.006f, GuideArrowColor);
                        _overlay.ArrowHead(g, tip, 0.07f, GuideArrowColor);
                    }
                    _overlay.Ring(t, 0.055f + 0.012f * pulse, 0.075f + 0.012f * pulse, TargetColor);
                    _overlay.Disc(t, 0.018f, TargetColor);
                    if (_gesture == null)
                    {
                        showGuideBall = true;
                        _guideBall.localPosition = V3(guide.Grab, top + 0.035f);
                        _guideBall.localScale = Vector3.one * (0.07f + 0.025f * pulse);
                    }
                }

                if (_gesture != null)
                {
                    var drop = _gesture.Drop;
                    bool snapped = drop.Kind != SnapKind.None;
                    var modeColor = _ctrl ? CreasePreviewColor : (_shift ? MountainColor : ValleyColor);

                    if (_plan != null && _plan.Valid && LineOnPaper.TryGet(_state, _plan.Grab, _plan.Drop, out var a, out var b))
                    {
                        if (_ctrl) _overlay.Dashed(V3(a, top + 0.002f), V3(b, top + 0.002f), 0.035f, 0.035f, 0.009f, modeColor);
                        else if (_shift) _overlay.Dashed(V3(a, top + 0.002f), V3(b, top + 0.002f), 0.05f, 0.03f, 0.011f, modeColor);
                        else _overlay.Segment(V3(a, top + 0.002f), V3(b, top + 0.002f), 0.011f, modeColor);
                    }

                    // Magnet highlight (R-021): the snapped point as a small ring, a line/mark as a thick strip.
                    if (drop.AnchorCount >= 1) _overlay.Ring(V3(drop.AnchorA, top + 0.004f), 0.025f, 0.04f, TargetColor);
                    if (drop.AnchorCount >= 2)
                    {
                        _overlay.Ring(V3(drop.AnchorB, top + 0.004f), 0.025f, 0.04f, TargetColor);
                        _overlay.Segment(V3(drop.AnchorA, top + 0.004f), V3(drop.AnchorB, top + 0.004f), 0.014f, TargetColor);
                    }

                    _overlay.Ring(V3(drop.Point, top + 0.003f), 0.045f, 0.06f, snapped ? TargetColor : modeColor);
                    _overlay.Dashed(V3(_gesture.Grab, top + 0.002f), V3(drop.Point, top + 0.002f), 0.025f, 0.025f, 0.004f,
                        DragLineColor);

                    showCursor = true;
                    _cursorBall.localPosition = V3(drop.Point, top + 0.03f);
                    _cursorBall.localScale = Vector3.one * 0.06f;
                    _cursorMat.color = snapped ? TargetColor : modeColor;
                }
                else if (_hoverValid && _anim == null)
                {
                    showHover = true;
                    _hoverBall.localPosition = V3(_hoverPoint, top + 0.02f);
                    _hoverBall.localScale = Vector3.one * (_hoverCorner ? 0.06f : 0.04f);
                    _hoverMat.color = _hoverCorner ? Color.white : new Color(0.8f, 0.8f, 0.8f);
                }
            }

            _overlay.Apply(_overlayMesh);
            _guideBall.gameObject.SetActive(showGuideBall);
            _hoverBall.gameObject.SetActive(showHover);
            _cursorBall.gameObject.SetActive(showCursor);
        }
    }
}
