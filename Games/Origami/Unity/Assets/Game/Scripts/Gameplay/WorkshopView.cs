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
    /// The workshop room built from code meshes (C-193, CONTENT §6, D-130, ARCHITECTURE §7): floor, walls (the south wall
    /// only 0.5 m high), table and chair, bookshelf, counter with 5 display slots, bed, trash can, window, door, one soft-shadow
    /// directional light and flat ambient light. No primitives, colliders or physics — collision is Core <c>RoomMotion</c>.
    /// <para>
    /// <see cref="Refresh"/> shows the snapshot: display slot i shows <c>Display[i].Shape</c> (the sheet as the player folded
    /// it, F-010 AC-06), the trash shows min(failures, 10) paper balls, and the window colour goes from day to sunset with the
    /// sheets used today (0–5). No Update — <see cref="OrigamiApp"/> calls Refresh when the snapshot reference changes.
    /// </para>
    /// </summary>
    public sealed class WorkshopView : MonoBehaviour
    {
        /// <summary>Display slot x positions on the counter top (D-130, view values).</summary>
        internal static readonly float[] SlotX = { 1.7f, 2.1f, 2.5f, 2.9f, 3.3f };
        internal const float SlotZ = -2.3f, CounterTop = 1.0f, SlotScale = 0.1f;
        internal const int MaxTrashBalls = 10;

        private const float WallThickness = 0.1f, SouthWallHeight = 0.5f;

        private static readonly Color FloorColor = new Color(0.62f, 0.5f, 0.38f);
        private static readonly Color WallColor = new Color(0.9f, 0.87f, 0.8f);
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

        private readonly List<Mesh> _meshes = new List<Mesh>();
        private readonly List<Material> _materials = new List<Material>();
        private readonly GameObject[] _slots = new GameObject[5];
        private readonly Mesh[] _slotMeshes = new Mesh[5];
        private readonly GameObject[] _balls = new GameObject[MaxTrashBalls];
        private Material _windowMat;
        private Material[] _paperMats;
        private bool _built;

        /// <summary>Display slots showing a piece after the last <see cref="Refresh"/>.</summary>
        public int DisplayedPieceCount { get; private set; }

        /// <summary>Paper balls visible in the trash can (min(failures, 10)).</summary>
        public int TrashBallCount { get; private set; }

        /// <summary>Window colour of the last Refresh (tests).</summary>
        internal Color WindowColor => _windowMat != null ? _windowMat.color : WindowDay;

        /// <summary>Slot GameObject (tests): active when it shows a piece.</summary>
        internal GameObject Slot(int i) => i >= 0 && i < _slots.Length ? _slots[i] : null;

        internal Mesh SlotMesh(int i) => i >= 0 && i < _slotMeshes.Length ? _slotMeshes[i] : null;

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
            _windowMat.color = Color.Lerp(WindowDay, WindowSunset, used / (float)RuleConstants.DailySheets);
        }

        private void Awake() => Build();

        private void Build()
        {
            if (_built) return;
            _built = true;

            // Light (Fold3DSpike values) + flat ambient.
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

            foreach (var f in RoomLayout.Furniture) BuildFurniture(f, wood, dark);

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
        }

        private void BuildFurniture(FurnitureSpec f, Material wood, Material dark)
        {
            float cx = (float)f.Center.X, cz = (float)f.Center.Y;
            float w = (float)f.Width, d = (float)f.Depth, h = (float)f.Height;
            switch (f.Id)
            {
                case "table":
                    BoxPart("TableTop", new Vector3(w, 0.05f, d), new Vector3(cx, h - 0.025f, cz), wood);
                    for (int i = 0; i < 4; i++)
                    {
                        float lx = cx + (i % 2 == 0 ? -1 : 1) * (w * 0.5f - 0.06f);
                        float lz = cz + (i < 2 ? -1 : 1) * (d * 0.5f - 0.06f);
                        BoxPart("TableLeg" + i, new Vector3(0.06f, h - 0.05f, 0.06f), new Vector3(lx, (h - 0.05f) * 0.5f, lz), dark);
                    }
                    break;
                case "chair":
                    BoxPart("ChairSeat", new Vector3(w, 0.06f, d), new Vector3(cx, h - 0.03f, cz), wood);
                    BoxPart("ChairBase", new Vector3(w * 0.8f, h - 0.06f, d * 0.8f), new Vector3(cx, (h - 0.06f) * 0.5f, cz), dark);
                    BoxPart("ChairBack", new Vector3(w, 0.5f, 0.05f), new Vector3(cx, h + 0.25f, cz - d * 0.5f + 0.025f), wood);
                    break;
                case "bookshelf":
                    BoxPart("Bookshelf", new Vector3(w, h, d), new Vector3(cx, h * 0.5f, cz), dark);
                    for (int row = 0; row < 3; row++)
                        for (int i = 0; i < 4; i++)
                        {
                            var mat = Lit(BookColors[(row + i) % BookColors.Length]);
                            float bx = cx - w * 0.5f + 0.25f + i * 0.36f;
                            BoxPart("Book" + row + i, new Vector3(0.24f, 0.32f, 0.06f), new Vector3(bx, 0.35f + row * 0.6f, cz - d * 0.5f - 0.03f), mat);
                        }
                    break;
                case "counter":
                    BoxPart("Counter", new Vector3(w, h, d), new Vector3(cx, h * 0.5f, cz), wood);
                    break;
                case "bed":
                    BoxPart("BedBase", new Vector3(w, h * 0.6f, d), new Vector3(cx, h * 0.3f, cz), dark);
                    BoxPart("Blanket", new Vector3(w * 0.96f, h * 0.4f, d * 0.7f), new Vector3(cx, h * 0.8f, cz - d * 0.15f), Lit(BedColor));
                    BoxPart("Pillow", new Vector3(w * 0.7f, h * 0.3f, d * 0.18f), new Vector3(cx, h * 0.75f, cz + d * 0.38f), Lit(PillowColor));
                    break;
                case "trash":
                    CylPart("Trash", w * 0.5f, h, new Vector3(cx, 0f, cz), Lit(TrashColor));
                    var ballMesh = Track(MeshFactory.Sphere(0.07f, 10));
                    var ballMat = Lit(BallColor);
                    for (int i = 0; i < _balls.Length; i++)
                    {
                        float a = i * 2.4f;
                        float rr = (i % 3) * 0.05f;
                        var pos = new Vector3(cx + Mathf.Cos(a) * rr, h + 0.03f + (i / 3) * 0.07f, cz + Mathf.Sin(a) * rr);
                        _balls[i] = MeshPart("Ball" + i, ballMesh, pos, ballMat);
                        _balls[i].SetActive(false);
                    }
                    break;
                case "window":
                    _windowMat = Unlit(WindowDay);
                    BoxPart("WindowFrame", new Vector3(w + 0.1f, h + 0.1f, 0.04f), new Vector3(cx, 1.5f, cz - 0.02f), dark);
                    BoxPart("Window", new Vector3(w, h, 0.02f), new Vector3(cx, 1.5f, cz - 0.05f), _windowMat);
                    break;
                case "door":
                    BoxPart("Door", new Vector3(w, h, 0.06f), new Vector3(cx, h * 0.5f, cz + 0.03f), Lit(DoorColor));
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
