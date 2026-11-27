using System.Collections.Generic;
using Origami.Core.Content;
using UnityEngine;

namespace Origami.Runtime.UI
{
    /// <summary>
    /// Code-generated model shapes (C-031, F-007 AC-07: no image assets). CONTENT_SPECS recommendation:
    /// plane sky-blue triangle, boat blue trapezoid, frog green circle, crane white diamond with a red dot,
    /// dragon red star. Each is a 64×64 RGBA texture with a transparent background, created once and cached.
    /// </summary>
    /// <remarks>
    /// Textures use <c>HideFlags.HideAndDontSave</c> so scene loads / asset unloading do not destroy them;
    /// a destroyed cached texture is regenerated. Main thread only.
    /// </remarks>
    public static class ModelIcons
    {
        /// <summary>Width and height of every generated texture, in pixels.</summary>
        public const int Size = 64;

        private static readonly Color32 Clear = new Color32(0, 0, 0, 0);
        private static readonly Color32 SkyBlue = new Color32(135, 206, 250, 255);
        private static readonly Color32 Blue = new Color32(50, 100, 230, 255);
        private static readonly Color32 Green = new Color32(60, 180, 75, 255);
        private static readonly Color32 White = new Color32(250, 250, 250, 255);
        private static readonly Color32 Red = new Color32(220, 40, 40, 255);
        private static readonly Color32 PaperColor = new Color32(245, 240, 220, 255);

        private static readonly Dictionary<string, Texture2D> s_cache = new Dictionary<string, Texture2D>();
        private static Texture2D s_paper;

        /// <summary>Texture of the model <paramref name="modelId"/> (cached); null for null/unknown IDs.</summary>
        public static Texture2D Get(string modelId)
        {
            if (modelId == null || OrigamiCatalog.FindModel(modelId) == null) return null;
            if (s_cache.TryGetValue(modelId, out var cached) && cached != null) return cached;
            var tex = Create(modelId);
            s_cache[modelId] = tex;
            return tex;
        }

        /// <summary>The paper being folded: a solid single-colour square (cached).</summary>
        public static Texture2D Paper
        {
            get
            {
                if (s_paper == null) s_paper = Build("origami-paper", (x, y) => PaperColor);
                return s_paper;
            }
        }

        private static Texture2D Create(string modelId)
        {
            switch (modelId)
            {
                case ModelIds.Plane:
                    return Build("origami-plane", (x, y) =>
                        InPolygon(x, y, new[] { new Vector2(6f, 10f), new Vector2(58f, 32f), new Vector2(6f, 54f) }) ? SkyBlue : Clear);
                case ModelIds.Boat:
                    return Build("origami-boat", (x, y) =>
                        InPolygon(x, y, new[] { new Vector2(18f, 14f), new Vector2(46f, 14f), new Vector2(60f, 40f), new Vector2(4f, 40f) }) ? Blue : Clear);
                case ModelIds.Frog:
                    return Build("origami-frog", (x, y) => InCircle(x, y, 32f, 32f, 26f) ? Green : Clear);
                case ModelIds.Crane:
                    return Build("origami-crane", (x, y) =>
                    {
                        if (InCircle(x, y, 32f, 36f, 6f)) return Red;
                        return Mathf.Abs(x - 32f) + Mathf.Abs(y - 32f) <= 28f ? White : Clear;
                    });
                case ModelIds.Dragon:
                    var star = StarPolygon(32f, 30f, 30f, 12f);
                    return Build("origami-dragon", (x, y) => InPolygon(x, y, star) ? Red : Clear);
                default:
                    // A catalog model without a dedicated shape: plain white square (still code-drawn).
                    return Build("origami-" + modelId, (x, y) => White);
            }
        }

        private delegate Color32 PixelFunc(float x, float y);

        private static Texture2D Build(string name, PixelFunc pixel)
        {
            var pixels = new Color32[Size * Size];
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    // Sample at the pixel centre; texture row 0 is the bottom.
                    pixels[y * Size + x] = pixel(x + 0.5f, y + 0.5f);
                }
            }
            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false)
            {
                name = name,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
            };
            tex.SetPixels32(pixels);
            tex.Apply(false);
            return tex;
        }

        private static bool InCircle(float x, float y, float cx, float cy, float r)
        {
            float dx = x - cx, dy = y - cy;
            return dx * dx + dy * dy <= r * r;
        }

        /// <summary>Even-odd point-in-polygon test.</summary>
        private static bool InPolygon(float x, float y, Vector2[] poly)
        {
            bool inside = false;
            for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
            {
                var a = poly[i];
                var b = poly[j];
                if ((a.y > y) != (b.y > y) && x < (b.x - a.x) * (y - a.y) / (b.y - a.y) + a.x) inside = !inside;
            }
            return inside;
        }

        /// <summary>Five-pointed star, first point straight up.</summary>
        private static Vector2[] StarPolygon(float cx, float cy, float outer, float inner)
        {
            var pts = new Vector2[10];
            for (int i = 0; i < 10; i++)
            {
                float r = (i % 2 == 0) ? outer : inner;
                float angle = Mathf.PI / 2f + i * Mathf.PI / 5f;
                pts[i] = new Vector2(cx + r * Mathf.Cos(angle), cy + r * Mathf.Sin(angle));
            }
            return pts;
        }
    }
}
