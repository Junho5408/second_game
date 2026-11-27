using System.Collections.Generic;
using Fold3DSpike.Core.Folding;
using UnityEngine;
using UnityEngine.Rendering;

namespace Fold3DSpike.Runtime.Gameplay
{
    /// <summary>Maps a table-plane point (x, z) at height y to its rendered position (e.g. rotated flap).</summary>
    public delegate Vector3 VertexXform(double x, double z, float y);

    /// <summary>
    /// Builds paper meshes from Core facets. Core (x, y) is world (x, z); layers stack upwards.
    /// Sub-meshes: 0 = coloured front side, 1 = white back side, 2 = crease/edge lines (unlit, vertex colour).
    /// Every facet has a separate top and bottom face (two-sided paper) a little apart (thickness).
    /// </summary>
    public static class PaperMeshBuilder
    {
        public const float BaseY = 0.003f;
        public const float LayerGap = 0.0035f;
        public const float Thickness = 0.002f;
        private const float EdgeLift = 0.0006f;
        private const float EdgeHalfWidth = 0.0045f;

        private static readonly List<Vector3> s_verts = new List<Vector3>();
        private static readonly List<Color> s_cols = new List<Color>();
        private static readonly List<int> s_front = new List<int>();
        private static readonly List<int> s_back = new List<int>();
        private static readonly List<int> s_edge = new List<int>();

        public static float LayerTop(int layer) => BaseY + layer * LayerGap;
        public static float LayerBottom(int layer) => LayerTop(layer) - Thickness;

        public static Vector3 Identity(double x, double z, float y) => new Vector3((float)x, y, (float)z);

        public static Mesh NewMesh(string name)
        {
            var m = new Mesh { name = name, indexFormat = IndexFormat.UInt32 };
            m.MarkDynamic();
            return m;
        }

        /// <summary>Fills <paramref name="mesh"/>; returns the lowest vertex y (for lifting the paper off the table).</summary>
        public static float Build(Mesh mesh, IList<Facet> facets, VertexXform xf, bool topOnly, bool edges,
            float yOffset, Color edgeColor)
        {
            if (xf == null) xf = Identity;
            s_verts.Clear();
            s_cols.Clear();
            s_front.Clear();
            s_back.Clear();
            s_edge.Clear();
            float minY = float.MaxValue;

            foreach (var f in facets)
            {
                int n = f.World.Count;
                float top = LayerTop(f.Layer) + yOffset;
                float bot = LayerBottom(f.Layer) + yOffset;

                // Top face: viewed from above it must be clockwise -> reversed fan of the CCW polygon.
                int b0 = s_verts.Count;
                for (int i = 0; i < n; i++) AddVert(xf(f.World[i].X, f.World[i].Y, top), Color.white, ref minY);
                var topList = f.FrontUp ? s_front : s_back;
                for (int i = 1; i < n - 1; i++)
                {
                    topList.Add(b0);
                    topList.Add(b0 + i + 1);
                    topList.Add(b0 + i);
                }

                if (!topOnly)
                {
                    int b1 = s_verts.Count;
                    for (int i = 0; i < n; i++) AddVert(xf(f.World[i].X, f.World[i].Y, bot), Color.white, ref minY);
                    var botList = f.FrontUp ? s_back : s_front;
                    for (int i = 1; i < n - 1; i++)
                    {
                        botList.Add(b1);
                        botList.Add(b1 + i);
                        botList.Add(b1 + i + 1);
                    }
                }

                if (edges)
                {
                    float ey = top + EdgeLift;
                    for (int i = 0; i < n; i++)
                    {
                        var a = f.World[i];
                        var b = f.World[(i + 1) % n];
                        var d = (b - a).Normalized.Perp * EdgeHalfWidth;
                        int e0 = s_verts.Count;
                        AddVert(xf(a.X - d.X, a.Y - d.Y, ey), edgeColor, ref minY);
                        AddVert(xf(b.X - d.X, b.Y - d.Y, ey), edgeColor, ref minY);
                        AddVert(xf(b.X + d.X, b.Y + d.Y, ey), edgeColor, ref minY);
                        AddVert(xf(a.X + d.X, a.Y + d.Y, ey), edgeColor, ref minY);
                        s_edge.Add(e0);
                        s_edge.Add(e0 + 1);
                        s_edge.Add(e0 + 2);
                        s_edge.Add(e0);
                        s_edge.Add(e0 + 2);
                        s_edge.Add(e0 + 3);
                    }
                }
            }

            mesh.Clear();
            mesh.SetVertices(s_verts);
            mesh.SetColors(s_cols);
            mesh.subMeshCount = 3;
            mesh.SetTriangles(s_front, 0, false);
            mesh.SetTriangles(s_back, 1, false);
            mesh.SetTriangles(s_edge, 2, false);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return s_verts.Count == 0 ? 0f : minY;
        }

        private static void AddVert(Vector3 v, Color c, ref float minY)
        {
            s_verts.Add(v);
            s_cols.Add(c);
            if (v.y < minY) minY = v.y;
        }
    }

    /// <summary>Flat unlit overlay geometry (lines, dashes, discs, rings) in one vertex-coloured mesh.</summary>
    public sealed class OverlayBuilder
    {
        private readonly List<Vector3> _v = new List<Vector3>();
        private readonly List<Color> _c = new List<Color>();
        private readonly List<int> _t = new List<int>();

        public void Clear()
        {
            _v.Clear();
            _c.Clear();
            _t.Clear();
        }

        /// <summary>Flat strip a-b of half width <paramref name="hw"/> lying in the horizontal plane.</summary>
        public void Segment(Vector3 a, Vector3 b, float hw, Color c)
        {
            var dir = b - a;
            dir.y = 0;
            if (dir.sqrMagnitude < 1e-10f) return;
            var p = new Vector3(-dir.z, 0, dir.x).normalized * hw;
            int i = _v.Count;
            _v.Add(a - p);
            _v.Add(b - p);
            _v.Add(b + p);
            _v.Add(a + p);
            for (int k = 0; k < 4; k++) _c.Add(c);
            _t.Add(i);
            _t.Add(i + 1);
            _t.Add(i + 2);
            _t.Add(i);
            _t.Add(i + 2);
            _t.Add(i + 3);
        }

        public void Dashed(Vector3 a, Vector3 b, float dash, float gap, float hw, Color c)
        {
            float len = Vector3.Distance(a, b);
            if (len < 1e-5f) return;
            var dir = (b - a) / len;
            for (float t = 0; t < len; t += dash + gap)
            {
                Segment(a + dir * t, a + dir * Mathf.Min(t + dash, len), hw, c);
            }
        }

        public void Ring(Vector3 centre, float r0, float r1, Color c, int segments = 40)
        {
            int start = _v.Count;
            for (int i = 0; i <= segments; i++)
            {
                float a = i * Mathf.PI * 2f / segments;
                var d = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                _v.Add(centre + d * r0);
                _v.Add(centre + d * r1);
                _c.Add(c);
                _c.Add(c);
            }
            for (int i = 0; i < segments; i++)
            {
                int k = start + i * 2;
                _t.Add(k);
                _t.Add(k + 1);
                _t.Add(k + 3);
                _t.Add(k);
                _t.Add(k + 3);
                _t.Add(k + 2);
            }
        }

        public void Disc(Vector3 centre, float r, Color c, int segments = 32)
        {
            int start = _v.Count;
            _v.Add(centre);
            _c.Add(c);
            for (int i = 0; i <= segments; i++)
            {
                float a = i * Mathf.PI * 2f / segments;
                _v.Add(centre + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * r);
                _c.Add(c);
            }
            for (int i = 0; i < segments; i++)
            {
                _t.Add(start);
                _t.Add(start + i + 1);
                _t.Add(start + i + 2);
            }
        }

        /// <summary>Flat arrow head at <paramref name="tip"/> pointing along a-&gt;tip.</summary>
        public void ArrowHead(Vector3 from, Vector3 tip, float size, Color c)
        {
            var dir = tip - from;
            dir.y = 0;
            if (dir.sqrMagnitude < 1e-8f) return;
            dir.Normalize();
            var side = new Vector3(-dir.z, 0, dir.x);
            int i = _v.Count;
            _v.Add(tip);
            _v.Add(tip - dir * size + side * size * 0.55f);
            _v.Add(tip - dir * size - side * size * 0.55f);
            for (int k = 0; k < 3; k++) _c.Add(c);
            _t.Add(i);
            _t.Add(i + 1);
            _t.Add(i + 2);
        }

        public void Apply(Mesh mesh)
        {
            mesh.Clear();
            mesh.SetVertices(_v);
            mesh.SetColors(_c);
            mesh.SetTriangles(_t, 0, true);
        }
    }

    /// <summary>Low-poly UV sphere (radius 0.5) for marker balls, so no physics primitives are needed.</summary>
    public static class SphereMesh
    {
        public static Mesh Create(int rings = 10, int sectors = 16)
        {
            var v = new List<Vector3>();
            var t = new List<int>();
            for (int r = 0; r <= rings; r++)
            {
                float phi = Mathf.PI * r / rings;
                for (int s = 0; s <= sectors; s++)
                {
                    float th = 2f * Mathf.PI * s / sectors;
                    v.Add(0.5f * new Vector3(Mathf.Sin(phi) * Mathf.Cos(th), Mathf.Cos(phi), Mathf.Sin(phi) * Mathf.Sin(th)));
                }
            }
            int w = sectors + 1;
            for (int r = 0; r < rings; r++)
            {
                for (int s = 0; s < sectors; s++)
                {
                    int a = r * w + s, b = a + w;
                    t.Add(a);
                    t.Add(a + 1);
                    t.Add(b);
                    t.Add(a + 1);
                    t.Add(b + 1);
                    t.Add(b);
                }
            }
            var m = new Mesh { name = "MarkerSphere" };
            m.SetVertices(v);
            m.SetTriangles(t, 0);
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }
    }
}
