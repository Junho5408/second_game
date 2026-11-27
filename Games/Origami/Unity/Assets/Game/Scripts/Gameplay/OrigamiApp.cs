using Origami.Core.Catalog;
using Origami.Core.Game;
using Origami.Core.Room;
using Origami.Core.Screens;
using Origami.Runtime.Book;
using Origami.Runtime.Folding;
using UnityEngine;

namespace Origami.Runtime.Gameplay
{
    /// <summary>
    /// Root of the v2 game (C-193, ARCHITECTURE §4): sole owner of the <see cref="ScreenFlow"/> and the only <c>Update</c>.
    /// Children (created in <c>Awake</c>): <see cref="WorkshopView"/>, <see cref="CharacterView"/>, <see cref="FoldTable"/>,
    /// <see cref="BookPanel"/>, <see cref="CameraRig"/>. The GUI (<c>OrigamiGui</c>, the only <c>OnGUI</c>) sits on the same
    /// GameObject and registers itself with <c>OrigamiGui.Bind</c>.
    /// <para>
    /// <see cref="ProcessFrame"/> order (C-193): (1) keys → <c>Flow.ProcessKeys</c> unless a drag is in progress without an
    /// overlay, (2) mode changes move the character (seat → chair, stand up → (2.5, 0.2) facing south, new day / new run →
    /// start position facing east), (3) walking without overlay → <c>RoomMotion.Step</c>, (4) <c>Table.ProcessFrame</c> (the pointer only while seated),
    /// (5) <c>CameraRig.Tick</c>, (6) <c>Workshop.Refresh</c> when the snapshot reference changed, then the GUI refresh.
    /// Tests call ProcessFrame directly with a fixed dt (A2-5).
    /// </para>
    /// </summary>
    /// <remarks>
    /// The runtime instance lives on <see cref="ObjectName"/> (DontDestroyOnLoad), created by <see cref="EnsureInstance"/> from
    /// this class' AfterSceneLoad hook (Bootstrap is not changed). Apps added by tests are independent and never become
    /// <see cref="Instance"/>. Main thread only.
    /// </remarks>
    public sealed class OrigamiApp : MonoBehaviour
    {
        public const string ObjectName = "[Origami]";

        /// <summary>Yaw (degrees) for east / south / north (0 = north +z, 90 = east +x).</summary>
        internal const float YawEast = 90f, YawSouth = 180f, YawNorth = 0f;

        /// <summary>Walk bob phase advance per metre walked (radians).</summary>
        private const float PhasePerMetre = 5f;

        private static OrigamiApp s_instance;

        private ScreenFlow _flow;
        private FoldTable _table;
        private BookPanel _book;
        private CharacterView _character;
        private CameraRig _rig;
        private WorkshopView _workshop;
        private Vector2 _position;
        private float _yaw = YawEast;
        private float _walkPhase;
        private WorkshopMode _lastMode;
        private int _lastDay;
        private int _lastChangeSerial;
        private GameSnapshot _shownSnapshot;
        private bool _built;

        /// <summary>The runtime instance created by <see cref="EnsureInstance"/>; null (real null) if none or destroyed.</summary>
        public static OrigamiApp Instance => s_instance != null ? s_instance : null;

        /// <summary>The screen flow (created in Awake, never replaced).</summary>
        public ScreenFlow Flow
        {
            get
            {
                Build();
                return _flow;
            }
        }

        public FoldTable Table => Built(() => _table);
        public BookPanel Book => Built(() => _book);
        public CharacterView Character => Built(() => _character);
        public CameraRig CameraRig => Built(() => _rig);
        public WorkshopView Workshop => Built(() => _workshop);

        /// <summary>World (x, z) of the character.</summary>
        public Vector2 CharacterPosition => _position;

        /// <summary>Degrees, 0 = north (+z), 90 = east (+x).</summary>
        public float CharacterYaw => _yaw;

        /// <summary>While walking (no overlay): <c>Interaction.Nearest</c> of the character position; otherwise null.</summary>
        public string InteractCandidate
        {
            get
            {
                var flow = Flow;
                if (flow.Overlay != Overlay.None || flow.Snapshot.Mode != WorkshopMode.Walking) return null;
                return Interaction.Nearest(WorldConvert.ToVec2(_position));
            }
        }

        /// <summary>The registered screen layer (OrigamiGui), null if none.</summary>
        internal IAppView View { get; set; }

        /// <summary>Mouse position of the previous <see cref="InputReader.Read"/> (right-drag delta).</summary>
        internal Vector2? LastMouse { get; set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Init()
        {
            EnsureInstance();
        }

        /// <summary>
        /// Returns the runtime app. Idempotent: <see cref="Instance"/> if alive, else the app on an existing
        /// <see cref="ObjectName"/> GameObject (added if missing), else a new persistent <see cref="ObjectName"/> GameObject.
        /// </summary>
        public static OrigamiApp EnsureInstance()
        {
            if (s_instance != null) return s_instance;
            var existing = GameObject.Find(ObjectName);
            if (existing != null)
            {
                var found = existing.GetComponent<OrigamiApp>();
                if (found == null) found = existing.AddComponent<OrigamiApp>();
                s_instance = found;
                return found;
            }
            var go = new GameObject(ObjectName);
            DontDestroyOnLoad(go);
            s_instance = go.AddComponent<OrigamiApp>();
            return s_instance;
        }

        /// <summary>One frame (see the class remarks). Negative/NaN dt counts as 0.</summary>
        public void ProcessFrame(InputFrame frame, float dt)
        {
            Build();
            if (float.IsNaN(dt) || dt < 0f) dt = 0f;
            var flow = _flow;

            // (1) keys (Esc while dragging cancels the drag in the table instead).
            if (!(flow.Overlay == Overlay.None && _table.IsDragging)) flow.ProcessKeys(frame.Keys, InteractCandidate);

            // (2) mode changes.
            SyncMode();

            // (3) walking.
            if (flow.Snapshot.Mode == WorkshopMode.Walking && flow.Overlay == Overlay.None)
            {
                var move = frame.Move;
                if (float.IsNaN(move.x) || float.IsInfinity(move.x)) move.x = 0f;
                if (float.IsNaN(move.y) || float.IsInfinity(move.y)) move.y = 0f;
                var before = _position;
                _position = WorldConvert.ToVector2(RoomMotion.Step(WorldConvert.ToVec2(_position), WorldConvert.ToVec2(move), dt));
                float walked = (_position - before).magnitude;
                if (move.sqrMagnitude > 1e-6f) _yaw = Mathf.Atan2(move.x, move.y) * Mathf.Rad2Deg;
                _walkPhase = walked > 0f ? (_walkPhase + walked * PhasePerMetre) % (2f * Mathf.PI) : 0f;
            }
            _character.SetPose(_position, _yaw, flow.Snapshot.Mode == WorkshopMode.Seated, _walkPhase);

            // (4) table, (5) camera, (6) room.
            // Outside the seated mode the pointer is not passed on (no orbit/zoom of the seated view while walking).
            _table.ProcessFrame(flow.Snapshot.Mode == WorkshopMode.Seated ? frame.Pointer : new PointerFrame(), dt);
            _rig.Tick(dt);
            RefreshWorkshop();

            View?.Refresh(dt);
        }

        /// <summary>Places the character (tests, layout). Yaw in degrees.</summary>
        public void SetCharacterPosition(Vector2 xz, float yaw)
        {
            Build();
            _position = xz;
            _yaw = yaw;
            _character.SetPose(_position, _yaw, Flow.Snapshot.Mode == WorkshopMode.Seated, 0f);
        }

        private void Update()
        {
            ProcessFrame(InputReader.Read(this), Time.deltaTime);
        }

        private void Awake()
        {
            Build();
        }

        private void OnDestroy()
        {
            if (s_instance == this) s_instance = null;
        }

        /// <summary>Builds if needed, then the child component normalised to a real null.</summary>
        private T Built<T>(System.Func<T> get) where T : Object
        {
            Build();
            var c = get();
            return c != null ? c : null;
        }

        private void Build()
        {
            if (_built) return;
            _built = true;
            _flow = new ScreenFlow();

            // Review ORIGAMI-009 I-1: the recognition catalogue costs ~0.35 s on first use — pay it at load, not at the first Finish.
            _ = ModelCatalog.Recognition;

            _workshop = Child<WorkshopView>("Workshop");
            _character = Child<CharacterView>("Character");
            _table = Child<FoldTable>(FoldTable.ObjectName);
            _table.Bind(new FoldHostAdapter(_flow));
            _book = Child<BookPanel>("BookPanel");
            _book.Bind(new BookHostAdapter(_flow));
            _rig = Child<CameraRig>("CameraRig");
            _rig.Bind(this);

            var s = _flow.Snapshot;
            _position = WorldConvert.ToVector2(RoomLayout.StartPosition);
            _yaw = YawEast;
            _lastMode = s.Mode;
            _lastDay = s.Day;
            _lastChangeSerial = _flow.LastSheetChange.Serial;
            _character.SetPose(_position, _yaw, false, 0f);
            RefreshWorkshop();
        }

        private T Child<T>(string name) where T : Component
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            return go.AddComponent<T>();
        }

        private void SyncMode()
        {
            var flow = _flow;
            var s = flow.Snapshot;
            var change = flow.LastSheetChange;
            bool reset = change.Serial != _lastChangeSerial && change.Kind == SheetChangeKind.Reset;
            _lastChangeSerial = change.Serial;

            if (reset || s.Day != _lastDay)
            {
                // New day (settlement) or new run: morning position (F-003 AC-07, D-130).
                _position = WorldConvert.ToVector2(RoomLayout.StartPosition);
                _yaw = YawEast;
                _walkPhase = 0f;
                _rig.Snap();
            }
            else if (s.Mode != _lastMode)
            {
                if (s.Mode == WorkshopMode.Seated)
                {
                    _position = WorldConvert.ToVector2(RoomLayout.ChairPosition);
                    _yaw = YawNorth;   // facing the table (north of the chair)
                }
                else if (_lastMode == WorkshopMode.Seated)
                {
                    _position = WorldConvert.ToVector2(RoomLayout.StandUpPosition);
                    _yaw = YawSouth;
                }
                _walkPhase = 0f;
            }
            _lastMode = s.Mode;
            _lastDay = s.Day;
        }

        private void RefreshWorkshop()
        {
            var s = _flow.Snapshot;
            if (ReferenceEquals(s, _shownSnapshot)) return;
            _shownSnapshot = s;
            _workshop.Refresh(s);
        }
    }
}
