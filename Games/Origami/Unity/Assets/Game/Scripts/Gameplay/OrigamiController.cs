using Origami.Core.Flow;
using UnityEngine;

namespace Origami.Runtime.Gameplay
{
    /// <summary>
    /// F-007 Unity bridge (INTERFACE_CONTRACTS.md C-030). Sole owner of the runtime <see cref="OrigamiFlow"/>.
    /// Each frame <see cref="Update"/> polls the legacy Input Manager once (<see cref="ReadKeys"/>) and hands the
    /// decoded keys to <see cref="ProcessFrame"/>; on-screen buttons (OrigamiView) call the same Flow methods,
    /// so a key and its button are the same command by construction (F-007 AC-05).
    /// </summary>
    /// <remarks>
    /// The runtime instance lives on the GameObject <see cref="ObjectName"/>, created by <see cref="EnsureInstance"/>
    /// (called from this class' AfterSceneLoad hook and from OrigamiView's hook, so creation does not depend on
    /// hook order or on <c>Bootstrap</c>). Controllers added manually (e.g. by tests) are independent and never
    /// replace <see cref="Instance"/>. Main thread only.
    /// </remarks>
    public sealed class OrigamiController : MonoBehaviour
    {
        /// <summary>Name of the GameObject that carries the runtime controller.</summary>
        public const string ObjectName = "[Origami]";

        private static OrigamiController s_instance;

        private readonly OrigamiFlow _flow = new OrigamiFlow();

        /// <summary>The runtime controller created by <see cref="EnsureInstance"/>; null (real null) if none or destroyed.</summary>
        public static OrigamiController Instance => s_instance != null ? s_instance : null;

        /// <summary>The screen flow of this controller. Created with the component, never replaced.</summary>
        public OrigamiFlow Flow => _flow;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Init()
        {
            EnsureInstance();
        }

        /// <summary>
        /// Returns the runtime controller. Idempotent: <see cref="Instance"/> if alive, else the controller on an
        /// existing <see cref="ObjectName"/> GameObject (added if missing), else a new persistent
        /// (DontDestroyOnLoad) <see cref="ObjectName"/> GameObject.
        /// </summary>
        public static OrigamiController EnsureInstance()
        {
            if (s_instance != null) return s_instance;

            var existing = GameObject.Find(ObjectName);
            if (existing != null)
            {
                var found = existing.GetComponent<OrigamiController>();
                if (found == null) found = existing.AddComponent<OrigamiController>();
                s_instance = found;
                return found;
            }

            var go = new GameObject(ObjectName);
            DontDestroyOnLoad(go);
            s_instance = go.AddComponent<OrigamiController>();
            return s_instance;
        }

        /// <summary>
        /// One frame of already-decoded keys: exactly <c>Flow.ProcessKeys(keys)</c> (only the highest-priority key
        /// counts, A-4). Returns true if the flow did something. Tests call this directly (frame/time independent).
        /// </summary>
        public bool ProcessFrame(FlowKeys keys)
        {
            return _flow.ProcessKeys(keys);
        }

        /// <summary>
        /// Polls the legacy Input Manager with <c>Input.GetKeyDown</c> (no key repeat) and maps it to
        /// <see cref="FlowKeys"/> (C-030): ↑/W FoldUp, ↓/S FoldDown, ←/A FoldLeft, →/D FoldRight, Space Flip,
        /// H Hint, Esc Escape, Return/KeypadEnter Enter. Several keys may be set; priority is decided by the flow.
        /// </summary>
        public static FlowKeys ReadKeys()
        {
            var keys = FlowKeys.None;
            if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W)) keys |= FlowKeys.FoldUp;
            if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S)) keys |= FlowKeys.FoldDown;
            if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A)) keys |= FlowKeys.FoldLeft;
            if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D)) keys |= FlowKeys.FoldRight;
            if (Input.GetKeyDown(KeyCode.Space)) keys |= FlowKeys.Flip;
            if (Input.GetKeyDown(KeyCode.H)) keys |= FlowKeys.Hint;
            if (Input.GetKeyDown(KeyCode.Escape)) keys |= FlowKeys.Escape;
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) keys |= FlowKeys.Enter;
            return keys;
        }

        private void Update()
        {
            ProcessFrame(ReadKeys());
        }

        private void OnDestroy()
        {
            if (s_instance == this) s_instance = null;
        }
    }
}
