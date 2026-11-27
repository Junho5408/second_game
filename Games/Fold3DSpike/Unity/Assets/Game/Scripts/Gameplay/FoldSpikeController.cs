using System;
using System.Collections.Generic;
using Fold3DSpike.Core.Folding;
using Fold3DSpike.Runtime.Systems;
using Fold3DSpike.Runtime.UI;
using UnityEngine;
using UnityEngine.Rendering;

namespace Fold3DSpike.Runtime.Gameplay
{
    /// <summary>
    /// SPIKE: 3D drag-to-fold paper. Owns the <see cref="FoldSession"/> (Core), builds the scene from code
    /// (light, table, paper meshes, markers), handles mouse input, the fold animation and the IMGUI HUD.
    /// Table plane = world XZ (Core x,y = world x,z); camera looks from -Z, so Core +Y is the far edge.
    /// </summary>
    public sealed class FoldSpikeController : MonoBehaviour
    {
        public const string ObjectName = "[FoldSpike]";

        // Interaction tuning (paper is 2 x 2 units = "20 cm").
        private const float GrabCornerRadius = 0.16f;
        private const float GrabEdgeRadius = 0.08f;
        private const float DropTargetSnap = 0.08f;
        private const float DropVertexSnap = 0.06f;
        private const float FoldDuration = 0.4f;
        private const float LiftFinishDuration = 0.2f;
        private const float FlipDuration = 0.6f;
        private const float LiftAngle = 150f;

        private static readonly Color FrontColor = new Color(0.2f, 0.5f, 0.93f);
        private static readonly Color BackColor = new Color(0.97f, 0.96f, 0.93f);
        private static readonly Color EdgeColor = new Color(0.12f, 0.13f, 0.2f, 0.6f);
        private static readonly Color GhostColor = new Color(1f, 0.88f, 0.3f, 0.32f);
        private static readonly Color GhostEdgeColor = new Color(1f, 0.7f, 0.15f, 0.9f);
        private static readonly Color ValleyColor = new Color(1f, 0.5f, 0.05f, 1f);
        private static readonly Color MountainColor = new Color(0.62f, 0.3f, 1f, 1f);
        private static readonly Color GuideCreaseColor = new Color(0.95f, 0.2f, 0.35f, 0.9f);
        private static readonly Color GuideArrowColor = new Color(1f, 0.72f, 0.05f, 0.95f);
        private static readonly Color TargetColor = new Color(0.15f, 0.85f, 0.4f, 0.95f);

        private static FoldSpikeController s_instance;

        private sealed class Anim
        {
            public FoldPlan Plan;
            public bool IsFlip;
            public List<Facet> Moving;
            public FoldLine Line;
            public float Y0;
            public float Sigma;
            public float From;
            public float To;
            public float T;
            public float Duration;
            public float StaticMinY;
        }

        private Camera _cam;
        private float _yaw;
        private float _pitch = 58f;
        private float _dist = 4.4f;
        private Vector3 _lastMouse;
        private bool _orbiting;

        private Transform _paperRoot;
        private Mesh _paperMesh, _flapMesh, _ghostMesh, _overlayMesh;
        private GameObject _flapGo, _ghostGo;
        private Transform _targetBall, _hoverBall, _cursorBall;
        private Material _hoverMat, _cursorMat;
        private readonly OverlayBuilder _overlay = new OverlayBuilder();

        private bool _paperShowsState;
        private bool _dragging;
        private Vec2 _grab, _drop;
        private bool _dropSnapped;
        private bool _mountain;
        private FoldPlan _plan;
        private bool _hoverValid, _hoverCorner;
        private Vec2 _hoverPoint;
        private Anim _anim;

        private string _toast;
        private float _toastUntil;

        // IMGUI
        private GUIStyle _title, _label, _small, _button, _toastStyle;
        private float _guiScale = 1f;
        private readonly List<Rect> _guiRects = new List<Rect>();
        private readonly List<Rect> _guiRectsBuilding = new List<Rect>();

        /// <summary>The runtime instance created by <see cref="EnsureInstance"/>; null if none.</summary>
        public static FoldSpikeController Instance => s_instance != null ? s_instance : null;

        /// <summary>Paper state, undo history and guided steps/scores.</summary>
        public FoldSession Session { get; private set; }

        /// <summary>false = translucent ghost of the result while dragging (default); true = flap is lifted live.</summary>
        public bool LiftPreview { get; set; }

        public bool IsAnimating => _anim != null;

        /// <summary>Vertex count of the static paper mesh (changes when the geometry changes).</summary>
        public int PaperVertexCount => _paperMesh != null ? _paperMesh.vertexCount : 0;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Init()
        {
            EnsureInstance();
        }

        /// <summary>Idempotent: returns the live instance, else attaches to/creates the "[FoldSpike]" object.</summary>
        public static FoldSpikeController EnsureInstance()
        {
            if (s_instance != null) return s_instance;
            var existing = GameObject.Find(ObjectName);
            if (existing != null)
            {
                var found = existing.GetComponent<FoldSpikeController>();
                if (found == null) found = existing.AddComponent<FoldSpikeController>();
                s_instance = found;
                return found;
            }
            var go = new GameObject(ObjectName);
            DontDestroyOnLoad(go);
            s_instance = go.AddComponent<FoldSpikeController>();
            return s_instance;
        }

        // ------------------------------------------------------------------ public API

        /// <summary>
        /// Programmatic fold (tests, debugging): grab/drop on the table plane (x = world X, y = world Z).
        /// With <paramref name="animate"/> false the fold is applied immediately. Returns false if not a valid fold.
        /// </summary>
        public bool Fold(Vector2 grab, Vector2 drop, bool mountain, bool animate)
        {
            if (_anim != null) FinishAnimationNow();
            CancelDrag();
            var plan = Session.Preview(new Vec2(grab.x, grab.y), new Vec2(drop.x, drop.y), mountain);
            if (!plan.Valid) return false;
            if (animate) StartFold(plan, 0f);
            else
            {
                Session.Commit(plan);
                AfterChange(true);
            }
            return true;
        }

        public void Undo()
        {
            if (_anim != null) FinishAnimationNow();
            CancelDrag();
            if (Session.Undo()) AfterChange(false);
            else ShowToast("되돌릴 접기가 없어요");
        }

        public void Restart()
        {
            if (_anim != null) _anim = null;
            CancelDrag();
            Session.Restart();
            AfterChange(false);
        }

        public void Flip()
        {
            if (_anim != null) FinishAnimationNow();
            CancelDrag();
            float cx = (float)Session.FlipAxisX();
            int max = Session.State.MaxLayer();
            _anim = new Anim
            {
                IsFlip = true,
                Moving = Session.State.Facets,
                Line = new FoldLine(new Vec2(cx, 0), new Vec2(1, 0)),
                Y0 = 0.5f * (PaperMeshBuilder.LayerTop(max) + PaperMeshBuilder.LayerBottom(0)),
                Sigma = 1f,
                From = 0f,
                To = 180f,
                Duration = FlipDuration,
            };
            BeginAnimationVisuals(new List<Facet>());
        }

        // ------------------------------------------------------------------ setup

        private void Awake()
        {
            Session = new FoldSession();
            BuildScene();
            AfterChange(false);
        }

        private void Start()
        {
            SetupCamera();
        }

        private void OnDestroy()
        {
            if (s_instance == this) s_instance = null;
        }

        private void BuildScene()
        {
            // Light
            var lightGo = new GameObject("Sun");
            lightGo.transform.SetParent(transform, false);
            lightGo.transform.rotation = Quaternion.Euler(52f, -35f, 0f);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.97f, 0.92f);
            light.intensity = 1.05f;
            light.shadows = LightShadows.Soft;
            light.shadowStrength = 0.55f;
            light.shadowBias = 0.04f;
            light.shadowNormalBias = 0.4f;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.46f, 0.47f, 0.52f);

            // Table
            var table = new GameObject("Table");
            table.transform.SetParent(transform, false);
            var tMesh = new Mesh { name = "Table" };
            tMesh.SetVertices(new List<Vector3>
            {
                new Vector3(-5, 0, -5), new Vector3(5, 0, -5), new Vector3(5, 0, 5), new Vector3(-5, 0, 5),
            });
            tMesh.SetTriangles(new[] { 0, 2, 1, 0, 3, 2 }, 0);
            tMesh.RecalculateNormals();
            tMesh.RecalculateBounds();
            table.AddComponent<MeshFilter>().sharedMesh = tMesh;
            var tr = table.AddComponent<MeshRenderer>();
            tr.sharedMaterial = SpikeMaterials.Lit(new Color(0.5f, 0.38f, 0.27f));
            tr.receiveShadows = true;
            tr.shadowCastingMode = ShadowCastingMode.Off;

            // Paper
            var root = new GameObject("Paper");
            root.transform.SetParent(transform, false);
            _paperRoot = root.transform;
            var paperMats = new[]
            {
                SpikeMaterials.Lit(FrontColor), SpikeMaterials.Lit(BackColor), SpikeMaterials.Unlit(Color.white),
            };
            _paperMesh = PaperMeshBuilder.NewMesh("PaperStatic");
            MakeRenderer("Static", _paperRoot, _paperMesh, paperMats, true);
            _flapMesh = PaperMeshBuilder.NewMesh("PaperFlap");
            _flapGo = MakeRenderer("Flap", _paperRoot, _flapMesh, paperMats, true);
            _flapGo.SetActive(false);

            var ghostMat = SpikeMaterials.Unlit(GhostColor);
            _ghostMesh = PaperMeshBuilder.NewMesh("Ghost");
            _ghostGo = MakeRenderer("Ghost", transform, _ghostMesh,
                new[] { ghostMat, ghostMat, SpikeMaterials.Unlit(Color.white) }, false);
            _ghostGo.SetActive(false);

            _overlayMesh = PaperMeshBuilder.NewMesh("Overlay");
            MakeRenderer("Overlay", transform, _overlayMesh, new[] { SpikeMaterials.Unlit(Color.white) }, false);

            var sphere = SphereMesh.Create();
            _targetBall = MakeBall("TargetBall", sphere, SpikeMaterials.Lit(GuideArrowColor));
            _hoverMat = SpikeMaterials.Lit(Color.white);
            _hoverBall = MakeBall("HoverBall", sphere, _hoverMat);
            _cursorMat = SpikeMaterials.Lit(ValleyColor);
            _cursorBall = MakeBall("CursorBall", sphere, _cursorMat);
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

        private Transform MakeBall(string name, Mesh mesh, Material mat)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            go.SetActive(false);
            return go.transform;
        }

        private void SetupCamera()
        {
            _cam = Camera.main;
            if (_cam == null)
            {
                var camGo = new GameObject("Main Camera");
                camGo.tag = "MainCamera";
                _cam = camGo.AddComponent<Camera>();
            }
            _cam.orthographic = false;
            _cam.fieldOfView = 40f;
            _cam.nearClipPlane = 0.03f;
            _cam.farClipPlane = 100f;
            _cam.clearFlags = CameraClearFlags.SolidColor;
            _cam.backgroundColor = new Color(0.11f, 0.12f, 0.16f);
            ApplyCamera();
        }

        private void ApplyCamera()
        {
            if (_cam == null) return;
            var rot = Quaternion.Euler(_pitch, _yaw, 0f);
            _cam.transform.position = rot * new Vector3(0f, 0f, -_dist);
            _cam.transform.rotation = rot;
        }

        // ------------------------------------------------------------------ frame

        private void Update()
        {
            if (_cam == null) SetupCamera();
            HandleCamera();
            if (_anim != null)
            {
                StepAnimation(Time.deltaTime);
            }
            else
            {
                HandleKeys();
                HandleFoldInput();
            }
            UpdateOverlayAndMarkers();
        }

        private void LateUpdate()
        {
            ApplyCamera();
        }

        private void HandleCamera()
        {
            var mouse = Input.mousePosition;
            if (Input.GetMouseButtonDown(1))
            {
                _orbiting = true;
                _lastMouse = mouse;
            }
            if (!Input.GetMouseButton(1)) _orbiting = false;
            if (_orbiting)
            {
                var d = mouse - _lastMouse;
                _yaw += d.x * 0.3f;
                _pitch = Mathf.Clamp(_pitch - d.y * 0.3f, 10f, 89f);
                _lastMouse = mouse;
            }
            float scroll = Input.mouseScrollDelta.y;
            if (Mathf.Abs(scroll) > 0.01f && !PointerOverGui())
            {
                _dist = Mathf.Clamp(_dist * (1f - scroll * 0.1f), 1.5f, 10f);
            }
        }

        private void HandleKeys()
        {
            if (_dragging) return;
            if (Input.GetKeyDown(KeyCode.Z) || Input.GetKeyDown(KeyCode.Backspace)) Undo();
            else if (Input.GetKeyDown(KeyCode.R)) Restart();
            else if (Input.GetKeyDown(KeyCode.F)) Flip();
            else if (Input.GetKeyDown(KeyCode.L)) ToggleLift();
        }

        private void ToggleLift()
        {
            LiftPreview = !LiftPreview;
            ShowToast(LiftPreview ? "미리보기: 손에 들고 접기" : "미리보기: 투명 유령");
        }

        private void HandleFoldInput()
        {
            bool hasP = PointerOnPaperPlane(out var p);
            _mountain = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

            if (!_dragging)
            {
                _hoverValid = hasP && !_orbiting && !PointerOverGui() &&
                              Session.State.TrySnapGrab(p, GrabCornerRadius, GrabEdgeRadius, out _hoverPoint, out _hoverCorner);
                if (Input.GetMouseButtonDown(0) && _hoverValid)
                {
                    _dragging = true;
                    _grab = _hoverPoint;
                    _drop = _grab;
                    _plan = null;
                    _hoverValid = false;
                }
                return;
            }

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                CancelDrag();
                ShowToast("접기를 취소했어요");
                return;
            }
            if (hasP)
            {
                _drop = SnapDrop(p, out _dropSnapped);
                _plan = Session.Preview(_grab, _drop, _mountain);
            }
            UpdateDragPreview();

            if (Input.GetMouseButtonUp(0) || !Input.GetMouseButton(0))
            {
                var plan = _plan;
                CancelDrag();
                if (plan != null && plan.Valid) StartFold(plan, LiftPreview ? LiftAngle : 0f);
                else if (plan != null && plan.Reason != null) ShowToast(plan.Reason);
            }
        }

        private Vec2 SnapDrop(Vec2 p, out bool snapped)
        {
            snapped = false;
            var step = Session.CurrentStep;
            if (step != null && Vec2.Distance(p, step.Drop) <= DropTargetSnap)
            {
                snapped = true;
                return step.Drop;
            }
            double best = DropVertexSnap;
            var result = p;
            foreach (var f in Session.State.Facets)
            {
                foreach (var v in f.World)
                {
                    if (Vec2.Distance(v, _grab) < 1e-6) continue;
                    double d = Vec2.Distance(v, p);
                    if (d <= best)
                    {
                        best = d;
                        result = v;
                        snapped = true;
                    }
                }
            }
            return result;
        }

        private bool PointerOnPaperPlane(out Vec2 p)
        {
            p = default;
            if (_cam == null) return false;
            var ray = _cam.ScreenPointToRay(Input.mousePosition);
            float planeY = PaperMeshBuilder.LayerTop(Session.State.MaxLayer());
            if (ray.direction.y > -1e-4f) return false;
            float t = (planeY - ray.origin.y) / ray.direction.y;
            var hit = ray.origin + ray.direction * t;
            p = new Vec2(hit.x, hit.z);
            return true;
        }

        private void CancelDrag()
        {
            _dragging = false;
            _plan = null;
            if (_ghostGo != null) _ghostGo.SetActive(false);
            if (_flapGo != null && _anim == null) _flapGo.SetActive(false);
            if (_paperRoot != null && _anim == null) _paperRoot.localPosition = Vector3.zero;
            if (!_paperShowsState && _anim == null) RebuildPaper();
        }

        private void UpdateDragPreview()
        {
            bool valid = _plan != null && _plan.Valid;
            if (valid)
            {
                PaperMeshBuilder.Build(_ghostMesh, _plan.Moved, null, true, true, 0.004f, GhostEdgeColor);
                _ghostGo.SetActive(true);
            }
            else
            {
                _ghostGo.SetActive(false);
            }

            if (LiftPreview && valid)
            {
                float staticMin = PaperMeshBuilder.Build(_paperMesh, _plan.Static, null, false, true, 0f, EdgeColor);
                _paperShowsState = false;
                float y0 = AxisHeight(_plan.Static, _plan.Moving, _plan.Mountain);
                float flapMin = PaperMeshBuilder.Build(_flapMesh, _plan.Moving,
                    Rotation(_plan.Line, y0, _plan.Mountain ? -1f : 1f, LiftAngle), false, true, 0f, EdgeColor);
                _flapGo.SetActive(true);
                _paperRoot.localPosition = new Vector3(0f, Lift(Mathf.Min(staticMin, flapMin)), 0f);
            }
            else
            {
                _flapGo.SetActive(false);
                _paperRoot.localPosition = Vector3.zero;
                if (!_paperShowsState) RebuildPaper();
            }
        }

        // ------------------------------------------------------------------ animation

        private void StartFold(FoldPlan plan, float fromAngle)
        {
            _anim = new Anim
            {
                Plan = plan,
                Moving = plan.Moving,
                Line = plan.Line,
                Y0 = AxisHeight(plan.Static, plan.Moving, plan.Mountain),
                Sigma = plan.Mountain ? -1f : 1f,
                From = fromAngle,
                To = 180f,
                Duration = fromAngle > 0f ? LiftFinishDuration : FoldDuration,
            };
            BeginAnimationVisuals(plan.Static);
        }

        private void BeginAnimationVisuals(List<Facet> staticFacets)
        {
            _ghostGo.SetActive(false);
            _hoverValid = false;
            _anim.StaticMinY = staticFacets.Count == 0
                ? float.MaxValue
                : PaperMeshBuilder.Build(_paperMesh, staticFacets, null, false, true, 0f, EdgeColor);
            if (staticFacets.Count == 0) _paperMesh.Clear();
            _paperShowsState = false;
            _flapGo.SetActive(true);
            StepAnimation(0f);
        }

        private void StepAnimation(float dt)
        {
            var a = _anim;
            a.T += dt;
            float k = a.Duration <= 0f ? 1f : Mathf.Clamp01(a.T / a.Duration);
            float e = k * k * (3f - 2f * k);
            float angle = Mathf.Lerp(a.From, a.To, e);
            float flapMin = PaperMeshBuilder.Build(_flapMesh, a.Moving, Rotation(a.Line, a.Y0, a.Sigma, angle),
                false, true, 0f, EdgeColor);
            _paperRoot.localPosition = new Vector3(0f, Lift(Mathf.Min(flapMin, a.StaticMinY)), 0f);
            if (k >= 1f) FinishAnimationNow();
        }

        private void FinishAnimationNow()
        {
            var a = _anim;
            _anim = null;
            if (a == null) return;
            bool scored = false;
            if (a.IsFlip) Session.Flip();
            else scored = Session.Commit(a.Plan);
            AfterChange(scored);
        }

        /// <summary>Rebuilds visuals after the session changed; shows the step score if one was just earned.</summary>
        private void AfterChange(bool folded)
        {
            _flapGo.SetActive(false);
            _ghostGo.SetActive(false);
            _paperRoot.localPosition = Vector3.zero;
            RebuildPaper();
            if (!folded) return;
            var s = Session.LastScore;
            if (s != null)
            {
                string msg = $"{Session.StepIndex}단계: {s.Score}점 — {Praise(s.Score)}";
                if (s.WrongSide) msg += " (반대쪽이 접혔어요)";
                if (Session.GuideComplete) msg += $"\n종이비행기 완성! 총점 {Session.TotalScore}점";
                ShowToast(msg, 3f);
            }
        }

        private static string Praise(int score)
        {
            if (score >= 90) return "완벽해요!";
            if (score >= 70) return "좋아요";
            if (score >= 40) return "조금 삐뚤어요";
            return "많이 어긋났어요";
        }

        private void RebuildPaper()
        {
            PaperMeshBuilder.Build(_paperMesh, Session.State.Facets, null, false, true, 0f, EdgeColor);
            _paperShowsState = true;
        }

        private static float Lift(float minY) => Mathf.Max(0f, 0.0008f - minY);

        /// <summary>Height of the hinge: just above the stack for valley folds, just below it for mountain folds.</summary>
        private static float AxisHeight(List<Facet> stat, List<Facet> moving, bool mountain)
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
        /// sigma +1 = valley (flap swings up toward the camera), -1 = mountain (swings under the paper).
        /// At 180° the result is the mirror image across the crease with the layer order reversed.
        /// </summary>
        private static VertexXform Rotation(FoldLine line, float y0, float sigma, float angleDeg)
        {
            double rad = angleDeg * Math.PI / 180.0;
            double c = Math.Cos(rad), sn = Math.Sin(rad);
            return (x, z, y) =>
            {
                var p = new Vec2(x, z);
                double s = line.Side(p);
                var foot = p - line.Normal * s;
                double v = y - y0;
                double s2 = s * c + sigma * v * sn;
                double v2 = -sigma * s * sn + v * c;
                var q = foot + line.Normal * s2;
                return new Vector3((float)q.X, (float)(y0 + v2), (float)q.Y);
            };
        }

        // ------------------------------------------------------------------ overlay & markers

        private void UpdateOverlayAndMarkers()
        {
            _overlay.Clear();
            float top = PaperMeshBuilder.LayerTop(Session.State.MaxLayer()) + 0.003f;
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 5f);

            var step = Session.CurrentStep;
            bool showGuide = step != null && _anim == null;
            if (showGuide)
            {
                _overlay.Dashed(V3(step.CreaseA, top), V3(step.CreaseB, top), 0.07f, 0.045f, 0.008f, GuideCreaseColor);
                var g = V3(step.Grab, top + 0.001f);
                var t = V3(step.Drop, top + 0.001f);
                var dir = (t - g);
                float len = dir.magnitude;
                if (len > 0.2f)
                {
                    var tip = t - dir / len * 0.09f;
                    _overlay.Dashed(g + dir / len * 0.08f, tip, 0.03f, 0.03f, 0.006f, GuideArrowColor);
                    _overlay.ArrowHead(g, tip, 0.07f, GuideArrowColor);
                }
                _overlay.Ring(t, 0.055f + 0.012f * pulse, 0.075f + 0.012f * pulse, TargetColor);
                _overlay.Disc(t, 0.018f, TargetColor);
            }

            if (_dragging && _plan != null && _plan.Valid)
            {
                if (ClipLineToPaper(_plan.Line, out var a, out var b))
                {
                    var col = _plan.Mountain ? MountainColor : ValleyColor;
                    if (_plan.Mountain) _overlay.Dashed(V3(a, top + 0.002f), V3(b, top + 0.002f), 0.05f, 0.03f, 0.011f, col);
                    else _overlay.Segment(V3(a, top + 0.002f), V3(b, top + 0.002f), 0.011f, col);
                }
            }
            if (_dragging)
            {
                var dc = _dropSnapped ? TargetColor : (_mountain ? MountainColor : ValleyColor);
                _overlay.Ring(V3(_drop, top + 0.003f), 0.045f, 0.06f, dc);
                _overlay.Dashed(V3(_grab, top + 0.002f), V3(_drop, top + 0.002f), 0.025f, 0.025f, 0.004f,
                    new Color(1f, 1f, 1f, 0.7f));
            }
            _overlay.Apply(_overlayMesh);

            // Balls
            bool showTarget = showGuide && !_dragging;
            _targetBall.gameObject.SetActive(showTarget);
            if (showTarget)
            {
                _targetBall.position = V3(step.Grab, top + 0.035f);
                _targetBall.localScale = Vector3.one * (0.07f + 0.025f * pulse);
            }

            bool showHover = _hoverValid && !_dragging && _anim == null;
            _hoverBall.gameObject.SetActive(showHover);
            if (showHover)
            {
                _hoverBall.position = V3(_hoverPoint, top + 0.02f);
                _hoverBall.localScale = Vector3.one * (_hoverCorner ? 0.06f : 0.04f);
                _hoverMat.color = _hoverCorner ? Color.white : new Color(0.8f, 0.8f, 0.8f);
            }

            _cursorBall.gameObject.SetActive(_dragging);
            if (_dragging)
            {
                float h = LiftPreview ? 0.12f : 0.03f;
                _cursorBall.position = V3(_drop, top + h);
                _cursorBall.localScale = Vector3.one * 0.06f;
                _cursorMat.color = _dropSnapped ? TargetColor : (_mountain ? MountainColor : ValleyColor);
            }
        }

        private static Vector3 V3(Vec2 p, float y) => new Vector3((float)p.X, y, (float)p.Y);

        /// <summary>Part of the crease line that crosses the paper (union of facet crossings), extended slightly.</summary>
        private bool ClipLineToPaper(FoldLine line, out Vec2 a, out Vec2 b)
        {
            double tMin = double.MaxValue, tMax = double.MinValue;
            var dir = line.Direction;
            foreach (var f in Session.State.Facets)
            {
                int n = f.World.Count;
                for (int i = 0; i < n; i++)
                {
                    var p = f.World[i];
                    var q = f.World[(i + 1) % n];
                    double sp = line.Side(p), sq = line.Side(q);
                    if ((sp <= 0 && sq >= 0) || (sp >= 0 && sq <= 0))
                    {
                        double den = sp - sq;
                        var x = Math.Abs(den) < 1e-12 ? p : Vec2.Lerp(p, q, sp / den);
                        double t = Vec2.Dot(x - line.Point, dir);
                        tMin = Math.Min(tMin, t);
                        tMax = Math.Max(tMax, t);
                    }
                }
            }
            if (tMin > tMax)
            {
                a = b = line.Point;
                return false;
            }
            a = line.Point + dir * (tMin - 0.08);
            b = line.Point + dir * (tMax + 0.08);
            return true;
        }

        // ------------------------------------------------------------------ HUD (IMGUI)

        private void ShowToast(string text, float seconds = 1.8f)
        {
            _toast = text;
            _toastUntil = Time.unscaledTime + seconds;
        }

        private bool PointerOverGui()
        {
            var m = Input.mousePosition;
            var v = new Vector2(m.x / _guiScale, (Screen.height - m.y) / _guiScale);
            foreach (var r in _guiRects)
                if (r.Contains(v)) return true;
            return false;
        }

        private void EnsureStyles()
        {
            if (_label != null) return;
            var font = KoreanFont.Get(18);
            _title = new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold, wordWrap = true };
            _label = new GUIStyle(GUI.skin.label) { fontSize = 18, wordWrap = true };
            _small = new GUIStyle(GUI.skin.label) { fontSize = 15, wordWrap = true };
            _button = new GUIStyle(GUI.skin.button) { fontSize = 17 };
            _toastStyle = new GUIStyle(GUI.skin.box) { fontSize = 22, alignment = TextAnchor.MiddleCenter, wordWrap = true };
            _toastStyle.normal.textColor = Color.white;
            if (font != null)
            {
                _title.font = font;
                _label.font = font;
                _small.font = font;
                _button.font = font;
                _toastStyle.font = font;
            }
        }

        private void OnGUI()
        {
            if (Session == null) return;
            EnsureStyles();
            _guiScale = Mathf.Clamp(Screen.height / 900f, 0.75f, 2.5f);
            GUI.matrix = Matrix4x4.Scale(new Vector3(_guiScale, _guiScale, 1f));
            float vw = Screen.width / _guiScale;
            float vh = Screen.height / _guiScale;
            bool repaint = Event.current.type == EventType.Repaint;
            if (repaint) _guiRectsBuilding.Clear();

            // Info panel (top-left)
            GUILayout.BeginArea(new Rect(10, 10, 470, vh - 80));
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("종이비행기 접기 · 3D 시험판", _title);
            var step = Session.CurrentStep;
            if (step != null)
            {
                GUILayout.Label($"단계 {Session.StepIndex + 1}/{Session.Steps.Length}: {step.Title}", _label);
                GUILayout.Label(step.Hint, _small);
                GUILayout.Label("노란 공 = 잡을 곳 · 초록 원 = 놓을 곳 · 빨간 점선 = 이상적인 접는 선", _small);
            }
            else
            {
                GUILayout.Label($"완성! 총점 {Session.TotalScore}점", _title);
                GUILayout.Label("이제 자유롭게 더 접어 보거나 '처음부터'로 다시 해 보세요.", _small);
            }
            var last = Session.LastScore;
            if (last != null)
            {
                GUILayout.Label(
                    $"방금 접기: {last.Score}점 · 접는 선 위치 오차 {last.CreaseOffset * 10:0.0}cm · 각도 오차 {last.AngleErrorDeg:0.0}°" +
                    $" · 잡은 곳 {last.GrabError * 10:0.0}cm / 놓은 곳 {last.DropError * 10:0.0}cm" +
                    (last.WrongSide ? " · 반대쪽이 접힘(점수 절반)" : ""), _small);
            }
            if (Session.Scores.Count > 0)
            {
                var parts = new List<string>();
                for (int i = 0; i < Session.Scores.Count; i++) parts.Add($"{i + 1}단계 {Session.Scores[i].Score}");
                GUILayout.Label(string.Join(" · ", parts) + $"   (평균 {Session.TotalScore})", _small);
            }
            GUILayout.EndVertical();
            if (repaint) AddRect(GUILayoutUtility.GetLastRect(), 10, 10);
            GUILayout.EndArea();

            // Buttons (top-right)
            float bw = 210f;
            GUILayout.BeginArea(new Rect(vw - bw - 10, 10, bw, 300));
            GUILayout.BeginVertical(GUI.skin.box);
            GUI.enabled = !_dragging;
            if (GUILayout.Button("되돌리기 (Z)", _button)) Undo();
            if (GUILayout.Button("처음부터 (R)", _button)) Restart();
            if (GUILayout.Button("종이 뒤집기 (F)", _button)) Flip();
            if (GUILayout.Button(LiftPreview ? "미리보기: 손에 들기 (L)" : "미리보기: 유령 (L)", _button)) ToggleLift();
            GUI.enabled = true;
            GUILayout.EndVertical();
            if (repaint) AddRect(GUILayoutUtility.GetLastRect(), vw - bw - 10, 10);
            GUILayout.EndArea();

            // Help (bottom)
            var help = new Rect(10, vh - 40, vw - 20, 32);
            GUI.Box(help, GUIContent.none);
            GUI.Label(new Rect(help.x + 8, help.y + 5, help.width - 16, 26),
                "왼쪽 드래그: 모서리·변을 잡아 끌어 놓으면 접힘 · Shift: 산접기(뒤로) · Esc: 취소 · 오른쪽 드래그: 시점 회전 · 휠: 확대/축소",
                _small);
            if (repaint) _guiRectsBuilding.Add(help);

            // Drag mode / toast (top-centre)
            if (_dragging)
            {
                string mode = _mountain ? "산접기 — 뒤로 접기 (Shift)" : "골접기 — 앞으로 접기 (Shift를 누르면 산접기)";
                if (_plan != null && !_plan.Valid && _plan.Reason != null) mode += " · " + _plan.Reason;
                GUI.Label(new Rect(vw * 0.5f - 260, 14, 520, 30), mode, _label);
            }
            if (_toast != null && Time.unscaledTime < _toastUntil)
            {
                float tw = 560f;
                GUI.Box(new Rect(vw * 0.5f - tw * 0.5f, vh * 0.16f, tw, 70), _toast, _toastStyle);
            }

            if (repaint)
            {
                _guiRects.Clear();
                _guiRects.AddRange(_guiRectsBuilding);
            }
        }

        private void AddRect(Rect r, float ox, float oy)
        {
            _guiRectsBuilding.Add(new Rect(r.x + ox, r.y + oy, r.width, r.height));
        }
    }
}
