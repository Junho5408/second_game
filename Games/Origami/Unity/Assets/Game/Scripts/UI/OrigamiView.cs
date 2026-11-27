using System.Collections.Generic;
using System.Collections.ObjectModel;
using Origami.Core.Flow;
using Origami.Core.Run;
using Origami.Runtime.Gameplay;
using UnityEngine;

namespace Origami.Runtime.UI
{
    /// <summary>
    /// F-007 screens drawn with IMGUI (INTERFACE_CONTRACTS.md C-031, ARCHITECTURE.md A-2).
    /// <see cref="Refresh"/> (LateUpdate, after the controller's Update) stores <c>FlowTexts.ScreenLines(Source.Flow)</c>
    /// in <see cref="Lines"/>; <c>OnGUI</c> draws those lines, code-generated shapes (<see cref="ModelIcons"/>) and the
    /// buttons of <see cref="OrigamiButtons.For"/>. A button click only calls its Flow method (the same one as the key).
    /// </summary>
    /// <remarks>
    /// No rule decisions and no rule-dependent string building here: texts come from <c>FlowTexts</c>.
    /// Layout is in a 1280×720 reference space scaled with <c>GUI.matrix</c>; coordinates are not part of the contract.
    /// Dependency direction UI → Gameplay → Core.
    /// </remarks>
    public sealed class OrigamiView : MonoBehaviour
    {
        private const float RefWidth = 1280f;
        private const float RefHeight = 720f;
        private const int LabelFontSize = 22;
        private const int ButtonFontSize = 20;
        private const float Margin = 24f;
        private const float LinesWidth = 760f;
        private const float LineSpacing = 4f;
        private const float RightX = 820f;
        private const float RightWidth = 420f;
        private const float GraphicTop = 24f;
        private const float PaperSize = 160f;
        private const float ProgressHeight = 14f;
        private const float NoticeIconSize = 128f;
        private const float ButtonTop = 230f;
        private const float ButtonHeight = 44f;
        private const float ButtonSpacing = 8f;
        private const float ModelIconSize = 40f;

        private static readonly IReadOnlyList<string> NoLines = new ReadOnlyCollection<string>(new string[0]);
        private static readonly Color LockedTint = new Color(0.35f, 0.35f, 0.35f, 1f);
        private static readonly Color ProgressBack = new Color(0.25f, 0.25f, 0.3f, 1f);
        private static readonly Color ProgressFill = new Color(0.95f, 0.75f, 0.2f, 1f);

        private OrigamiController _source;
        private IReadOnlyList<string> _lines = NoLines;
        private GUIStyle _labelStyle;
        private GUIStyle _buttonStyle;

        /// <summary>The controller this view reads; null (real null) if none is bound or it was destroyed.</summary>
        public OrigamiController Source => _source != null ? _source : null;

        /// <summary>Lines of the latest <see cref="Refresh"/> (<c>FlowTexts.ScreenLines(Source.Flow)</c>); empty before any.</summary>
        public IReadOnlyList<string> Lines => _lines;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Init()
        {
            var controller = OrigamiController.EnsureInstance();
            var view = controller.GetComponent<OrigamiView>();
            if (view == null) view = controller.gameObject.AddComponent<OrigamiView>();
            view.Bind(controller);
        }

        /// <summary>Binds the view to <paramref name="controller"/> (null unbinds) and refreshes immediately.</summary>
        public void Bind(OrigamiController controller)
        {
            _source = controller;
            Refresh();
        }

        /// <summary>Updates <see cref="Lines"/> from the source's flow. Unbound or destroyed source: keeps the last lines.</summary>
        public void Refresh()
        {
            if (_source == null) return;
            _lines = FlowTexts.ScreenLines(_source.Flow);
        }

        private void Awake()
        {
            if (_source == null) _source = GetComponent<OrigamiController>();
            Refresh();
        }

        private void LateUpdate()
        {
            Refresh();
        }

        private void OnGUI()
        {
            var source = Source;
            if (source == null) return;
            EnsureStyles();

            var oldMatrix = GUI.matrix;
            var oldColor = GUI.color;
            float scale = Mathf.Min(Screen.width / RefWidth, Screen.height / RefHeight);
            if (scale <= 0f || float.IsNaN(scale) || float.IsInfinity(scale)) scale = 1f;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1f));

            var flow = source.Flow;
            DrawLines();
            DrawGraphics(flow);
            DrawButtons(flow);

            GUI.color = oldColor;
            GUI.matrix = oldMatrix;
        }

        /// <summary>GUI.skin is only valid inside OnGUI, so styles (and the OS font, A-5) are created lazily here.</summary>
        private void EnsureStyles()
        {
            if (_labelStyle != null && _buttonStyle != null) return;
            var font = KoreanFont.Get(LabelFontSize);

            _labelStyle = new GUIStyle(GUI.skin.label) { fontSize = LabelFontSize, wordWrap = true };
            _labelStyle.normal.textColor = Color.white;
            _buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = ButtonFontSize };
            if (font != null)
            {
                _labelStyle.font = font;
                _buttonStyle.font = font;
            }
        }

        private void DrawLines()
        {
            float y = Margin;
            var lines = _lines;
            for (int i = 0; i < lines.Count; i++)
            {
                var content = new GUIContent(lines[i]);
                float h = Mathf.Max(_labelStyle.CalcHeight(content, LinesWidth), LabelFontSize + 4f);
                GUI.Label(new Rect(Margin, y, LinesWidth, h), content, _labelStyle);
                y += h + LineSpacing;
            }
        }

        private static void DrawGraphics(OrigamiFlow flow)
        {
            switch (flow.Screen)
            {
                case FlowScreen.Folding:
                case FlowScreen.AbandonConfirm:
                    var fold = flow.Snapshot.Fold;
                    if (fold == null) return;
                    GUI.color = Color.white;
                    GUI.DrawTexture(new Rect(RightX, GraphicTop, PaperSize, PaperSize), ModelIcons.Paper);
                    var bar = new Rect(RightX, GraphicTop + PaperSize + 8f, PaperSize, ProgressHeight);
                    GUI.color = ProgressBack;
                    GUI.DrawTexture(bar, Texture2D.whiteTexture);
                    float fraction = fold.StepCount > 0 ? Mathf.Clamp01((float)fold.Progress / fold.StepCount) : 0f;
                    if (fraction > 0f)
                    {
                        GUI.color = ProgressFill;
                        GUI.DrawTexture(new Rect(bar.x, bar.y, bar.width * fraction, bar.height), Texture2D.whiteTexture);
                    }
                    GUI.color = Color.white;
                    break;
                case FlowScreen.Notice:
                    var result = flow.LastResult;
                    if (result.Outcome != CommandOutcome.FoldCompleted) return;
                    var icon = ModelIcons.Get(result.Piece.ModelId);
                    if (icon == null) return;
                    GUI.color = Color.white;
                    GUI.DrawTexture(new Rect(RightX, GraphicTop, NoticeIconSize, NoticeIconSize), icon);
                    break;
            }
        }

        private void DrawButtons(OrigamiFlow flow)
        {
            var buttons = OrigamiButtons.For(flow);
            float y = ButtonTop;
            for (int i = 0; i < buttons.Count; i++)
            {
                var button = buttons[i];
                float x = RightX;
                float width = RightWidth;
                if (button.ModelId != null)
                {
                    var icon = ModelIcons.Get(button.ModelId);
                    if (icon != null)
                    {
                        GUI.color = flow.Snapshot.IsUnlocked(button.ModelId) ? Color.white : LockedTint;
                        GUI.DrawTexture(new Rect(x, y + (ButtonHeight - ModelIconSize) / 2f, ModelIconSize, ModelIconSize), icon);
                        GUI.color = Color.white;
                    }
                    x += ModelIconSize + ButtonSpacing;
                    width -= ModelIconSize + ButtonSpacing;
                }

                if (GUI.Button(new Rect(x, y, width, ButtonHeight), button.Label, _buttonStyle))
                {
                    // Same Flow method as the key (F-007 AC-05). The list is stale after a click, so stop here.
                    button.Invoke();
                    Refresh();
                    return;
                }
                y += ButtonHeight + ButtonSpacing;
            }
        }
    }
}
