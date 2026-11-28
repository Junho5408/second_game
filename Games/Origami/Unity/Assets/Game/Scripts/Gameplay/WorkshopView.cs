using System.Collections.Generic;
using Origami.Core.Economy;
using Origami.Core.Game;
using Origami.Core.Room;
using Origami.Runtime.Folding;
using Origami.Runtime.Systems;
using UnityEngine;
using UnityEngine.Rendering;

namespace Origami.Runtime.Gameplay
{
    /// <summary>
    /// The workshop room (C-193, CONTENT §6, D-130, ARCHITECTURE §7 and §14): code-built flat-colour floor and walls (the south
    /// wall only 0.5 m high), the furniture as CC0 models (<see cref="ArtKeys"/>, <c>Art/ASSETS.md</c>) fitted by
    /// <see cref="ModelFit"/> to the Core <c>RoomLayout</c> boxes, decoration props, the counter's 5 display slots, the trash
    /// balls, the window sky, a warm directional light with soft shadows, trilight ambient and a warm bedside lamp.
    /// No primitives, colliders or physics — collision and interaction stay Core <c>RoomMotion</c>/<c>Interaction</c>; the models
    /// only change what is drawn. A furniture model that cannot be loaded is drawn with the previous code meshes instead.
    /// <para>
    /// <see cref="Refresh"/> shows the snapshot: display slot i shows <c>Display[i].Shape</c> (the sheet as the player folded
    /// it, F-010 AC-06), the trash shows min(failures, 10) paper balls, and the window colour goes from day to sunset with the
    /// sheets used today (0–5): the unlit sky pane behind the window and the model's <c>glass</c> slot (MaterialPropertyBlock).
    /// <see cref="UpdateCutaway"/> hides the tall door (shadows only) while the character stands behind it as seen from the
    /// walking camera. No Update — <see cref="OrigamiApp"/> calls both.
    /// </para>
    /// Placement and visual decisions: <c>Scripts/Gameplay/README.md</c>.
    /// </summary>
    public sealed class WorkshopView : MonoBehaviour
    {
        /// <summary>Display slot x positions on the counter top (D-130, view values).</summary>
        internal static readonly float[] SlotX = { 1.7f, 2.1f, 2.5f, 2.9f, 3.3f };
        internal const float SlotZ = -2.3f, CounterTop = 1.0f, SlotScale = 0.1f;
        internal const int MaxTrashBalls = 10;

        /// <summary>Chair model: whole height (with backrest) and the cushion top the seated character rests on (ASSETS.md).</summary>
        internal const float ChairHeight = 0.92f, ChairSeatHeight = 0.48f;

        /// <summary>Window: two casement models, bottom 1.0 m, in front of the north wall; the sky pane behind them.</summary>
        internal const float WindowBottom = 1.0f, WindowHeight = 1.0f, WindowFrameWidth = 0.564f, WindowInset = 0.03f;

        /// <summary>Door cut-away: hidden while the character is within this x distance of the door and south of this z.</summary>
        internal const float DoorCutawayHalfWidth = 1.1f, DoorCutawayZ = -1.0f;

        private const float WallThickness = 0.1f, SouthWallHeight = 0.5f;
        private const float BallRadius = 0.07f;
        private const float GlassAlpha = 0.55f;
        private const string GlassColorProperty = "_Color";

        // Flat colours (DECISIONS 2026-11-28): floor a darker variant of the Kenney wood (0.83, 0.65, 0.42), cream walls.
        private static readonly Color FloorColor = new Color(0.56f, 0.41f, 0.28f);
        private static readonly Color WallColor = new Color(0.93f, 0.88f, 0.79f);
        private static readonly Color WoodColor = new Color(0.55f, 0.38f, 0.24f);
        private static readonly Color DarkWoodColor = new Color(0.4f, 0.27f, 0.17f);
        private static readonly Color BedColor = new Color(0.55f, 0.66f, 0.85f);
        private static readonly Color PillowColor = new Color(0.96f, 0.96f, 0.94f);
        private static readonly Color TrashColor = new Color(0.45f, 0.48f, 0.5f);
        private static readonly Color BallColor = new Color(0.95f, 0.94f, 0.9f);
        private static readonly Color DoorColor = new Color(0.47f, 0.32f, 0.2f);
        private static readonly Color WindowDay = new Color(0.62f, 0.84f, 1f);
        private static readonly Color WindowSunset = new Color(1f, 0.55f, 0.3f);
        private static readonly Color PaperFront = new Color(0.2f, 0.5f, 0.93f);
        private static readonly Color PaperBack = new Color(0.97f, 0.96f, 0.93f);
        private static readonly Color PaperEdge = new Color(0.12f, 0.13f, 0.2f, 0.6f);
        private static readonly Color[] BookColors =
        {
            new Color(0.75f, 0.25f, 0.25f), new Color(0.25f, 0.5f, 0.3f), new Color(0.25f, 0.35f, 0.65f), new Color(0.8f, 0.65f, 0.25f),
        };

        // Lighting (built-in RP, no fog — ARCHITECTURE §14.5).
        internal static readonly Quaternion SunRotation = Quaternion.Euler(58f, -50f, 0f);
        private static readonly Color SunColor = new Color(1f, 0.9f, 0.76f);
        private const float SunIntensity = 1.15f, SunShadowStrength = 0.6f;
        internal static readonly Color AmbientSky = new Color(0.62f, 0.6f, 0.6f);
        internal static readonly Color AmbientEquator = new Color(0.55f, 0.47f, 0.4f);
        internal static readonly Color AmbientGround = new Color(0.34f, 0.27f, 0.21f);
        private static readonly Color LampColor = new Color(1f, 0.78f, 0.52f);
        private const float LampIntensity = 0.9f, LampRange = 2.4f;

        // Props (ASSETS.md "Props"): positions are bottom centres (x, y, z).
        private static readonly Vector3 NightstandAt = new Vector3(-2.2f, 0f, 2.78f);
        private static readonly Vector3 FloorLampAt = new Vector3(0.95f, 0f, 2.85f);
        private static readonly Vector3 RugAt = new Vector3(0f, 0f, 0.3f);
        private static readonly Vector3 PottedPlantAt = new Vector3(3.72f, 0f, 2.72f);
        private static readonly Vector3 PaperBoxAt = new Vector3(3.75f, 0f, -2.75f);
        private static readonly Vector3 CoatRackAt = new Vector3(-3.7f, 0f, -1.2f);
        private const float SmallPlantX = 0.55f, BooksZ = 2.70f;

        // Bookshelf shelf tops as fractions of the model height (native y 0.7 / 3.1 / 5.5 of 7.9) and the 3 bay centres.
        private static readonly float[] ShelfFractions = { 0.7f / 7.9f, 3.1f / 7.9f, 5.5f / 7.9f };
        private static readonly float[] BayOffsets = { -0.53f, 0f, 0.53f };

        private readonly List<Mesh> _meshes = new List<Mesh>();
        private readonly List<Material> _materials = new List<Material>();
        private readonly GameObject[] _slots = new GameObject[5];
        private readonly Mesh[] _slotMeshes = new Mesh[5];
        private readonly GameObject[] _balls = new GameObject[MaxTrashBalls];
        private readonly Dictionary<string, List<GameObject>> _furniture = new Dictionary<string, List<GameObject>>();
        private readonly Dictionary<string, bool> _usesModel = new Dictionary<string, bool>();
        private readonly List<GameObject> _props = new List<GameObject>();
        private readonly List<(Renderer renderer, int index)> _glass = new List<(Renderer, int)>();
        private readonly List<Renderer> _doorRenderers = new List<Renderer>();
        private MaterialPropertyBlock _mpb;
        private Material _windowMat;
        private Material[] _paperMats;
        private Color _windowColor = WindowDay;
        private bool _built;

        /// <summary>Use the imported models (set before the component wakes, e.g. on an inactive GameObject; tests use false for the fallback).</summary>
        internal bool LoadModels { get; set; } = true;

        /// <summary>Display slots showing a piece after the last <see cref="Refresh"/>.</summary>
        public int DisplayedPieceCount { get; private set; }

        /// <summary>Paper balls visible in the trash can (min(failures, 10)).</summary>
        public int TrashBallCount { get; private set; }

        /// <summary>Window colour of the last Refresh (tests).</summary>
        internal Color WindowColor => _windowColor;

        /// <summary>Slot GameObject (tests): active when it shows a piece.</summary>
        internal GameObject Slot(int i) => i >= 0 && i < _slots.Length ? _slots[i] : null;

        internal Mesh SlotMesh(int i) => i >= 0 && i < _slotMeshes.Length ? _slotMeshes[i] : null;

        internal GameObject Ball(int i) => i >= 0 && i < _balls.Length ? _balls[i] : null;

        internal Light Sun { get; private set; }

        internal Light LampLight { get; private set; }

        /// <summary>Glass material slots of the window models tinted by <see cref="Refresh"/>.</summary>
        internal int GlassSlotCount => _glass.Count;

        /// <summary>The door is hidden (shadows only) by <see cref="UpdateCutaway"/>.</summary>
        internal bool DoorCutaway { get; private set; }

        /// <summary>Decoration props that loaded (rug, lamps, plants, …).</summary>
        internal int PropCount => _props.Count;

        /// <summary>Furniture <paramref name="id"/> (a <c>RoomLayout.Furniture</c> id) is drawn with its model (false = code meshes).</summary>
        internal bool UsesModel(string id)
        {
            Build();
            return id != null && _usesModel.TryGetValue(id, out var m) && m;
        }

        /// <summary>World bounds of everything drawn for furniture <paramref name="id"/> (window: frames only, no sky pane).</summary>
        internal bool TryFurnitureBounds(string id, out Bounds bounds)
        {
            Build();
            bounds = default;
            if (id == null || !_furniture.TryGetValue(id, out var parts)) return false;
            bool any = false;
            foreach (var p in parts)
            {
                if (!ModelFit.TryWorldBounds(p, out var b)) continue;
                if (!any) bounds = b;
                else bounds.Encapsulate(b);
                any = true;
            }
            return any;
        }

        /// <summary>Current glass tint of slot <paramref name="i"/> (from its MaterialPropertyBlock; tests).</summary>
        internal Color GlassTint(int i)
        {
            if (i < 0 || i >= _glass.Count || _glass[i].renderer == null) return Color.clear;
            var mpb = new MaterialPropertyBlock();
            _glass[i].renderer.GetPropertyBlock(mpb, _glass[i].index);
            return mpb.GetColor(GlassColorProperty);
        }

        /// <summary>Shows display, trash and window of <paramref name="snapshot"/> (null → empty room).</summary>
        public void Refresh(GameSnapshot snapshot)
        {
            Build();
            int shown = 0;
            for (int i = 0; i < _slots.Length; i++)
            {
                var piece = snapshot != null && i < snapshot.Display.Count ? snapshot.Display[i] : null;
                bool on = piece != null && piece.Shape != null;
                if (on)
                {
                    PaperMeshBuilder.Build(_slotMeshes[i], piece.Shape.Facets, null, false, true, 0f, PaperEdge);
                    shown++;
                }
                _slots[i].SetActive(on);
            }
            DisplayedPieceCount = shown;

            int balls = snapshot == null ? 0 : Mathf.Clamp(snapshot.FailureCount, 0, MaxTrashBalls);
            for (int i = 0; i < _balls.Length; i++) _balls[i].SetActive(i < balls);
            TrashBallCount = balls;

            int used = snapshot == null ? 0 : Mathf.Clamp(RuleConstants.DailySheets - snapshot.SheetsLeft, 0, RuleConstants.DailySheets);
            SetWindowColor(Color.Lerp(WindowDay, WindowSunset, used / (float)RuleConstants.DailySheets));
        }

        /// <summary>
        /// Door cut-away for the walking camera (south of the room, looking north): the 2.08 m door stands on the 0.5 m south
        /// wall line and would hide the character behind it, so it only casts shadows while the character is near it.
        /// </summary>
        public void UpdateCutaway(Vector2 characterXZ)
        {
            Build();
            var door = DoorSpec();
            bool hide = door != null
                && Mathf.Abs(characterXZ.x - (float)door.Center.X) < DoorCutawayHalfWidth
                && characterXZ.y < DoorCutawayZ;
            if (hide == DoorCutaway) return;
            DoorCutaway = hide;
            foreach (var r in _doorRenderers)
                if (r != null) r.shadowCastingMode = hide ? ShadowCastingMode.ShadowsOnly : ShadowCastingMode.On;
        }

        private void Awake() => Build();

        private void Build()
        {
            if (_built) return;
            _built = true;
            _mpb = new MaterialPropertyBlock();

            BuildLights();

            float w = (float)(RoomLayout.MaxX - RoomLayout.MinX), d = (float)(RoomLayout.MaxZ - RoomLayout.MinZ);
            float h = (float)RoomLayout.WallHeight;
            float t = WallThickness;
            var floor = Lit(FloorColor);
            var wall = Lit(WallColor);
            var wood = Lit(WoodColor);
            var dark = Lit(DarkWoodColor);

            BoxPart("Floor", new Vector3(w, t, d), new Vector3(0f, -t * 0.5f, 0f), floor);
            BoxPart("WallNorth", new Vector3(w + 2 * t, h, t), new Vector3(0f, h * 0.5f, (float)RoomLayout.MaxZ + t * 0.5f), wall);
            BoxPart("WallWest", new Vector3(t, h, d), new Vector3((float)RoomLayout.MinX - t * 0.5f, h * 0.5f, 0f), wall);
            BoxPart("WallEast", new Vector3(t, h, d), new Vector3((float)RoomLayout.MaxX + t * 0.5f, h * 0.5f, 0f), wall);
            BoxPart("WallSouth", new Vector3(w + 2 * t, SouthWallHeight, t),
                new Vector3(0f, SouthWallHeight * 0.5f, (float)RoomLayout.MinZ - t * 0.5f), wall);

            foreach (var f in RoomLayout.Furniture)
            {
                var parts = new List<GameObject>();
                bool model = LoadModels && BuildFurnitureModel(f, parts);
                if (!model)
                {
                    foreach (var p in parts) ModelFit.DestroySafe(p);
                    parts.Clear();
                    BuildFurnitureCode(f, wood, dark, parts);
                }
                _furniture[f.Id] = parts;
                _usesModel[f.Id] = model;
            }
            BuildTrashBalls();
            if (LoadModels) BuildProps();
            CollectDoorRenderers();

            // Display slots on the counter (paper scale 0.1, like the table).
            _paperMats = new[] { Lit(PaperFront), Lit(PaperBack), Unlit(Color.white) };
            for (int i = 0; i < _slots.Length; i++)
            {
                var go = new GameObject("Slot" + (i + 1));
                go.transform.SetParent(transform, false);
                go.transform.localPosition = new Vector3(SlotX[i], CounterTop, SlotZ);
                go.transform.localScale = Vector3.one * SlotScale;
                var mesh = PaperMeshBuilder.NewMesh("Display" + (i + 1));
                _slotMeshes[i] = mesh;
                _meshes.Add(mesh);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterials = _paperMats;
                go.SetActive(false);
                _slots[i] = go;
            }
            SetWindowColor(WindowDay);
        }

        private void BuildLights()
        {
            var lightGo = new GameObject("Sun");
            lightGo.transform.SetParent(transform, false);
            lightGo.transform.localRotation = SunRotation;
            var sun = lightGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = SunColor;
            sun.intensity = SunIntensity;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = SunShadowStrength;
            sun.shadowBias = 0.04f;
            sun.shadowNormalBias = 0.4f;
            Sun = sun;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = AmbientSky;
            RenderSettings.ambientEquatorColor = AmbientEquator;
            RenderSettings.ambientGroundColor = AmbientGround;
            RenderSettings.ambientIntensity = 1f;
        }

        // ---------------------------------------------------------------- furniture models

        /// <summary>Fits the models for <paramref name="f"/> into <paramref name="parts"/>; false if any is missing.</summary>
        private bool BuildFurnitureModel(FurnitureSpec f, List<GameObject> parts)
        {
            float cx = (float)f.Center.X, cz = (float)f.Center.Y;
            float w = (float)f.Width, d = (float)f.Depth, h = (float)f.Height;
            var at = new Vector3(cx, 0f, cz);
            switch (f.Id)
            {
                case "table":
                    return Add(parts, ModelFit.Spawn(ArtKeys.Table, transform, "Table", FitSpec.Box(new Vector3(w, h, d)), at, 0f));
                case "chair":
                    // Backrest at native +z; the seated character faces the table (+z), so the chair turns 180°.
                    return Add(parts, ModelFit.Spawn(ArtKeys.Chair, transform, "Chair", FitSpec.Box(new Vector3(w, ChairHeight, d)), at, 180f));
                case "bookshelf":
                {
                    if (!Add(parts, ModelFit.Spawn(ArtKeys.Bookshelf, transform, "Bookshelf", FitSpec.Box(new Vector3(w, h, d)), at, 0f)))
                        return false;
                    BuildBooks(f, parts[0]);
                    return true;
                }
                case "counter":
                {
                    // Two open display shelves side by side (ASSETS.md), each half the counter width; top = CounterTop.
                    var size = new Vector3(w * 0.5f, h, d);
                    return Add(parts, ModelFit.Spawn(ArtKeys.DisplayShelf, transform, "CounterWest", FitSpec.Box(size), new Vector3(cx - w * 0.25f, 0f, cz), 0f))
                        && Add(parts, ModelFit.Spawn(ArtKeys.DisplayShelf, transform, "CounterEast", FitSpec.Box(size), new Vector3(cx + w * 0.25f, 0f, cz), 0f));
                }
                case "bed":
                    // Uniform (ASSETS.md): footprint width 1.0, length 1.97; headboard at native +z = the north wall.
                    return Add(parts, ModelFit.Spawn(ArtKeys.Bed, transform, "Bed", FitSpec.Inside(new Vector3(w, 0f, d)), at, 0f));
                case "trash":
                    // Uniform (DECISIONS 2026-11-28: the bin is not work furniture): 0.36 × 0.73 × 0.40.
                    return Add(parts, ModelFit.Spawn(ArtKeys.TrashCan, transform, "TrashCan", FitSpec.Inside(new Vector3(w, 0f, d)), at, 0f));
                case "window":
                {
                    float z = cz - WindowInset;
                    for (int i = 0; i < 2; i++)
                    {
                        float x = cx + (i == 0 ? -0.5f : 0.5f) * WindowFrameWidth;
                        if (!Add(parts, ModelFit.Spawn(ArtKeys.Window, transform, "Window" + (i + 1), FitSpec.Height(WindowHeight),
                                new Vector3(x, WindowBottom, z), 0f)))
                            return false;
                    }
                    BuildSkyPane(cx, cz, 2f * WindowFrameWidth);
                    CollectGlass(parts);
                    return true;
                }
                case "door":
                    // Uniform, width 1.0 → 2.08 m high; depth centred on the south wall plane. See UpdateCutaway.
                    return Add(parts, ModelFit.Spawn(ArtKeys.Door, transform, "Door", FitSpec.Inside(new Vector3(w, 0f, 0f)), at, 0f));
                default:
                    return false;
            }
        }

        private static bool Add(List<GameObject> parts, GameObject go)
        {
            if (go == null) return false;
            parts.Add(go);
            return true;
        }

        /// <summary>Books on the three lower shelves: one set per bay, a fixed pattern of gaps, turns and offsets.</summary>
        private void BuildBooks(FurnitureSpec f, GameObject shelf)
        {
            if (!ModelFit.TryWorldBounds(shelf, out var b)) return;
            var prefab = ModelFit.Load(ArtKeys.Books);
            if (prefab == null) return;
            float cx = (float)f.Center.X;
            int n = 0;
            for (int s = 0; s < ShelfFractions.Length; s++)
            {
                float y = b.min.y + ShelfFractions[s] * b.size.y - transform.position.y;
                for (int bay = 0; bay < BayOffsets.Length; bay++)
                {
                    int k = s * BayOffsets.Length + bay;
                    if (k % 4 == 3) continue;   // a gap now and then
                    float x = cx + BayOffsets[bay] + ((k * 7) % 5 - 2) * 0.03f;
                    var go = ModelFit.Place(prefab, transform, "Books" + (++n), FitSpec.Scale(0.2f), new Vector3(x, y, BooksZ), (k % 2) * 180f);
                    AddProp(go);
                }
            }
        }

        private void BuildSkyPane(float cx, float cz, float width)
        {
            _windowMat = Unlit(WindowDay);
            // Just in front of the north wall, behind the glass (z = wall − 0.005).
            BoxPart("WindowSky", new Vector3(width, WindowHeight, 0.005f), new Vector3(cx, WindowBottom + WindowHeight * 0.5f, cz - 0.005f), _windowMat);
        }

        private void CollectGlass(List<GameObject> windows)
        {
            foreach (var wgo in windows)
                foreach (var r in wgo.GetComponentsInChildren<Renderer>(true))
                {
                    var mats = r.sharedMaterials;
                    for (int i = 0; i < mats.Length; i++)
                    {
                        var m = mats[i];
                        if (m != null && m.name.IndexOf(ArtKeys.WindowGlassMaterial, System.StringComparison.OrdinalIgnoreCase) >= 0)
                            _glass.Add((r, i));
                    }
                }
        }

        private void SetWindowColor(Color c)
        {
            _windowColor = c;
            if (_windowMat != null) _windowMat.color = c;
            if (_glass.Count == 0) return;
            var tint = new Color(c.r, c.g, c.b, GlassAlpha);
            foreach (var (r, index) in _glass)
            {
                if (r == null) continue;
                _mpb.Clear();
                _mpb.SetColor(GlassColorProperty, tint);
                r.SetPropertyBlock(_mpb, index);
            }
        }

        private FurnitureSpec DoorSpec()
        {
            foreach (var f in RoomLayout.Furniture)
                if (f.Id == "door") return f;
            return null;
        }

        private void CollectDoorRenderers()
        {
            _doorRenderers.Clear();
            if (!_furniture.TryGetValue("door", out var parts)) return;
            foreach (var p in parts)
                if (p != null) _doorRenderers.AddRange(p.GetComponentsInChildren<Renderer>(true));
        }

        /// <summary>Trash balls on top of whatever trash is drawn (model rim or code cylinder top).</summary>
        private void BuildTrashBalls()
        {
            float cx = 0f, cz = 0f, top = 0.5f;
            foreach (var f in RoomLayout.Furniture)
                if (f.Id == "trash")
                {
                    cx = (float)f.Center.X;
                    cz = (float)f.Center.Y;
                    top = (float)f.Height;
                }
            bool model = UsesModel("trash");
            if (model && TryFurnitureBounds("trash", out var b)) top = b.max.y - transform.position.y;
            // Model (open bin): the first row sits just inside the rim. Code cylinder (closed top): on the lid, as before 019.
            float baseY = model ? top - BallRadius * 0.6f : top + 0.03f;
            var ballMesh = Track(MeshFactory.Sphere(BallRadius, 10));
            var ballMat = Lit(BallColor);
            for (int i = 0; i < _balls.Length; i++)
            {
                float a = i * 2.4f;
                float rr = (i % 3) * 0.05f;
                var pos = new Vector3(cx + Mathf.Cos(a) * rr, baseY + (i / 3) * 0.07f, cz + Mathf.Sin(a) * rr);
                _balls[i] = MeshPart("Ball" + i, ballMesh, pos, ballMat);
                _balls[i].SetActive(false);
            }
        }

        // ---------------------------------------------------------------- props (decoration, not in Core)

        private void BuildProps()
        {
            var nightstand = Prop(ArtKeys.Nightstand, "Nightstand", FitSpec.Scale(0.175f), NightstandAt, 0f);
            float standTop = nightstand != null && ModelFit.TryWorldBounds(nightstand, out var nb) ? nb.max.y - transform.position.y : 0.46f;
            var lampAt = new Vector3(NightstandAt.x, standTop, NightstandAt.z);
            var lamp = Prop(ArtKeys.LampTable, "LampTable", FitSpec.Scale(0.12f), lampAt, 0f);
            if (lamp != null)
            {
                var lightGo = new GameObject("LampLight");
                lightGo.transform.SetParent(transform, false);
                lightGo.transform.localPosition = lampAt + new Vector3(0f, 0.3f, -0.12f);
                var l = lightGo.AddComponent<Light>();
                l.type = LightType.Point;
                l.color = LampColor;
                l.intensity = LampIntensity;
                l.range = LampRange;
                l.shadows = LightShadows.None;
                LampLight = l;
            }
            Prop(ArtKeys.LampFloor, "LampFloor", FitSpec.Height(1.6f), FloorLampAt, 0f);
            Prop(ArtKeys.Rug, "Rug", FitSpec.Scale(0.15f), RugAt, 0f);
            Prop(ArtKeys.PottedPlant, "PottedPlant", FitSpec.Height(1.1f), PottedPlantAt, 0f);
            Prop(ArtKeys.Box, "PaperBox", FitSpec.Scale(0.15f), PaperBoxAt, 0f);
            Prop(ArtKeys.CoatRack, "CoatRack", FitSpec.Height(1.7f), CoatRackAt, 0f);
            if (UsesModel("bookshelf") && TryFurnitureBounds("bookshelf", out var sb))
                Prop(ArtKeys.PlantSmall, "PlantSmall", FitSpec.Scale(0.2f), new Vector3(SmallPlantX, sb.max.y - transform.position.y, sb.center.z - transform.position.z), 0f);
            BuildPaneling();
        }

        /// <summary>Wooden wainscot (1.0 × 0.89 m tiles) along the north, east and west walls, facing the room.</summary>
        private void BuildPaneling()
        {
            var prefab = ModelFit.Load(ArtKeys.Paneling);
            if (prefab == null) return;
            var fit = FitSpec.Box(new Vector3(1f, 0.89f, 0.06f));
            float inset = 0.03f;
            int n = 0;
            for (float x = (float)RoomLayout.MinX + 0.5f; x < (float)RoomLayout.MaxX; x += 1f)
            {
                if (Mathf.Abs(x) < 0.8f) continue;   // behind the bookshelf
                AddProp(ModelFit.Place(prefab, transform, "Panel" + (++n), fit, new Vector3(x, 0f, (float)RoomLayout.MaxZ - inset), 0f));
            }
            for (float z = (float)RoomLayout.MinZ + 0.5f; z < (float)RoomLayout.MaxZ; z += 1f)
            {
                AddProp(ModelFit.Place(prefab, transform, "Panel" + (++n), fit, new Vector3((float)RoomLayout.MaxX - inset, 0f, z), 90f));
                AddProp(ModelFit.Place(prefab, transform, "Panel" + (++n), fit, new Vector3((float)RoomLayout.MinX + inset, 0f, z), -90f));
            }
        }

        private GameObject Prop(string key, string name, FitSpec fit, Vector3 at, float yaw) =>
            AddProp(ModelFit.Spawn(key, transform, name, fit, at, yaw));

        private GameObject AddProp(GameObject go)
        {
            if (go == null) return null;
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                r.shadowCastingMode = ShadowCastingMode.On;
                r.receiveShadows = true;
            }
            _props.Add(go);
            return go;
        }

        // ---------------------------------------------------------------- code-built fallback (pre-019 meshes)

        private void BuildFurnitureCode(FurnitureSpec f, Material wood, Material dark, List<GameObject> parts)
        {
            float cx = (float)f.Center.X, cz = (float)f.Center.Y;
            float w = (float)f.Width, d = (float)f.Depth, h = (float)f.Height;
            switch (f.Id)
            {
                case "table":
                    parts.Add(BoxPart("TableTop", new Vector3(w, 0.05f, d), new Vector3(cx, h - 0.025f, cz), wood));
                    for (int i = 0; i < 4; i++)
                    {
                        float lx = cx + (i % 2 == 0 ? -1 : 1) * (w * 0.5f - 0.06f);
                        float lz = cz + (i < 2 ? -1 : 1) * (d * 0.5f - 0.06f);
                        parts.Add(BoxPart("TableLeg" + i, new Vector3(0.06f, h - 0.05f, 0.06f), new Vector3(lx, (h - 0.05f) * 0.5f, lz), dark));
                    }
                    break;
                case "chair":
                    parts.Add(BoxPart("ChairSeat", new Vector3(w, 0.06f, d), new Vector3(cx, h - 0.03f, cz), wood));
                    parts.Add(BoxPart("ChairBase", new Vector3(w * 0.8f, h - 0.06f, d * 0.8f), new Vector3(cx, (h - 0.06f) * 0.5f, cz), dark));
                    parts.Add(BoxPart("ChairBack", new Vector3(w, 0.5f, 0.05f), new Vector3(cx, h + 0.25f, cz - d * 0.5f + 0.025f), wood));
                    break;
                case "bookshelf":
                    parts.Add(BoxPart("Bookshelf", new Vector3(w, h, d), new Vector3(cx, h * 0.5f, cz), dark));
                    for (int row = 0; row < 3; row++)
                        for (int i = 0; i < 4; i++)
                        {
                            var mat = Lit(BookColors[(row + i) % BookColors.Length]);
                            float bx = cx - w * 0.5f + 0.25f + i * 0.36f;
                            parts.Add(BoxPart("Book" + row + i, new Vector3(0.24f, 0.32f, 0.06f), new Vector3(bx, 0.35f + row * 0.6f, cz - d * 0.5f - 0.03f), mat));
                        }
                    break;
                case "counter":
                    parts.Add(BoxPart("Counter", new Vector3(w, h, d), new Vector3(cx, h * 0.5f, cz), wood));
                    break;
                case "bed":
                    parts.Add(BoxPart("BedBase", new Vector3(w, h * 0.6f, d), new Vector3(cx, h * 0.3f, cz), dark));
                    parts.Add(BoxPart("Blanket", new Vector3(w * 0.96f, h * 0.4f, d * 0.7f), new Vector3(cx, h * 0.8f, cz - d * 0.15f), Lit(BedColor)));
                    parts.Add(BoxPart("Pillow", new Vector3(w * 0.7f, h * 0.3f, d * 0.18f), new Vector3(cx, h * 0.75f, cz + d * 0.38f), Lit(PillowColor)));
                    break;
                case "trash":
                    parts.Add(CylPart("Trash", w * 0.5f, h, new Vector3(cx, 0f, cz), Lit(TrashColor)));
                    break;
                case "window":
                    parts.Add(BoxPart("WindowFrame", new Vector3(w + 0.1f, h + 0.1f, 0.04f), new Vector3(cx, WindowBottom + h * 0.5f, cz - 0.02f), dark));
                    _windowMat = Unlit(WindowDay);
                    BoxPart("Window", new Vector3(w, h, 0.02f), new Vector3(cx, WindowBottom + h * 0.5f, cz - 0.05f), _windowMat);
                    break;
                case "door":
                    parts.Add(BoxPart("Door", new Vector3(w, h, 0.06f), new Vector3(cx, h * 0.5f, cz + 0.03f), Lit(DoorColor)));
                    break;
            }
        }

        private Material Lit(Color c) => TrackMat(Materials.Lit(c));

        private Material Unlit(Color c) => TrackMat(Materials.Unlit(c));

        private Material TrackMat(Material m)
        {
            _materials.Add(m);
            return m;
        }

        private Mesh Track(Mesh m)
        {
            _meshes.Add(m);
            return m;
        }

        private GameObject BoxPart(string name, Vector3 size, Vector3 center, Material mat) =>
            MeshPart(name, Track(MeshFactory.Box(size)), center, mat);

        private GameObject CylPart(string name, float radius, float height, Vector3 bottom, Material mat) =>
            MeshPart(name, Track(MeshFactory.Cylinder(radius, height)), bottom, mat);

        private GameObject MeshPart(string name, Mesh mesh, Vector3 pos, Material mat)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = pos;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.On;
            r.receiveShadows = true;
            return go;
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
