using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Origami.Core.Folding;
using Origami.Core.Game;
using Origami.Core.Screens;
using Origami.Core.Texts;
using Origami.Runtime.Gameplay;
using UnityEngine;

namespace Origami.Runtime.UI
{
    /// <summary>
    /// The only <c>OnGUI</c> of the game (C-194, IMGUI A-2): HUD, seated buttons, the book (<c>BookPanel.Draw</c>), the cursor
    /// layer label, the 2-second toast and the overlays (counter, sleep confirm, settlement, result, pause, new-run confirm).
    /// <para>
    /// <see cref="Refresh"/> (called at the end of <c>OrigamiApp.ProcessFrame</c>) builds every string and the button list;
    /// <c>OnGUI</c> only draws them. Buttons call the same <see cref="ScreenFlow"/> methods as the keys (C-171). Texts come from
    /// Core <c>GameTexts</c>; fixed dialog texts from <see cref="WorkshopTexts"/>. Korean font: <c>KoreanFont.Get</c>.
    /// Layout in a 1280×720 reference space scaled with <c>GUI.matrix</c> (positions are not part of the contract).
    /// </para>
    /// Toast: a new non-empty <c>Flow.Message</c> (rejections, recognition results) or a released drag whose fold was invalid
    /// (<c>Table.LastError</c>) shows for 2 s; a new run (<c>SheetChangeKind.Reset</c>) clears it (review ORIGAMI-011 N-2).
    /// </summary>
    public sealed class OrigamiGui : MonoBehaviour, IAppView
    {
        internal const float ToastSeconds = 2f;
        internal const float RefWidth = 1280f, RefHeight = 720f;
        internal const float SeatedBookWidth = RefWidth * 0.45f;

        private const int FontSize = 20, SmallFontSize = 18, TitleFontSize = 26;
        private const float Margin = 16f, LineHeight = 26f, ButtonHeight = 38f, ButtonGap = 6f;
        private const float HudWidth = 330f, SideButtonWidth = 200f;
        private const float PanelWidth = 640f, PanelMinHeight = 220f;
        private const float ToastWidth = 680f, ToastHeight = 44f, ToastY = 560f;

        private static readonly IReadOnlyList<string> NoLines = new ReadOnlyCollection<string>(new string[0]);
        private static readonly Color Backdrop = new Color(0f, 0f, 0f, 0.55f);
        private static readonly Color BoxColor = new Color(0.08f, 0.09f, 0.12f, 0.78f);
        private static readonly Color ToastColor = new Color(0.12f, 0.1f, 0.06f, 0.88f);

        /// <summary>Where the screen currently shows the book.</summary>
        internal enum BookPlacement { None, Left, Full }

        /// <summary>A button of the current screen; <see cref="Click"/> calls the ScreenFlow method (same as the key).</summary>
        internal sealed class GuiButton
        {
            public GuiButton(string label, Action click)
            {
                Label = label;
                Click = click;
            }

            public string Label { get; }
            public Action Click { get; }
        }

        private OrigamiApp _app;
        private IReadOnlyList<string> _visible = NoLines;
        private readonly List<string> _hudLines = new List<string>();
        private readonly List<string> _hintLines = new List<string>();
        private readonly List<string> _panelLines = new List<string>();
        private readonly List<GuiButton> _sideButtons = new List<GuiButton>();
        private readonly List<GuiButton> _panelButtons = new List<GuiButton>();
        private readonly List<Rect> _hits = new List<Rect>();
        private string _panelTitle = "";
        private string _toast = "";
        private float _toastLeft;
        private int _seenMessage;
        private int _seenChange;
        private bool _wasDragging;
        private BookPlacement _book;
        private float _scale = 1f;

        private GUIStyle _label, _small, _title, _button, _toastStyle;

        /// <summary>Screen texts of the last <see cref="Refresh"/> (HUD, prompt, hints, toast, overlay) — for tests.</summary>
        public IReadOnlyList<string> VisibleLines => _visible;

        /// <summary>Current toast text, "" when none.</summary>
        internal string Toast => _toast;

        /// <summary>Title of the open overlay panel, "" when none.</summary>
        internal string PanelTitle => _panelTitle;

        internal IReadOnlyList<GuiButton> SideButtons => _sideButtons;
        internal IReadOnlyList<GuiButton> PanelButtons => _panelButtons;
        internal BookPlacement Book => _book;

        /// <summary>Number of OnGUI passes that drew a frame (tests: OnGUI really ran).</summary>
        internal int DrawCount { get; private set; }

        /// <summary>The book was drawn with BookPanel.Draw in the last OnGUI pass.</summary>
        internal bool BookDrawn { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Init()
        {
            var app = OrigamiApp.EnsureInstance();
            var gui = app.GetComponent<OrigamiGui>();
            if (gui == null) gui = app.gameObject.AddComponent<OrigamiGui>();
            gui.Bind(app);
        }

        /// <summary>Connects to <paramref name="app"/> (null = disconnect) and registers as its screen layer. Old messages are not shown.</summary>
        public void Bind(OrigamiApp app)
        {
            if (_app != null && ReferenceEquals(_app.View, this)) _app.View = null;
            _app = app;
            _toast = "";
            _toastLeft = 0f;
            _wasDragging = false;
            if (app == null)
            {
                _visible = NoLines;
                return;
            }
            app.View = this;
            _seenMessage = app.Flow.Message.Serial;
            _seenChange = app.Flow.LastSheetChange.Serial;
            Refresh(0f);
        }

        /// <summary>Rebuilds texts and buttons and advances the toast timer by dt (NaN/negative → 0).</summary>
        public void Refresh(float dt)
        {
            if (_app == null) return;
            if (float.IsNaN(dt) || dt < 0f) dt = 0f;
            var flow = _app.Flow;
            var s = flow.Snapshot;
            UpdateToast(flow, dt);

            var overlay = flow.Overlay;
            var mode = s.Mode;
            var lines = new List<string>();

            _hudLines.Clear();
            if (mode == WorkshopMode.Walking || mode == WorkshopMode.Seated) _hudLines.AddRange(GameTexts.Hud(s).Lines);
            lines.AddRange(_hudLines);

            _hintLines.Clear();
            if (overlay == Overlay.None)
            {
                string prompt = GameTexts.InteractPrompt(_app.InteractCandidate);
                if (prompt.Length > 0) _hintLines.Add(prompt);
                if (flow.WalkHintVisible) _hintLines.Add(GameTexts.WalkHint);
                if (flow.TakeSheetHintVisible) _hintLines.Add(GameTexts.TakeSheetHint);
                if (flow.PostTutorialHintVisible) _hintLines.Add(GameTexts.PostTutorialHint);
                var sheet = s.Sheet;
                if (mode == WorkshopMode.Seated && sheet != null && sheet.GuideVisible && sheet.Guide != null)
                {
                    _hintLines.Add(GameTexts.GuideTitle(sheet.Guide));
                    if (!string.IsNullOrEmpty(sheet.Guide.Text)) _hintLines.Add(sheet.Guide.Text);
                }
            }
            lines.AddRange(_hintLines);
            if (_toast.Length > 0) lines.Add(_toast);

            BuildPanel(flow, s);
            if (_panelTitle.Length > 0) lines.Add(_panelTitle);
            lines.AddRange(_panelLines);

            BuildSideButtons(flow, s);
            _book = overlay != Overlay.None ? BookPlacement.None
                : mode == WorkshopMode.Bookshelf ? BookPlacement.Full
                : mode == WorkshopMode.Seated && s.BookOpen ? BookPlacement.Left
                : BookPlacement.None;

            _visible = new ReadOnlyCollection<string>(lines.ToArray());
        }

        /// <summary>
        /// The screen position (pixels, y up like <c>Input.mousePosition</c>) is over a button, the book, the HUD or an overlay
        /// of the last repaint.
        /// </summary>
        public bool PointerOverGui(Vector2 screenPosition)
        {
            if (_hits.Count == 0) return false;
            float scale = _scale > 0f ? _scale : 1f;
            var p = new Vector2(screenPosition.x / scale, (Screen.height - screenPosition.y) / scale);
            foreach (var r in _hits)
                if (r.Contains(p)) return true;
            return false;
        }

        /// <summary>Clicks the visible button with this label (side or panel buttons; tests — same action as the real click).</summary>
        internal bool Press(string label)
        {
            foreach (var b in _panelButtons)
                if (b.Label == label) return Click(b);
            foreach (var b in _sideButtons)
                if (b.Label == label) return Click(b);
            return false;
        }

        private bool Click(GuiButton b)
        {
            b.Click();
            Refresh(0f);
            return true;
        }

        private void OnDestroy()
        {
            if (_app != null && ReferenceEquals(_app.View, this)) _app.View = null;
        }

        // ------------------------------------------------------------------ model

        private void UpdateToast(ScreenFlow flow, float dt)
        {
            if (_toastLeft > 0f)
            {
                _toastLeft -= dt;
                if (_toastLeft <= 0f)
                {
                    _toastLeft = 0f;
                    _toast = "";
                }
            }

            var msg = flow.Message;
            bool newMessage = msg.Serial != _seenMessage;
            if (newMessage)
            {
                _seenMessage = msg.Serial;
                if (msg.Text.Length > 0) ShowToast(msg.Text);
            }

            var table = _app.Table;
            bool dragging = table != null && table.IsDragging;
            if (_wasDragging && !dragging && !newMessage && table.LastError != FoldError.None)
            {
                string text = FoldTexts.ErrorMessage(table.LastError);
                if (!string.IsNullOrEmpty(text)) ShowToast(text);
            }
            _wasDragging = dragging;

            var change = flow.LastSheetChange;
            if (change.Serial != _seenChange)
            {
                _seenChange = change.Serial;
                if (change.Kind == SheetChangeKind.Reset)
                {
                    _toast = "";
                    _toastLeft = 0f;
                }
            }
        }

        private void ShowToast(string text)
        {
            _toast = text;
            _toastLeft = ToastSeconds;
        }

        private void BuildPanel(ScreenFlow flow, GameSnapshot s)
        {
            _panelTitle = "";
            _panelLines.Clear();
            _panelButtons.Clear();
            switch (flow.Overlay)
            {
                case Overlay.Settlement:
                    if (flow.PendingSettlement != null) _panelLines.AddRange(GameTexts.SettlementLines(flow.PendingSettlement));
                    _panelButtons.Add(new GuiButton(s.IsEnded ? WorkshopTexts.ShowResult : WorkshopTexts.NextDay, flow.AcknowledgeSettlement));
                    return;
                case Overlay.Result:
                    AddResult(flow, s);
                    return;
                case Overlay.PauseMenu:
                    _panelTitle = WorkshopTexts.PauseTitle;
                    _panelLines.Add(WorkshopTexts.PauseKeys);
                    _panelButtons.Add(new GuiButton(WorkshopTexts.Continue, flow.ClosePause));
                    _panelButtons.Add(new GuiButton(WorkshopTexts.NewRun, flow.RequestNewRun));
                    return;
                case Overlay.ConfirmNewRun:
                    _panelTitle = WorkshopTexts.ConfirmNewRunQuestion;
                    _panelLines.Add(WorkshopTexts.YesNoKeys);
                    _panelButtons.Add(new GuiButton(WorkshopTexts.Yes, flow.ConfirmNewRun));
                    _panelButtons.Add(new GuiButton(WorkshopTexts.No, flow.ClosePause));
                    return;
            }
            switch (s.Mode)
            {
                case WorkshopMode.Counter:
                    _panelLines.AddRange(GameTexts.CounterLines(s));
                    _panelButtons.Add(new GuiButton(WorkshopTexts.Close, () => flow.CloseCounter()));
                    return;
                case WorkshopMode.SleepConfirm:
                    _panelTitle = WorkshopTexts.SleepTitle;
                    _panelLines.Add(WorkshopTexts.YesNoKeys);
                    _panelButtons.Add(new GuiButton(WorkshopTexts.Yes, () => flow.Sleep(true)));
                    _panelButtons.Add(new GuiButton(WorkshopTexts.No, () => flow.Sleep(false)));
                    return;
                case WorkshopMode.Ended:
                    AddResult(flow, s);
                    return;
            }
        }

        private void AddResult(ScreenFlow flow, GameSnapshot s)
        {
            _panelLines.AddRange(GameTexts.ResultLines(s));
            _panelButtons.Add(new GuiButton(WorkshopTexts.NewRun, flow.ConfirmNewRun));
        }

        private void BuildSideButtons(ScreenFlow flow, GameSnapshot s)
        {
            _sideButtons.Clear();
            if (flow.Overlay != Overlay.None) return;
            if (s.Mode == WorkshopMode.Bookshelf)
            {
                _sideButtons.Add(new GuiButton(WorkshopTexts.Close, () => flow.CloseBook()));
                return;
            }
            if (s.Mode != WorkshopMode.Seated) return;
            var sheet = s.Sheet;
            if (sheet == null)
            {
                _sideButtons.Add(new GuiButton(WorkshopTexts.TakeSheet, () => flow.TakeSheet()));
            }
            else
            {
                _sideButtons.Add(new GuiButton(WorkshopTexts.Finish, () => flow.Finish()));
                _sideButtons.Add(new GuiButton(WorkshopTexts.Discard, () => flow.Discard()));
                _sideButtons.Add(new GuiButton(WorkshopTexts.Undo, () => flow.Undo()));
                _sideButtons.Add(new GuiButton(WorkshopTexts.Unfold, () => flow.Unfold()));
                _sideButtons.Add(new GuiButton(WorkshopTexts.Flip, () => flow.Flip()));
            }
            _sideButtons.Add(new GuiButton(WorkshopTexts.Book, () => flow.ToggleBook()));
            _sideButtons.Add(new GuiButton(WorkshopTexts.StandUp, () => flow.StandUp()));
            if (sheet != null && sheet.GuideVisible) _sideButtons.Add(new GuiButton(WorkshopTexts.SkipGuide, () => flow.SkipGuide()));
        }

        // ------------------------------------------------------------------ drawing

        private void OnGUI()
        {
            if (_app == null) return;
            EnsureStyles();
            var e = Event.current;
            bool repaint = e != null && e.type == EventType.Repaint;
            if (repaint)
            {
                _hits.Clear();
                BookDrawn = false;
            }

            var oldMatrix = GUI.matrix;
            var oldColor = GUI.color;
            float scale = Mathf.Min(Screen.width / RefWidth, Screen.height / RefHeight);
            if (scale <= 0f || float.IsNaN(scale) || float.IsInfinity(scale)) scale = 1f;
            if (repaint) _scale = scale;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1f));
            try
            {
                if (DrawAll(repaint)) Refresh(0f);
            }
            finally
            {
                GUI.color = oldColor;
                GUI.matrix = oldMatrix;
            }
            if (repaint) DrawCount++;
        }

        /// <summary>Draws in the C-194 order. Returns true after a button click (the lists are stale → refresh, stop drawing).</summary>
        private bool DrawAll(bool repaint)
        {
            var s = _app.Flow.Snapshot;
            bool bookLeft = _book == BookPlacement.Left;

            // 1. HUD + hints.
            float hudX = s.Mode == WorkshopMode.Seated ? RefWidth - Margin - HudWidth : Margin;
            if (_hudLines.Count > 0) DrawBoxLines(new Rect(hudX, Margin, HudWidth, 0f), _hudLines, _label, repaint);
            if (_hintLines.Count > 0)
            {
                float x = s.Mode == WorkshopMode.Seated ? (bookLeft ? SeatedBookWidth + Margin : Margin) : (RefWidth - 640f) * 0.5f;
                float w = s.Mode == WorkshopMode.Seated ? RefWidth - Margin * 3f - HudWidth - x : 640f;
                float y = s.Mode == WorkshopMode.Seated ? Margin : RefHeight - Margin - (LineHeight + 4f) * _hintLines.Count - 16f;
                DrawBoxLines(new Rect(x, y, Mathf.Max(200f, w), 0f), _hintLines, _label, false);
            }

            // 2. Side buttons (seated column under the HUD; bookshelf: close at the top right over the book).
            if (_sideButtons.Count > 0)
            {
                float x = RefWidth - Margin - SideButtonWidth;
                float y = s.Mode == WorkshopMode.Seated ? Margin + (LineHeight + 2f) * _hudLines.Count + 30f : Margin;
                for (int i = 0; i < _sideButtons.Count; i++)
                {
                    var r = new Rect(x, y + i * (ButtonHeight + ButtonGap), SideButtonWidth, ButtonHeight);
                    if (repaint) _hits.Add(r);
                    if (GUI.Button(r, _sideButtons[i].Label, _button))
                    {
                        _sideButtons[i].Click();
                        return true;
                    }
                }
            }

            // 3. Book.
            if (_book != BookPlacement.None && _app.Book != null)
            {
                var area = _book == BookPlacement.Full
                    ? new Rect(Margin, Margin + ButtonHeight + ButtonGap, RefWidth - Margin * 2f, RefHeight - Margin * 2f - ButtonHeight - ButtonGap)
                    : new Rect(0f, 0f, SeatedBookWidth, RefHeight);
                if (repaint)
                {
                    _hits.Add(area);
                    BookDrawn = true;
                }
                _app.Book.Draw(area);
            }

            // 4. Cursor label while dragging.
            var table = _app.Table;
            string cursor = table != null ? table.CursorLabel : "";
            if (!string.IsNullOrEmpty(cursor) && Event.current != null)
            {
                var m = Event.current.mousePosition;
                var r = new Rect(m.x + 18f, m.y + 12f, 220f, LineHeight + 4f);
                DrawFill(r, BoxColor);
                GUI.Label(r, cursor, _small);
            }

            // 5. Toast.
            if (_toast.Length > 0)
            {
                var r = new Rect((RefWidth - ToastWidth) * 0.5f, ToastY, ToastWidth, ToastHeight);
                DrawFill(r, ToastColor);
                GUI.Label(r, _toast, _toastStyle);
            }

            // 6. Overlay panel.
            if (_panelButtons.Count > 0 || _panelLines.Count > 0)
            {
                var full = new Rect(0f, 0f, RefWidth, RefHeight);
                if (repaint) _hits.Add(full);
                DrawFill(full, Backdrop);
                float titleH = _panelTitle.Length > 0 ? 40f : 0f;
                float h = Mathf.Max(PanelMinHeight, 32f + titleH + _panelLines.Count * LineHeight + 16f + ButtonHeight + 24f);
                h = Mathf.Min(h, RefHeight - Margin * 2f);
                var panel = new Rect((RefWidth - PanelWidth) * 0.5f, (RefHeight - h) * 0.5f, PanelWidth, h);
                DrawFill(panel, BoxColor);
                float y = panel.y + 16f;
                if (titleH > 0f)
                {
                    GUI.Label(new Rect(panel.x + 24f, y, panel.width - 48f, titleH), _panelTitle, _title);
                    y += titleH;
                }
                foreach (var line in _panelLines)
                {
                    GUI.Label(new Rect(panel.x + 24f, y, panel.width - 48f, LineHeight), line, _label);
                    y += LineHeight;
                }
                float bw = Mathf.Min(220f, (panel.width - 48f - ButtonGap * (_panelButtons.Count - 1)) / Mathf.Max(1, _panelButtons.Count));
                float bx = panel.x + (panel.width - (bw * _panelButtons.Count + ButtonGap * (_panelButtons.Count - 1))) * 0.5f;
                float by = panel.yMax - ButtonHeight - 16f;
                for (int i = 0; i < _panelButtons.Count; i++)
                {
                    if (GUI.Button(new Rect(bx + i * (bw + ButtonGap), by, bw, ButtonHeight), _panelButtons[i].Label, _button))
                    {
                        _panelButtons[i].Click();
                        return true;
                    }
                }
            }
            return false;
        }

        private void DrawBoxLines(Rect r, List<string> lines, GUIStyle style, bool hit)
        {
            r.height = 12f + lines.Count * (LineHeight + 2f);
            DrawFill(r, BoxColor);
            if (hit) _hits.Add(r);
            float y = r.y + 6f;
            foreach (var line in lines)
            {
                GUI.Label(new Rect(r.x + 10f, y, r.width - 20f, LineHeight), line, style);
                y += LineHeight + 2f;
            }
        }

        private static void DrawFill(Rect r, Color c)
        {
            var old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = old;
        }

        /// <summary>GUI.skin is only valid inside OnGUI, so the styles and the OS font (A-5) are created lazily here.</summary>
        private void EnsureStyles()
        {
            if (_label != null) return;
            var font = KoreanFont.Get(FontSize);
            _label = new GUIStyle(GUI.skin.label) { fontSize = FontSize, wordWrap = false, clipping = TextClipping.Clip };
            _label.normal.textColor = Color.white;
            _small = new GUIStyle(_label) { fontSize = SmallFontSize, alignment = TextAnchor.MiddleCenter };
            _title = new GUIStyle(_label) { fontSize = TitleFontSize, fontStyle = FontStyle.Bold };
            _toastStyle = new GUIStyle(_label) { alignment = TextAnchor.MiddleCenter };
            _toastStyle.normal.textColor = new Color(1f, 0.92f, 0.7f);
            _button = new GUIStyle(GUI.skin.button) { fontSize = SmallFontSize };
            if (font != null)
            {
                _label.font = font;
                _small.font = font;
                _title.font = font;
                _toastStyle.font = font;
                _button.font = font;
            }
        }
    }
}
