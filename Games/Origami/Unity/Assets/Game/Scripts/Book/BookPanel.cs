using System;
using System.Collections.Generic;
using Origami.Core.Book;
using Origami.Core.Catalog;
using Origami.Runtime.UI;
using UnityEngine;

namespace Origami.Runtime.Book
{
    /// <summary>
    /// IMGUI book view (C-192, F-007, F-008, CONTENT §3): always a two-page spread (even, odd), the contents page with
    /// one button per model, the symbols page, covers with the "사기" button, step diagrams and finished shapes.
    /// <para>
    /// The panel has no <c>Update</c> and no <c>OnGUI</c> (CODING_CONVENTION: the only OnGUI is <c>OrigamiGui</c>, 014):
    /// the host GUI calls <see cref="Draw"/> from its OnGUI. <see cref="Prepare"/> builds pages, strings and textures and
    /// may be called outside OnGUI; <see cref="Draw"/> calls it itself when the spread, the owned pages or the money
    /// changed. Every button only calls a <c>Click…</c> method, which only calls the <see cref="IBookHost"/>; the panel
    /// never changes game state. Textures are cached per (page, owned state of the page's model) — the only part of the
    /// owned list a page picture depends on — and destroyed in <c>OnDestroy</c>.
    /// </para>
    /// </summary>
    public sealed class BookPanel : MonoBehaviour
    {
        /// <summary>Diagram/silhouette texture size in pixels (not part of the contract).</summary>
        internal const int TextureSize = 256;

        private const float Pad = 12f, FooterHeight = 46f, TitleHeight = 34f, ButtonHeight = 38f, Gap = 6f;
        private const int TitleFontSize = 20, TextFontSize = 15, ButtonFontSize = 16, SmallFontSize = 13;

        private static readonly Color CoverColor = new Color(0.42f, 0.27f, 0.18f, 0.96f);
        private static readonly Color PaperColor = new Color(0.98f, 0.96f, 0.90f, 1f);
        private static readonly Color InkColor = new Color(0.16f, 0.14f, 0.12f, 1f);
        private static readonly Color SealedInkColor = new Color(0.45f, 0.40f, 0.36f, 1f);
        private static readonly string[] NoIds = new string[0];

        private readonly Dictionary<int, Texture2D> _textures = new Dictionary<int, Texture2D>();
        private IBookHost _host;
        private bool _prepared;
        private int _preparedPage, _preparedMask, _preparedMoney;

        private GUIStyle _titleStyle, _textStyle, _buttonStyle, _smallStyle, _sealedStyle;

        /// <summary>Left page of the last <see cref="Prepare"/> (null before the first Prepare or without a host).</summary>
        public BookPage LeftPage { get; private set; }

        /// <summary>Right page of the last <see cref="Prepare"/> (with 44 pages every spread has one, QA-3).</summary>
        public BookPage RightPage { get; private set; }

        internal BookPageView LeftView { get; private set; }
        internal BookPageView RightView { get; private set; }

        /// <summary>The ← / → buttons of the spread (always both; drawn disabled at the first/last spread).</summary>
        internal IReadOnlyList<BookButton> SpreadButtons { get; } = new[]
        {
            new BookButton(BookButtonKind.TurnBack, BookTexts.PrevButton, null),
            new BookButton(BookButtonKind.TurnForward, BookTexts.NextButton, null),
        };

        internal bool CanTurnBack => LeftPage != null && LeftPage.Index > 0;
        internal bool CanTurnForward => LeftPage != null && LeftPage.Index + 2 < BookLayout.PageCount;

        /// <summary>Number of cached textures (tests).</summary>
        internal int CachedTextureCount => _textures.Count;

        /// <summary>Connects the game (null = disconnect). Clears the prepared spread; textures stay cached.</summary>
        public void Bind(IBookHost host)
        {
            _host = host;
            _prepared = false;
            LeftPage = null;
            RightPage = null;
            LeftView = null;
            RightView = null;
        }

        /// <summary>
        /// Builds the current spread from the host (left = host page clamped to an even page, right = left + 1), its page
        /// views and its textures. Does nothing when nothing changed since the last call. Without a host the pages are
        /// cleared. Safe outside OnGUI.
        /// </summary>
        public void Prepare()
        {
            if (_host == null)
            {
                Bind(null);
                return;
            }
            int page = BookLayout.ClampToSpread(_host.CurrentPage);
            int mask = OwnedMask(_host.OwnedModelIds);
            int money = _host.Money;
            if (_prepared && page == _preparedPage && mask == _preparedMask && money == _preparedMoney) return;

            var owned = OwnedList();
            LeftPage = BookPages.Get(page, owned);
            RightPage = page + 1 < BookLayout.PageCount ? BookPages.Get(page + 1, owned) : null;
            LeftView = BookPageView.Build(LeftPage, money);
            RightView = RightPage != null ? BookPageView.Build(RightPage, money) : null;
            _prepared = true;
            _preparedPage = page;
            _preparedMask = mask;
            _preparedMoney = money;
            TextureFor(LeftPage);
            if (RightPage != null) TextureFor(RightPage);
        }

        /// <summary>
        /// Picture of a page for the host's current owned pages: step diagram, cover silhouette (grey when not owned),
        /// finished shape, or the symbol table of page 1. Null for the contents page, sealed pages, pages outside
        /// 0..43, and without a host. Cached; the panel owns (and destroys) the texture.
        /// </summary>
        public Texture2D DiagramTexture(int page)
        {
            if (_host == null || page < 0 || page >= BookLayout.PageCount) return null;
            BookPage p;
            if (_prepared && LeftPage != null && LeftPage.Index == page && _preparedMask == OwnedMask(_host.OwnedModelIds)) p = LeftPage;
            else if (_prepared && RightPage != null && RightPage.Index == page && _preparedMask == OwnedMask(_host.OwnedModelIds)) p = RightPage;
            else p = BookPages.Get(page, OwnedList());
            return TextureFor(p);
        }

        /// <summary>Contents entry button: <c>host.TurnPage(CoverPage)</c>. Unknown/null IDs and no host: nothing.</summary>
        public void ClickContents(string modelId)
        {
            if (_host == null || ModelCatalog.IndexOf(modelId) < 0) return;
            _host.TurnPage(BookLayout.CoverPage(modelId));
        }

        /// <summary>"사기" button: <c>host.BuyPage(modelId)</c> (Core accepts or rejects, F-008). No host: nothing.</summary>
        public void ClickBuy(string modelId)
        {
            if (_host == null) return;
            _host.BuyPage(modelId);
        }

        /// <summary>← / → buttons (delta ∓2): <c>host.TurnPage(CurrentPage + delta)</c>, saturated to int. No host: nothing.</summary>
        public void ClickTurn(int delta)
        {
            if (_host == null) return;
            long target = (long)_host.CurrentPage + delta;
            _host.TurnPage((int)Math.Max(int.MinValue, Math.Min(int.MaxValue, target)));
        }

        /// <summary>What a drawn button does when clicked (Draw calls this; tests call it with the view's buttons).</summary>
        internal void Press(BookButton button)
        {
            if (button == null) return;
            switch (button.Kind)
            {
                case BookButtonKind.Contents:
                    ClickContents(button.ModelId);
                    break;
                case BookButtonKind.Buy:
                    ClickBuy(button.ModelId);
                    break;
                case BookButtonKind.TurnBack:
                    ClickTurn(-2);
                    break;
                case BookButtonKind.TurnForward:
                    ClickTurn(2);
                    break;
            }
        }

        /// <summary>
        /// Draws the spread into <paramref name="area"/> (GUI coordinates of the caller's current GUI.matrix). Only inside
        /// OnGUI. Without a host it draws nothing.
        /// </summary>
        public void Draw(Rect area)
        {
            if (_host == null) return;
            Prepare();
            if (LeftView == null) return;
            EnsureStyles();

            var oldColor = GUI.color;
            GUI.color = CoverColor;
            GUI.DrawTexture(area, Texture2D.whiteTexture);
            GUI.color = oldColor;

            float pageW = Mathf.Max(10f, (area.width - Pad * 3f) * 0.5f);
            float pageH = Mathf.Max(10f, area.height - Pad * 2f - FooterHeight);
            var leftRect = new Rect(area.x + Pad, area.y + Pad, pageW, pageH);
            var rightRect = new Rect(leftRect.xMax + Pad, area.y + Pad, pageW, pageH);
            DrawPage(LeftView, leftRect);
            if (RightView != null) DrawPage(RightView, rightRect);

            float fy = area.yMax - FooterHeight + (FooterHeight - ButtonHeight) * 0.5f;
            float bw = Mathf.Min(140f, pageW * 0.45f);
            var wasEnabled = GUI.enabled;
            GUI.enabled = wasEnabled && CanTurnBack;
            if (GUI.Button(new Rect(area.x + Pad, fy, bw, ButtonHeight), SpreadButtons[0].Label, _buttonStyle)) Press(SpreadButtons[0]);
            GUI.enabled = wasEnabled && CanTurnForward;
            if (GUI.Button(new Rect(area.xMax - Pad - bw, fy, bw, ButtonHeight), SpreadButtons[1].Label, _buttonStyle)) Press(SpreadButtons[1]);
            GUI.enabled = wasEnabled;
        }

        private void DrawPage(BookPageView view, Rect r)
        {
            var oldColor = GUI.color;
            GUI.color = PaperColor;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = oldColor;

            float x = r.x + 10f, w = Mathf.Max(10f, r.width - 20f);
            float y = r.y + 8f;
            float bottom = r.yMax - 22f;
            GUI.Label(new Rect(r.x, r.yMax - 22f, r.width, 20f), BookTexts.PageNumber(view.Page.Index), _smallStyle);

            if (view.Page.Sealed)
            {
                GUI.Label(new Rect(x, r.y, w, r.height - 22f), view.Lines.Count > 0 ? view.Lines[0] : BookTexts.Sealed, _sealedStyle);
                return;
            }

            if (view.Title.Length > 0)
            {
                GUI.Label(new Rect(x, y, w, TitleHeight), view.Title, _titleStyle);
                y += TitleHeight + Gap;
            }

            var tex = view.HasPicture ? DiagramTexture(view.Page.Index) : null;
            if (view.Page.Kind == BookPageKind.Symbols)
            {
                float side = Mathf.Max(10f, Mathf.Min(w * 0.38f, bottom - y));
                if (tex != null) GUI.DrawTexture(new Rect(x, y, side, side), tex, ScaleMode.ScaleToFit, true);
                float rowH = side / DiagramRaster.SymbolRows;
                for (int i = 0; i < view.Lines.Count && i < DiagramRaster.SymbolRows; i++)
                    GUI.Label(new Rect(x + side + Gap, y + i * rowH, w - side - Gap, rowH), view.Lines[i], _textStyle);
                return;
            }

            if (tex != null)
            {
                float side = Mathf.Max(10f, Mathf.Min(w, (bottom - y) * 0.55f));
                GUI.DrawTexture(new Rect(x + (w - side) * 0.5f, y, side, side), tex, ScaleMode.ScaleToFit, true);
                y += side + Gap;
            }

            foreach (var line in view.Lines)
            {
                float h = _textStyle.CalcHeight(new GUIContent(line), w);
                GUI.Label(new Rect(x, y, w, h), line, _textStyle);
                y += h + 2f;
            }

            foreach (var b in view.Buttons)
            {
                y += Gap;
                if (GUI.Button(new Rect(x, y, w, ButtonHeight), b.Label, _buttonStyle)) Press(b);
                y += ButtonHeight;
            }
        }

        /// <summary>GUI.skin is only valid inside OnGUI, so styles (and the OS font, A-5) are created lazily here.</summary>
        private void EnsureStyles()
        {
            if (_titleStyle != null) return;
            var font = KoreanFont.Get(TextFontSize);
            _titleStyle = new GUIStyle(GUI.skin.label) { fontSize = TitleFontSize, fontStyle = FontStyle.Bold, wordWrap = true };
            _textStyle = new GUIStyle(GUI.skin.label) { fontSize = TextFontSize, wordWrap = true };
            _smallStyle = new GUIStyle(GUI.skin.label) { fontSize = SmallFontSize, alignment = TextAnchor.MiddleCenter };
            _sealedStyle = new GUIStyle(GUI.skin.label) { fontSize = TitleFontSize, wordWrap = true, alignment = TextAnchor.MiddleCenter };
            _buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = ButtonFontSize, wordWrap = true };
            foreach (var s in new[] { _titleStyle, _textStyle, _smallStyle, _sealedStyle })
            {
                s.normal.textColor = InkColor;
                if (font != null) s.font = font;
            }
            _sealedStyle.normal.textColor = SealedInkColor;
            if (font != null) _buttonStyle.font = font;
        }

        private Texture2D TextureFor(BookPage p)
        {
            if (p == null) return null;
            bool owned = p.Kind == BookPageKind.Cover ? p.Cover.Owned : !p.Sealed;
            int key = p.Index * 2 + (owned ? 1 : 0);
            if (_textures.TryGetValue(key, out var cached) && cached != null) return cached;

            Texture2D tex = null;
            switch (p.Kind)
            {
                case BookPageKind.Symbols:
                    tex = DiagramRaster.RenderSymbols(TextureSize);
                    break;
                case BookPageKind.Cover:
                    tex = DiagramRaster.RenderShape(p.Cover.Silhouette, p.Cover.RotationDeg, !p.Cover.Owned, TextureSize);
                    break;
                case BookPageKind.Step:
                    if (p.Diagram != null) tex = DiagramRaster.Render(p.Diagram, TextureSize);
                    break;
                case BookPageKind.Complete:
                    if (p.CompleteShape != null)
                    {
                        var model = ModelCatalog.Find(p.ModelId);
                        tex = DiagramRaster.RenderShape(p.CompleteShape, model != null ? model.CoverRotationDeg : 0.0, false, TextureSize);
                    }
                    break;
            }
            if (tex == null) return null;
            _textures[key] = tex;
            return tex;
        }

        private IReadOnlyList<string> OwnedList()
        {
            var ids = _host != null ? _host.OwnedModelIds : null;
            return ids ?? (IReadOnlyList<string>)NoIds;
        }

        private static int OwnedMask(IReadOnlyList<string> ids)
        {
            if (ids == null) return 0;
            int mask = 0;
            for (int i = 0; i < ids.Count; i++)
            {
                int m = ModelCatalog.IndexOf(ids[i]);
                if (m >= 0 && m < 31) mask |= 1 << m;
            }
            return mask;
        }

        private void OnDestroy()
        {
            foreach (var tex in _textures.Values)
            {
                // Immediate: the textures are runtime-only objects owned by this panel, and nothing draws them after this.
                if (tex != null) DestroyImmediate(tex);
            }
            _textures.Clear();
        }
    }
}
