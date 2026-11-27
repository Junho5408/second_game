using System.Globalization;
using Sandbox.Core.Scoring;
using Sandbox.Runtime.Gameplay;
using UnityEngine;

namespace Sandbox.Runtime.UI
{
    /// <summary>
    /// On-screen text for F-001: score, combo and multiplier (C-004 "표시").
    /// Reads <see cref="ScoreCounterController.Current"/> in LateUpdate (after the controller's Update)
    /// into <see cref="Text"/>, and draws it with IMGUI (<c>OnGUI</c> + <c>GUI.Label</c>).
    /// </summary>
    /// <remarks>
    /// IMGUI is used instead of UI Toolkit because a code-built UI Toolkit panel needs a PanelSettings
    /// theme asset, and this project is code-first with no Editor access. Dependency direction is
    /// UI → Gameplay (ARCHITECTURE.md); Gameplay never references this class.
    /// </remarks>
    public sealed class ScoreView : MonoBehaviour
    {
        private const int FontSize = 28;
        private static readonly Rect LabelRect = new Rect(16f, 16f, 480f, 140f);

        private ScoreCounterController _source;
        private GUIStyle _style;

        /// <summary>Text currently shown on screen. Starts as <c>Format(ScoreSnapshot.Initial)</c>.</summary>
        public string Text { get; private set; } = Format(ScoreSnapshot.Initial);

        /// <summary>The controller this view reads; null if none is bound or it was destroyed.</summary>
        public ScoreCounterController Source => _source != null ? _source : null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Init()
        {
            var controller = ScoreCounterController.EnsureInstance();
            var view = controller.GetComponent<ScoreView>();
            if (view == null) view = controller.gameObject.AddComponent<ScoreView>();
            view.Bind(controller);
        }

        /// <summary>
        /// Display format, one value per line: "Score: {score}\nCombo: {combo}\nx{multiplier}".
        /// Pure; invariant culture.
        /// </summary>
        public static string Format(ScoreSnapshot snapshot) =>
            string.Format(CultureInfo.InvariantCulture, "Score: {0}\nCombo: {1}\nx{2}",
                snapshot.Score, snapshot.Combo, snapshot.Multiplier);

        /// <summary>Binds the view to <paramref name="controller"/> and shows its current snapshot immediately.</summary>
        public void Bind(ScoreCounterController controller)
        {
            _source = controller;
            Refresh();
        }

        private void Awake()
        {
            if (_source == null) _source = GetComponent<ScoreCounterController>();
            Refresh();
        }

        private void LateUpdate()
        {
            Refresh();
        }

        private void Refresh()
        {
            // Unbound or destroyed source: keep the last text (Initial before any binding).
            if (_source == null) return;
            Text = Format(_source.Current);
        }

        private void OnGUI()
        {
            // GUI.skin is only accessible inside OnGUI, so the style is created lazily here.
            if (_style == null)
            {
                _style = new GUIStyle(GUI.skin.label) { fontSize = FontSize };
                _style.normal.textColor = Color.white;
            }

            GUI.Label(LabelRect, Text, _style);
        }
    }
}
