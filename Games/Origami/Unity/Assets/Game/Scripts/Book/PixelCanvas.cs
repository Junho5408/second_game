using System;
using System.Collections.Generic;
using UnityEngine;

namespace Origami.Runtime.Book
{
    /// <summary>
    /// CPU pixel buffer used by <see cref="DiagramRaster"/> (no GPU, so it works in headless CI). Coordinates are pixels
    /// with (0,0) at the bottom-left (same as <c>Texture2D</c>). Shapes are drawn hard-edged (pixel centre inside → pixel
    /// set); <see cref="ToTexture"/> box-downsamples the supersampled buffer, so interior pixels keep their exact colour.
    /// </summary>
    internal sealed class PixelCanvas
    {
        private readonly Color32[] _px;
        private readonly List<double> _xs = new List<double>();

        public PixelCanvas(int width, int height, Color32 background)
        {
            Width = width;
            Height = height;
            _px = new Color32[width * height];
            for (int i = 0; i < _px.Length; i++) _px[i] = background;
        }

        public int Width { get; }
        public int Height { get; }

        public Color32 Get(int x, int y) => _px[y * Width + x];

        private void Set(int x, int y, Color32 c)
        {
            if (x < 0 || y < 0 || x >= Width || y >= Height) return;
            int i = y * Width + x;
            if (c.a == 255)
            {
                _px[i] = c;
                return;
            }
            var d = _px[i];
            int a = c.a, ia = 255 - a;
            _px[i] = new Color32((byte)((c.r * a + d.r * ia) / 255), (byte)((c.g * a + d.g * ia) / 255),
                (byte)((c.b * a + d.b * ia) / 255), (byte)Math.Max(a, (int)d.a));
        }

        /// <summary>Even-odd scanline fill of a polygon given as pixel-space points (x0, y0, x1, y1, …).</summary>
        public void FillPolygon(IReadOnlyList<double> xs, IReadOnlyList<double> ys, Color32 c)
        {
            int n = xs.Count;
            if (n < 3) return;
            double minY = double.MaxValue, maxY = double.MinValue;
            for (int i = 0; i < n; i++)
            {
                minY = Math.Min(minY, ys[i]);
                maxY = Math.Max(maxY, ys[i]);
            }
            int row0 = Math.Max(0, (int)Math.Floor(minY - 0.5));
            int row1 = Math.Min(Height - 1, (int)Math.Ceiling(maxY + 0.5));
            for (int row = row0; row <= row1; row++)
            {
                double yc = row + 0.5;
                _xs.Clear();
                for (int i = 0; i < n; i++)
                {
                    int j = (i + 1) % n;
                    double y0 = ys[i], y1 = ys[j];
                    if ((y0 <= yc && yc < y1) || (y1 <= yc && yc < y0))
                        _xs.Add(xs[i] + (yc - y0) / (y1 - y0) * (xs[j] - xs[i]));
                }
                _xs.Sort();
                for (int k = 0; k + 1 < _xs.Count; k += 2)
                {
                    int a = Math.Max(0, (int)Math.Ceiling(_xs[k] - 0.5));
                    int b = Math.Min(Width - 1, (int)Math.Floor(_xs[k + 1] - 0.5));
                    for (int x = a; x <= b; x++) Set(x, row, c);
                }
            }
        }

        /// <summary>Solid segment of the given width (round caps).</summary>
        public void Line(double ax, double ay, double bx, double by, double width, Color32 c)
        {
            double r = Math.Max(0.5, width * 0.5);
            int x0 = Math.Max(0, (int)Math.Floor(Math.Min(ax, bx) - r - 1));
            int x1 = Math.Min(Width - 1, (int)Math.Ceiling(Math.Max(ax, bx) + r + 1));
            int y0 = Math.Max(0, (int)Math.Floor(Math.Min(ay, by) - r - 1));
            int y1 = Math.Min(Height - 1, (int)Math.Ceiling(Math.Max(ay, by) + r + 1));
            double dx = bx - ax, dy = by - ay, len2 = dx * dx + dy * dy;
            double r2 = r * r;
            for (int y = y0; y <= y1; y++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    double px = x + 0.5 - ax, py = y + 0.5 - ay;
                    double t = len2 > 0 ? Math.Max(0, Math.Min(1, (px * dx + py * dy) / len2)) : 0;
                    double ex = px - t * dx, ey = py - t * dy;
                    if (ex * ex + ey * ey <= r2) Set(x, y, c);
                }
            }
        }

        /// <summary>
        /// Patterned segment: <paramref name="pattern"/> alternates "on" and "off" lengths in pixels, starting with "on".
        /// A pattern entry of 0 "on" length draws a dot (used for dash-dot).
        /// </summary>
        public void Pattern(double ax, double ay, double bx, double by, double width, double[] pattern, Color32 c)
        {
            double dx = bx - ax, dy = by - ay, len = Math.Sqrt(dx * dx + dy * dy);
            if (len <= 0) return;
            double ux = dx / len, uy = dy / len;
            double s = 0;
            int k = 0;
            while (s < len && k < 100000)
            {
                double seg = Math.Max(0, pattern[k % pattern.Length]);
                bool on = k % 2 == 0;
                double e = Math.Min(len, s + seg);
                if (on) Line(ax + ux * s, ay + uy * s, ax + ux * e, ay + uy * e, width, c);   // seg 0 = round dot
                else if (seg <= 0) e = s + 1;                                                   // never stall
                s = e;
                k++;
            }
        }

        /// <summary>Ring of radius r (pixels) and the given stroke width.</summary>
        public void Ring(double cx, double cy, double r, double width, Color32 c)
        {
            double h = Math.Max(0.5, width * 0.5);
            int x0 = Math.Max(0, (int)Math.Floor(cx - r - h - 1)), x1 = Math.Min(Width - 1, (int)Math.Ceiling(cx + r + h + 1));
            int y0 = Math.Max(0, (int)Math.Floor(cy - r - h - 1)), y1 = Math.Min(Height - 1, (int)Math.Ceiling(cy + r + h + 1));
            for (int y = y0; y <= y1; y++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    double ex = x + 0.5 - cx, ey = y + 0.5 - cy;
                    if (Math.Abs(Math.Sqrt(ex * ex + ey * ey) - r) <= h) Set(x, y, c);
                }
            }
        }

        /// <summary>Filled disc of radius r (pixels).</summary>
        public void Disc(double cx, double cy, double r, Color32 c)
        {
            int x0 = Math.Max(0, (int)Math.Floor(cx - r - 1)), x1 = Math.Min(Width - 1, (int)Math.Ceiling(cx + r + 1));
            int y0 = Math.Max(0, (int)Math.Floor(cy - r - 1)), y1 = Math.Min(Height - 1, (int)Math.Ceiling(cy + r + 1));
            for (int y = y0; y <= y1; y++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    double ex = x + 0.5 - cx, ey = y + 0.5 - cy;
                    if (ex * ex + ey * ey <= r * r) Set(x, y, c);
                }
            }
        }

        /// <summary>Axis-aligned filled rectangle [x0, x1) × [y0, y1) in pixels.</summary>
        public void Rect(int x0, int y0, int x1, int y1, Color32 c)
        {
            for (int y = Math.Max(0, y0); y < Math.Min(Height, y1); y++)
            for (int x = Math.Max(0, x0); x < Math.Min(Width, x1); x++)
                Set(x, y, c);
        }

        /// <summary>Glyph bitmap (rows top → bottom, '#' = ink) with its top-left cell at (left, top), cell size = scale.</summary>
        public void Glyph(string[] rows, int left, int top, int scale, Color32 c)
        {
            for (int r = 0; r < rows.Length; r++)
            {
                for (int col = 0; col < rows[r].Length; col++)
                {
                    if (rows[r][col] != '#') continue;
                    int x = left + col * scale, y = top - (r + 1) * scale;
                    Rect(x, y, x + scale, y + scale, c);
                }
            }
        }

        /// <summary>Box-downsampled copy (factor = supersampling) as a readable, non-mipmapped RGBA32 texture.</summary>
        public Texture2D ToTexture(int factor)
        {
            int w = Width / factor, h = Height / factor, n = factor * factor;
            var outPx = new Color32[w * h];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int r = 0, g = 0, b = 0, a = 0;
                    for (int sy = 0; sy < factor; sy++)
                    for (int sx = 0; sx < factor; sx++)
                    {
                        var p = _px[(y * factor + sy) * Width + x * factor + sx];
                        r += p.r;
                        g += p.g;
                        b += p.b;
                        a += p.a;
                    }
                    outPx[y * w + x] = new Color32((byte)(r / n), (byte)(g / n), (byte)(b / n), (byte)(a / n));
                }
            }
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.name = "OrigamiDiagram";
            tex.hideFlags = HideFlags.DontSave;
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            tex.SetPixels32(outPx);
            tex.Apply(false, false);
            return tex;
        }
    }
}
