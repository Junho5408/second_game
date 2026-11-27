using System;
using System.Collections.Generic;
using Origami.Core.Folding;
using Origami.Core.Geometry;
using UnityEngine;
using UnityEngine.Rendering;

namespace Origami.Runtime.Folding
{
    /// <summary>
    /// Builds paper meshes from Core facets (C-191, ported from Fold3DSpike <c>PaperMeshBuilder</c>). Paper (x, y) is local
    /// (x, z) of the paper space; layers stack upwards (paper units — the <see cref="FoldTable"/> scales the space by 0.1).
    /// Sub-meshes: 0 = coloured front side, 1 = white back side, 2 = outline edges (unlit, vertex colour).
    /// Every facet has a separate top and bottom face (two-sided paper) a little apart (<see cref="Thickness"/>).
    /// Not thread safe (shared scratch lists, main thread only).
    /// </summary>
    public static class PaperMeshBuilder
    {
        public const float BaseY = 0.003f;
        public const float LayerGap = 0.0035f;
        public const float Thickness = 0.002f;
        private const float EdgeLift = 0.0006f;
        private const float EdgeHalfWidth = 0.0045f;
        private const float CreaseLift = 0.0009f;
        private const double CutEps = 1e-9;
        private const double MinPieceLength = 1e-6;

        private static readonly List<Vector3> s_verts = new List<Vector3>();
        private static readonly List<Color> s_cols = new List<Color>();
        private static readonly List<int> s_front = new List<int>();
        private static readonly List<int> s_back = new List<int>();
        private static readonly List<int> s_edge = new List<int>();
        private static readonly List<double> s_cuts = new List<double>();

        /// <summary>Height of the top face of <paramref name="layer"/>.</summary>
        public static float LayerTop(int layer) => BaseY + layer * LayerGap;

        /// <summary>Height of the bottom face of <paramref name="layer"/>.</summary>
        public static float LayerBottom(int layer) => LayerTop(layer) - Thickness;

        /// <summary>The identity transform: (x, z) at height y → (x, y, z).</summary>
        public static Vector3 Identity(double x, double z, float y) => new Vector3((float)x, y, (float)z);

        /// <summary>A dynamic mesh with 32-bit indices.</summary>
        public static Mesh NewMesh(string name)
        {
            var m = new Mesh { name = name, indexFormat = IndexFormat.UInt32 };
            m.MarkDynamic();
            return m;
        }

        /// <summary>
        /// Fills <paramref name="mesh"/> with <paramref name="facets"/> (transformed by <paramref name="xf"/>, null = identity);
        /// returns the lowest vertex y (for lifting the paper off the table), 0 when empty.
        /// </summary>
        public static float Build(Mesh mesh, IReadOnlyList<Facet> facets, VertexXform xf, bool topOnly, bool edges,
            float yOffset, Color edgeColor)
        {
            if (mesh == null) throw new ArgumentNullException(nameof(mesh));
            if (xf == null) xf = Identity;
            s_verts.Clear();
            s_cols.Clear();
            s_front.Clear();
            s_back.Clear();
            s_edge.Clear();
            float minY = float.MaxValue;

            if (facets != null)
            {
                foreach (var f in facets)
                {
                    int n = f.World.Count;
                    float top = LayerTop(f.Layer) + yOffset;
                    float bot = LayerBottom(f.Layer) + yOffset;

                    // Top face: seen from above it must be clockwise → reversed fan of the CCW polygon.
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

        /// <summary>
        /// Adds the crease marks of <paramref name="state"/> (F-005 "자국 선") as flat strips of half width
        /// <paramref name="halfWidth"/>: <see cref="DropSnap.MarkPieces"/> cut where higher facets' edges cross them, and only
        /// the parts visible from above (<see cref="DropSnap.IsVisible"/> at each part's midpoint), each just above its
        /// facet's top face.
        /// </summary>
        public static void AddCreaseLines(OverlayBuilder overlay, PaperState state, float halfWidth, Color color)
        {
            AddCreaseLinesCounted(overlay, state, halfWidth, color);
        }

        /// <summary><see cref="AddCreaseLines"/>; returns the number of strips added.</summary>
        internal static int AddCreaseLinesCounted(OverlayBuilder overlay, PaperState state, float halfWidth, Color color)
        {
            if (overlay == null) throw new ArgumentNullException(nameof(overlay));
            if (state == null || state.Creases.Count == 0) return 0;
            int count = 0;
            var facets = state.Facets;
            foreach (var piece in DropSnap.MarkPieces(state))
            {
                Vec2 a = piece.A, b = piece.B;
                var d = b - a;
                if (d.Length < MinPieceLength) continue;
                int layer = facets[piece.FacetIndex].Layer;

                // Cut the piece wherever an edge of a higher facet crosses it; visibility is constant between cuts.
                s_cuts.Clear();
                s_cuts.Add(0.0);
                s_cuts.Add(1.0);
                for (int g = 0; g < facets.Count; g++)
                {
                    if (g == piece.FacetIndex || facets[g].Layer <= layer) continue;
                    var w = facets[g].World;
                    for (int i = 0; i < w.Count; i++)
                    {
                        if (SegmentParam(a, d, w[i], w[(i + 1) % w.Count], out double t)) s_cuts.Add(t);
                    }
                }
                s_cuts.Sort();

                float y = LayerTop(layer) + CreaseLift;
                double runStart = -1.0, runEnd = -1.0;
                for (int k = 0; k + 1 < s_cuts.Count; k++)
                {
                    double t0 = s_cuts[k], t1 = s_cuts[k + 1];
                    if ((t1 - t0) * d.Length < MinPieceLength) continue;
                    bool visible = DropSnap.IsVisible(state, a + d * (0.5 * (t0 + t1)), piece.FacetIndex);
                    if (visible)
                    {
                        if (runStart < 0.0) runStart = t0;
                        runEnd = t1;
                    }
                    else if (runStart >= 0.0)
                    {
                        count += Emit(overlay, a, d, runStart, runEnd, y, halfWidth, color);
                        runStart = -1.0;
                    }
                }
                if (runStart >= 0.0) count += Emit(overlay, a, d, runStart, runEnd, y, halfWidth, color);
            }
            return count;
        }

        private static int Emit(OverlayBuilder overlay, Vec2 a, Vec2 d, double t0, double t1, float y, float hw, Color c)
        {
            var p = a + d * t0;
            var q = a + d * t1;
            overlay.Segment(new Vector3((float)p.X, y, (float)p.Y), new Vector3((float)q.X, y, (float)q.Y), hw, c);
            return 1;
        }

        /// <summary>Parameter t ∈ (0, 1) on a + d·t where segment p–q crosses it (proper crossing only).</summary>
        private static bool SegmentParam(Vec2 a, Vec2 d, Vec2 p, Vec2 q, out double t)
        {
            t = 0.0;
            var e = q - p;
            double den = Vec2.Cross(d, e);
            if (Math.Abs(den) < 1e-12) return false;
            var ap = p - a;
            t = Vec2.Cross(ap, e) / den;
            double u = Vec2.Cross(ap, d) / den;
            return t > CutEps && t < 1.0 - CutEps && u >= -CutEps && u <= 1.0 + CutEps;
        }

        private static void AddVert(Vector3 v, Color c, ref float minY)
        {
            s_verts.Add(v);
            s_cols.Add(c);
            if (v.y < minY) minY = v.y;
        }
    }
}
