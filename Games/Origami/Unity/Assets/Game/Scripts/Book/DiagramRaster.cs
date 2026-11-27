using System;
using System.Collections.Generic;
using Origami.Core.Book;
using Origami.Core.Folding;
using Origami.Core.Geometry;
using UnityEngine;

namespace Origami.Runtime.Book
{
    /// <summary>
    /// Code rasterizer for the book pictures (C-192, CONTENT §3): Core diagram data → readable RGBA32 <see cref="Texture2D"/>.
    /// Everything is drawn on the CPU (<see cref="PixelCanvas"/>, 2× supersampled), so it is deterministic and works in
    /// batch-mode CI without a GPU. Texture y points up like paper y ("away from the seated player"), and diagrams are not
    /// rotated (only the finished-shape pictures use the model's rotation, CONTENT §3).
    /// The caller owns the returned texture and must destroy it (<see cref="BookPanel"/> caches and destroys them).
    /// </summary>
    public static class DiagramRaster
    {
        /// <summary>Smallest / largest accepted texture size in pixels.</summary>
        internal const int MinSize = 16, MaxSize = 2048;

        private const int Super = 2;

        // Palette (exact colours so tests can look for them; not part of the contract).
        internal static readonly Color32 Background = new Color32(242, 235, 220, 255);
        internal static readonly Color32 Front = new Color32(51, 128, 237, 255);       // coloured side (FoldTable front)
        internal static readonly Color32 Back = new Color32(255, 255, 255, 255);       // white side
        internal static readonly Color32 Edge = new Color32(40, 42, 60, 255);
        internal static readonly Color32 Mark = new Color32(150, 150, 158, 255);       // crease marks: thin solid line
        internal static readonly Color32 Valley = new Color32(255, 128, 13, 255);      // — — —
        internal static readonly Color32 Mountain = new Color32(158, 77, 255, 255);    // —·—·
        internal static readonly Color32 CreaseLine = new Color32(0, 150, 130, 255);   // dotted + two-way arrow
        internal static readonly Color32 Arrow = new Color32(204, 38, 38, 255);
        internal static readonly Color32 Anchor = new Color32(25, 166, 51, 255);       // magnet reference points
        internal static readonly Color32 LabelInk = new Color32(140, 25, 115, 255);    // "위 n겹"
        internal static readonly Color32 LabelBox = new Color32(255, 250, 235, 255);
        internal static readonly Color32 InsetBackground = new Color32(250, 247, 240, 255);
        internal static readonly Color32 Grey = new Color32(140, 140, 140, 255);       // silhouette of an unowned model

        /// <summary>
        /// Step diagram: the state right before the step (layer order, front = paper colour, back = white, facet edges,
        /// visible crease marks), the fold line (valley dashes / mountain dash-dot / fold-and-unfold dots), a curved arrow
        /// grab → drop with a circle at the grab point (two-way for fold-and-unfold), the "위 n겹" label for top-n folds,
        /// circles at the magnet anchors, and the state after the step as a small inset at the bottom right.
        /// </summary>
        /// <exception cref="ArgumentNullException">diagram is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">size not in 16..2048.</exception>
        public static Texture2D Render(StepDiagram diagram, int size)
        {
            if (diagram == null) throw new ArgumentNullException(nameof(diagram));
            CheckSize(size);
            int w = size * Super;
            var canvas = new PixelCanvas(w, w, Background);
            double sc = Super;

            var step = diagram.Step;
            int inset = (int)(w * 0.3), margin = (int)(w * 0.02);
            int ix0 = w - margin - inset, iy0 = margin;

            // Main picture: the "before" state, the arrow and its bulge (control point). It uses the whole square unless
            // it would run under the bottom-right inset; then the larger of the strips above / left of the inset.
            var pts = new List<Vec2>();
            AddPoints(pts, diagram.Before);
            pts.Add(step.Grab);
            pts.Add(step.Drop);
            pts.Add(ArrowControl(step.Grab, step.Drop));
            var map = Fit(pts, 0, 0, w, w, MainMargin);
            if (Overlaps(map, pts, 12.0 * sc, ix0 - margin, iy0 + inset + margin))
            {
                int free = w - inset - 2 * margin;
                var above = Fit(pts, 0, w - free, w, free, MainMargin);
                var left = Fit(pts, 0, 0, free, w, MainMargin);
                map = above.Scale >= left.Scale ? above : left;
            }

            DrawState(canvas, diagram.Before, map, false, 1.6 * sc);

            // Fold line.
            var a = map.Px(diagram.LineA);
            var b = map.Px(diagram.LineB);
            if (step.Kind == FoldStepKind.Crease)
                canvas.Pattern(a.X, a.Y, b.X, b.Y, 3.0 * sc, new[] { 0.0, 5.0 * sc }, CreaseLine);
            else if (step.Mountain)
                canvas.Pattern(a.X, a.Y, b.X, b.Y, 2.6 * sc, new[] { 9.0 * sc, 3.5 * sc, 0.0, 3.5 * sc }, Mountain);
            else
                canvas.Pattern(a.X, a.Y, b.X, b.Y, 2.6 * sc, new[] { 9.0 * sc, 5.0 * sc }, Valley);

            // Arrow grab → drop.
            var g = map.Px(step.Grab);
            var d = map.Px(step.Drop);
            DrawArrow(canvas, g, d, step.Kind == FoldStepKind.Crease, sc);
            canvas.Ring(g.X, g.Y, 5.0 * sc, 1.8 * sc, Arrow);

            // Magnet anchors (the book's only position hint).
            foreach (var anchor in diagram.Anchors)
            {
                var p = map.Px(anchor);
                canvas.Ring(p.X, p.Y, 6.5 * sc, 2.0 * sc, Anchor);
            }

            if (step.Layers > 0) DrawLayerLabel(canvas, g, d, step.Layers, w);

            // Small "after" picture at the bottom right.
            canvas.Rect(ix0, iy0, ix0 + inset, iy0 + inset, InsetBackground);
            var after = new List<Vec2>();
            AddPoints(after, diagram.After);
            DrawState(canvas, diagram.After, Fit(after, ix0, iy0, inset, inset, 1.15), false, 1.0 * sc);
            Frame(canvas, ix0, iy0, inset, (int)Math.Max(1, sc));

            return canvas.ToTexture(Super);
        }

        /// <summary>
        /// Finished shape seen from above, rotated by <paramref name="rotationDeg"/> (counter-clockwise) around its centre.
        /// <paramref name="grey"/> = uniform grey silhouette (cover of an unowned model); otherwise paper colours, edges
        /// and crease marks.
        /// </summary>
        /// <exception cref="ArgumentNullException">shape is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">size not in 16..2048.</exception>
        public static Texture2D RenderShape(PaperState shape, double rotationDeg, bool grey, int size)
        {
            if (shape == null) throw new ArgumentNullException(nameof(shape));
            CheckSize(size);
            int w = size * Super;
            var canvas = new PixelCanvas(w, w, Background);

            var pts = new List<Vec2>();
            AddPoints(pts, shape);
            var centre = Centre(pts);
            double rad = (double.IsNaN(rotationDeg) || double.IsInfinity(rotationDeg) ? 0.0 : rotationDeg) * Math.PI / 180.0;
            var rotated = new List<Vec2>(pts.Count);
            foreach (var p in pts) rotated.Add(Rotate(p, centre, rad));
            var fit = Fit(rotated, 0, 0, w, w, 1.2);
            var map = new Mapping(fit, centre, rad);
            DrawState(canvas, shape, map, grey, 1.6 * Super);
            return canvas.ToTexture(Super);
        }

        /// <summary>
        /// Page 1 "접기 기호" picture (CONTENT §3): six equal rows from the top — valley, mountain, fold-and-unfold,
        /// top-n layers ("2겹" at the arrow tail), crease mark, magnet anchor. The panel prints the row labels.
        /// </summary>
        internal static Texture2D RenderSymbols(int size)
        {
            CheckSize(size);
            int w = size * Super;
            double sc = Super;
            var canvas = new PixelCanvas(w, w, Background);
            double rowH = w / (double)SymbolRows;
            double x0 = w * 0.1, x1 = w * 0.9;
            for (int i = 0; i < SymbolRows; i++)
            {
                double y = w - (i + 0.5) * rowH;
                switch (i)
                {
                    case 0:
                        canvas.Pattern(x0, y, x1, y, 2.6 * sc, new[] { 9.0 * sc, 5.0 * sc }, Valley);
                        break;
                    case 1:
                        canvas.Pattern(x0, y, x1, y, 2.6 * sc, new[] { 9.0 * sc, 3.5 * sc, 0.0, 3.5 * sc }, Mountain);
                        break;
                    case 2:
                        canvas.Pattern(x0, y - rowH * 0.18, x1, y - rowH * 0.18, 3.0 * sc, new[] { 0.0, 5.0 * sc }, CreaseLine);
                        DrawArrow(canvas, new Vec2(x0 + w * 0.15, y + rowH * 0.05), new Vec2(x1 - w * 0.15, y + rowH * 0.05), true, sc);
                        break;
                    case 3:
                        var g = new Vec2(x0 + w * 0.22, y - rowH * 0.1);
                        var d = new Vec2(x1, y - rowH * 0.1);
                        DrawArrow(canvas, g, d, false, sc);
                        canvas.Ring(g.X, g.Y, 5.0 * sc, 1.8 * sc, Arrow);
                        DrawLayerLabel(canvas, g, d, 2, w);
                        break;
                    case 4:
                        canvas.Line(x0, y, x1, y, 1.6 * sc, Mark);
                        break;
                    default:
                        canvas.Ring(w * 0.5, y, 6.5 * sc, 2.0 * sc, Anchor);
                        break;
                }
            }
            return canvas.ToTexture(Super);
        }

        /// <summary>Number of rows of <see cref="RenderSymbols"/> (= <see cref="BookTexts.SymbolLabels"/>).</summary>
        internal const int SymbolRows = 6;

        // ---------------------------------------------------------------- helpers

        private static void CheckSize(int size)
        {
            if (size < MinSize || size > MaxSize) throw new ArgumentOutOfRangeException(nameof(size));
        }

        private static void AddPoints(List<Vec2> pts, PaperState s)
        {
            if (s == null) return;
            foreach (var f in s.Facets)
            foreach (var p in f.World)
                pts.Add(p);
        }

        private static Vec2 Centre(List<Vec2> pts)
        {
            Bounds(pts, out double minX, out double minY, out double maxX, out double maxY);
            return new Vec2((minX + maxX) * 0.5, (minY + maxY) * 0.5);
        }

        private static Vec2 Rotate(Vec2 p, Vec2 c, double rad)
        {
            double cos = Math.Cos(rad), sin = Math.Sin(rad);
            double x = p.X - c.X, y = p.Y - c.Y;
            return new Vec2(c.X + x * cos - y * sin, c.Y + x * sin + y * cos);
        }

        private const double MainMargin = 1.22;

        /// <summary>
        /// Uniform fit of the points' bounding box (each side at least 1 paper unit, times <paramref name="margin"/>)
        /// centred in the pixel rectangle (x0, y0, width, height).
        /// </summary>
        private static Mapping Fit(List<Vec2> pts, double x0, double y0, double width, double height, double margin)
        {
            Bounds(pts, out double minX, out double minY, out double maxX, out double maxY);
            var c = new Vec2((minX + maxX) * 0.5, (minY + maxY) * 0.5);
            double ex = Math.Max(1.0, maxX - minX) * margin, ey = Math.Max(1.0, maxY - minY) * margin;
            double scale = Math.Min(width / ex, height / ey);
            return new Mapping(c, scale, x0 + width * 0.5, y0 + height * 0.5);
        }

        /// <summary>True when the mapped points' box (grown by pad pixels) reaches into the corner x ≥ cornerX, y ≤ cornerY.</summary>
        private static bool Overlaps(Mapping map, List<Vec2> pts, double pad, double cornerX, double cornerY)
        {
            var mapped = new List<Vec2>(pts.Count);
            foreach (var p in pts) mapped.Add(map.Px(p));
            Bounds(mapped, out _, out double minY, out double maxX, out _);
            return maxX + pad > cornerX && minY - pad < cornerY;
        }

        private static void Bounds(List<Vec2> pts, out double minX, out double minY, out double maxX, out double maxY)
        {
            minX = minY = double.MaxValue;
            maxX = maxY = double.MinValue;
            foreach (var p in pts)
            {
                minX = Math.Min(minX, p.X);
                maxX = Math.Max(maxX, p.X);
                minY = Math.Min(minY, p.Y);
                maxY = Math.Max(maxY, p.Y);
            }
            if (pts.Count == 0) minX = minY = maxX = maxY = 0;
        }

        /// <summary>Control point of the quadratic arrow curve (bulges to the left of grab → drop).</summary>
        private static Vec2 ArrowControl(Vec2 g, Vec2 d) => Vec2.Lerp(g, d, 0.5) + (d - g).Perp * 0.25;

        /// <summary>Facets bottom → top; each facet's crease marks and edges right after its fill, so upper layers hide them.</summary>
        private static void DrawState(PixelCanvas canvas, PaperState state, Mapping map, bool grey, double lineW)
        {
            if (state == null) return;
            var facets = state.Facets;
            var order = new List<int>(facets.Count);
            for (int i = 0; i < facets.Count; i++) order.Add(i);
            order.Sort((i, j) => facets[i].Layer != facets[j].Layer ? facets[i].Layer.CompareTo(facets[j].Layer) : i.CompareTo(j));

            var marks = new List<MarkPiece>[facets.Count];
            if (!grey && state.Creases.Count > 0)
            {
                foreach (var m in DropSnap.MarkPieces(state))
                {
                    if (marks[m.FacetIndex] == null) marks[m.FacetIndex] = new List<MarkPiece>();
                    marks[m.FacetIndex].Add(m);
                }
            }

            var xs = new List<double>();
            var ys = new List<double>();
            foreach (int i in order)
            {
                var f = facets[i];
                xs.Clear();
                ys.Clear();
                foreach (var p in f.World)
                {
                    var q = map.Px(p);
                    xs.Add(q.X);
                    ys.Add(q.Y);
                }
                canvas.FillPolygon(xs, ys, grey ? Grey : f.FrontUp ? Front : Back);
                if (grey) continue;
                if (marks[i] != null)
                {
                    foreach (var m in marks[i])
                    {
                        var a = map.Px(m.A);
                        var b = map.Px(m.B);
                        canvas.Line(a.X, a.Y, b.X, b.Y, lineW, Mark);
                    }
                }
                for (int k = 0; k < xs.Count; k++)
                {
                    int n = (k + 1) % xs.Count;
                    canvas.Line(xs[k], ys[k], xs[n], ys[n], lineW, Edge);
                }
            }
        }

        /// <summary>Quadratic curve grab → drop bulging to the left of the drag direction, arrow head(s).</summary>
        private static void DrawArrow(PixelCanvas canvas, Vec2 g, Vec2 d, bool twoWay, double sc)
        {
            var dir = d - g;
            double len = dir.Length;
            if (len < 1e-9) return;
            var ctrl = ArrowControl(g, d);
            const int n = 24;
            var prev = g;
            for (int i = 1; i <= n; i++)
            {
                double t = i / (double)n;
                var p = Bezier(g, ctrl, d, t);
                canvas.Line(prev.X, prev.Y, p.X, p.Y, 2.2 * sc, Arrow);
                prev = p;
            }
            Head(canvas, d, d - ctrl, sc);
            if (twoWay) Head(canvas, g, g - ctrl, sc);
        }

        private static Vec2 Bezier(Vec2 a, Vec2 c, Vec2 b, double t)
        {
            double u = 1 - t;
            return a * (u * u) + c * (2 * u * t) + b * (t * t);
        }

        private static void Head(PixelCanvas canvas, Vec2 tip, Vec2 direction, double sc)
        {
            var u = direction.Normalized;
            if (u.LengthSq < 1e-12) return;
            double l = 11.0 * sc, h = 5.5 * sc;
            var baseP = tip - u * l;
            var p1 = baseP + u.Perp * h;
            var p2 = baseP - u.Perp * h;
            canvas.FillPolygon(new[] { tip.X, p1.X, p2.X }, new[] { tip.Y, p1.Y, p2.Y }, Arrow);
        }

        /// <summary>"위 n겹" in built-in glyphs on a light box, placed behind the arrow tail (inside the canvas).</summary>
        private static void DrawLayerLabel(PixelCanvas canvas, Vec2 grab, Vec2 drop, int layers, int w)
        {
            int cell = Math.Max(1, w / 170);
            int digitCell = Math.Max(1, (cell * 8 + 2) / 5);
            string digits = DiagramGlyphs.DigitsOf(layers);
            int gap = cell * 2;
            int width = 8 * cell + gap + digits.Length * (3 * digitCell + digitCell) - digitCell + gap + 8 * cell;
            int height = 9 * cell;
            int pad = cell * 2;

            var away = (grab - drop).Normalized;
            if (away.LengthSq < 1e-12) away = new Vec2(-1, 0);
            double cx = grab.X + away.X * (width * 0.5 + 10 * cell) + away.Perp.X * height;
            double cy = grab.Y + away.Y * (height * 0.5 + 10 * cell) + away.Perp.Y * height;
            int left = (int)Math.Round(cx - width * 0.5);
            int top = (int)Math.Round(cy + height * 0.5);
            left = Math.Max(pad, Math.Min(canvas.Width - pad - width, left));
            top = Math.Max(height + pad, Math.Min(canvas.Height - pad, top));

            canvas.Rect(left - pad, top - height - pad, left + width + pad, top + pad, LabelBox);
            canvas.Glyph(DiagramGlyphs.Wi, left, top, cell, LabelInk);
            int x = left + 8 * cell + gap;
            int digitTop = top - (height - 5 * digitCell) / 2;
            foreach (char ch in digits)
            {
                canvas.Glyph(DiagramGlyphs.Digit(ch - '0'), x, digitTop, digitCell, LabelInk);
                x += 4 * digitCell;
            }
            x += gap - digitCell;
            canvas.Glyph(DiagramGlyphs.Gyeop, x, top, cell, LabelInk);
        }

        private static void Frame(PixelCanvas canvas, int x0, int y0, int side, int t)
        {
            canvas.Rect(x0, y0, x0 + side, y0 + t, Edge);
            canvas.Rect(x0, y0 + side - t, x0 + side, y0 + side, Edge);
            canvas.Rect(x0, y0, x0 + t, y0 + side, Edge);
            canvas.Rect(x0 + side - t, y0, x0 + side, y0 + side, Edge);
        }

        /// <summary>Paper → pixel transform: optional rotation around a centre, then uniform scale around the fit centre.</summary>
        private readonly struct Mapping
        {
            private readonly Vec2 _centre;
            private readonly double _scale, _px, _py;
            private readonly bool _rotate;
            private readonly Vec2 _rotCentre;
            private readonly double _cos, _sin;

            /// <summary>Pixels per paper unit.</summary>
            public double Scale => _scale;

            public Mapping(Vec2 centre, double scale, double px, double py)
            {
                _centre = centre;
                _scale = scale;
                _px = px;
                _py = py;
                _rotate = false;
                _rotCentre = new Vec2(0, 0);
                _cos = 1;
                _sin = 0;
            }

            public Mapping(Mapping fit, Vec2 rotCentre, double rad)
            {
                _centre = fit._centre;
                _scale = fit._scale;
                _px = fit._px;
                _py = fit._py;
                _rotate = rad != 0;
                _rotCentre = rotCentre;
                _cos = Math.Cos(rad);
                _sin = Math.Sin(rad);
            }

            public Vec2 Px(Vec2 p)
            {
                if (_rotate)
                {
                    double x = p.X - _rotCentre.X, y = p.Y - _rotCentre.Y;
                    p = new Vec2(_rotCentre.X + x * _cos - y * _sin, _rotCentre.Y + x * _sin + y * _cos);
                }
                return new Vec2(_px + (p.X - _centre.X) * _scale, _py + (p.Y - _centre.Y) * _scale);
            }
        }
    }
}
