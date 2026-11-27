using Sandbox.Core.Scoring;
using UnityEngine;

namespace Sandbox.Runtime.Gameplay
{
    /// <summary>
    /// F-001 Unity bridge (INTERFACE_CONTRACTS.md C-004). Sole owner of the runtime <see cref="ScoreCounter"/>.
    /// Each frame it reads <c>Time.unscaledTimeAsDouble</c> once, polls the legacy Input Manager
    /// (R key = reset, Space / left mouse button = press) and stores <c>GetState(t)</c> in <see cref="Current"/>.
    /// Views only read <see cref="Current"/> (ideally in LateUpdate, after this Update ran).
    /// </summary>
    /// <remarks>
    /// The runtime instance lives on the GameObject <see cref="ObjectName"/>, created by
    /// <see cref="EnsureInstance"/> (called from this class' AfterSceneLoad hook and from the UI's hook,
    /// so creation does not depend on hook order or on <c>Bootstrap</c>). Additional controllers created
    /// manually (e.g. by tests) are independent and never replace <see cref="Instance"/>.
    /// Main thread only.
    /// </remarks>
    public sealed class ScoreCounterController : MonoBehaviour
    {
        /// <summary>Name of the GameObject that carries the runtime controller.</summary>
        public const string ObjectName = "[ScoreCounter]";

        private static ScoreCounterController s_instance;

        private readonly ScoreCounter _counter = new ScoreCounter();

        /// <summary>The runtime controller created by <see cref="EnsureInstance"/>, or null if none exists.</summary>
        public static ScoreCounterController Instance => s_instance != null ? s_instance : null;

        /// <summary>
        /// Result of the latest <c>GetState</c> call. Starts at <see cref="ScoreSnapshot.Initial"/> (R-2).
        /// </summary>
        public ScoreSnapshot Current { get; private set; } = ScoreSnapshot.Initial;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Init()
        {
            EnsureInstance();
        }

        /// <summary>
        /// Returns the runtime controller, creating the persistent <see cref="ObjectName"/> GameObject
        /// (DontDestroyOnLoad) if it does not exist yet. Idempotent: never creates a duplicate.
        /// </summary>
        public static ScoreCounterController EnsureInstance()
        {
            if (s_instance != null) return s_instance;

            var existing = GameObject.Find(ObjectName);
            if (existing != null)
            {
                var found = existing.GetComponent<ScoreCounterController>();
                if (found == null) found = existing.AddComponent<ScoreCounterController>();
                s_instance = found;
                return found;
            }

            var go = new GameObject(ObjectName);
            DontDestroyOnLoad(go);
            s_instance = go.AddComponent<ScoreCounterController>();
            return s_instance;
        }

        /// <summary>Registers one press at <paramref name="time"/> seconds, then sets Current = GetState(time).</summary>
        /// <remarks>Invalid times (NaN, ±Infinity, earlier than the last valid press) are ignored by Core.</remarks>
        public void SubmitPress(double time)
        {
            _counter.Press(time);
            Current = _counter.GetState(time);
        }

        /// <summary>Resets the counter (R-005), then refreshes <see cref="Current"/> (always Initial afterwards).</summary>
        public void SubmitReset()
        {
            _counter.Reset();
            Current = _counter.GetState(Time.unscaledTimeAsDouble);
        }

        /// <summary>
        /// One frame of C-004 processing with already-decoded input, in contract order:
        /// reset first, then Space press, then click press (each a separate Press with the same time),
        /// then Current = GetState(time) — also on frames without input so combo expiry (R-004) shows.
        /// <see cref="Update"/> calls this with the real input; tests call it directly.
        /// </summary>
        public void ProcessFrame(double time, bool resetDown, bool spaceDown, bool clickDown)
        {
            if (resetDown) _counter.Reset();
            if (spaceDown) _counter.Press(time);
            if (clickDown) _counter.Press(time);
            Current = _counter.GetState(time);
        }

        private void Update()
        {
            double t = Time.unscaledTimeAsDouble; // read once per frame (C-004)
            ProcessFrame(
                t,
                Input.GetKeyDown(KeyCode.R),
                Input.GetKeyDown(KeyCode.Space),
                Input.GetMouseButtonDown(0));
        }

        private void OnDestroy()
        {
            if (s_instance == this) s_instance = null;
        }
    }
}
