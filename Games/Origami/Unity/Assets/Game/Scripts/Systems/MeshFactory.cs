using System;
using System.Collections.Generic;
using UnityEngine;

namespace Origami.Runtime.Systems
{
    /// <summary>
    /// Code-built meshes for the room, furniture and character (C-190). Replaces <c>GameObject.CreatePrimitive</c>,
    /// which needs the 3D physics module Origami does not have (ARCHITECTURE §7). Every mesh has outward normals and
    /// front faces wound for Unity (clockwise seen from outside). The caller owns the mesh and destroys it.
    /// Sizes are in metres. Invalid sizes (≤ 0, NaN, infinite) → <see cref="ArgumentOutOfRangeException"/>;
    /// segment counts are raised to their minimum silently.
    /// </summary>
    public static class MeshFactory
    {
        /// <summary>Axis-aligned box of <paramref name="size"/>, centred on the origin (24 vertices, flat faces).</summary>
        public static Mesh Box(Vector3 size)
        {
            RequirePositive(size.x, nameof(size));
            RequirePositive(size.y, nameof(size));
            RequirePositive(size.z, nameof(size));
            var h = size * 0.5f;
            var v = new List<Vector3>(24);
            var n = new List<Vector3>(24);
            var t = new List<int>(36);
            Face(v, n, t, Vector3.right * h.x, Vector3.up * h.y, Vector3.forward * h.z);
            Face(v, n, t, Vector3.left * h.x, Vector3.up * h.y, Vector3.back * h.z);
            Face(v, n, t, Vector3.up * h.y, Vector3.forward * h.z, Vector3.right * h.x);
            Face(v, n, t, Vector3.down * h.y, Vector3.forward * h.z, Vector3.left * h.x);
            Face(v, n, t, Vector3.forward * h.z, Vector3.right * h.x, Vector3.up * h.y);
            Face(v, n, t, Vector3.back * h.z, Vector3.left * h.x, Vector3.up * h.y);
            return Finish("Box", v, n, t);
        }

        /// <summary>Upright cylinder with caps; the bottom cap's centre is the origin, the top is at y = height.</summary>
        public static Mesh Cylinder(float radius, float height, int segments = 24)
        {
            RequirePositive(radius, nameof(radius));
            RequirePositive(height, nameof(height));
            segments = Math.Max(3, segments);
            var v = new List<Vector3>();
            var n = new List<Vector3>();
            var t = new List<int>();

            // Side: one column of (bottom, top) per angle, the seam duplicated.
            for (int i = 0; i <= segments; i++)
            {
                var d = Dir(i, segments);
                v.Add(d * radius);
                n.Add(d);
                v.Add(d * radius + Vector3.up * height);
                n.Add(d);
            }
            for (int i = 0; i < segments; i++)
            {
                int a = i * 2, b = a + 2, c = a + 3, top = a + 1;
                t.Add(a); t.Add(c); t.Add(b);
                t.Add(a); t.Add(top); t.Add(c);
            }

            Cap(v, n, t, radius, height, segments, true);
            Cap(v, n, t, radius, 0f, segments, false);
            return Finish("Cylinder", v, n, t);
        }

        /// <summary>UV sphere centred on the origin (segments around, segments / 2 rings).</summary>
        public static Mesh Sphere(float radius, int segments = 16)
        {
            RequirePositive(radius, nameof(radius));
            segments = Math.Max(3, segments);
            int rings = Math.Max(2, segments / 2);
            var v = new List<Vector3>();
            var n = new List<Vector3>();
            for (int r = 0; r <= rings; r++)
            {
                float phi = Mathf.PI * r / rings;
                AddRing(v, n, phi, segments, radius, 0f);
            }
            var t = new List<int>();
            Grid(t, rings + 1, segments + 1);
            return Finish("Sphere", v, n, t);
        }

        /// <summary>
        /// Upright capsule centred on the origin: total height <paramref name="height"/> (at least 2 × radius) including the
        /// two hemispheres, axis along y (the character body).
        /// </summary>
        public static Mesh Capsule(float radius, float height, int segments = 16)
        {
            RequirePositive(radius, nameof(radius));
            RequirePositive(height, nameof(height));
            segments = Math.Max(3, segments);
            int half = Math.Max(1, segments / 4);
            float offset = Mathf.Max(0f, height * 0.5f - radius);
            var v = new List<Vector3>();
            var n = new List<Vector3>();
            for (int r = 0; r <= half; r++)
                AddRing(v, n, 0.5f * Mathf.PI * r / half, segments, radius, offset);
            for (int r = 0; r <= half; r++)
                AddRing(v, n, 0.5f * Mathf.PI + 0.5f * Mathf.PI * r / half, segments, radius, -offset);
            var t = new List<int>();
            Grid(t, 2 * half + 2, segments + 1);
            return Finish("Capsule", v, n, t);
        }

        // ------------------------------------------------------------------ helpers

        private static void RequirePositive(float value, string name)
        {
            if (!(value > 0f) || float.IsInfinity(value)) throw new ArgumentOutOfRangeException(name, value, "must be > 0 and finite");
        }

        /// <summary>Quad at <paramref name="normal"/> (its length = distance from the origin) spanned by ±u, ±w, facing outward.</summary>
        private static void Face(List<Vector3> v, List<Vector3> n, List<int> t, Vector3 normal, Vector3 u, Vector3 w)
        {
            var dir = normal.normalized;
            // Front face of (a, b, c) in Unity = Cross(b − a, c − a); make Cross(u', w') point along dir.
            if (Vector3.Dot(Vector3.Cross(u, w), dir) < 0f) w = -w;
            int i = v.Count;
            v.Add(normal - u - w);
            v.Add(normal + u - w);
            v.Add(normal + u + w);
            v.Add(normal - u + w);
            for (int k = 0; k < 4; k++) n.Add(dir);
            t.Add(i); t.Add(i + 1); t.Add(i + 2);
            t.Add(i); t.Add(i + 2); t.Add(i + 3);
        }

        private static Vector3 Dir(int i, int segments)
        {
            float a = 2f * Mathf.PI * i / segments;
            return new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
        }

        private static void Cap(List<Vector3> v, List<Vector3> n, List<int> t, float radius, float y, int segments, bool top)
        {
            var normal = top ? Vector3.up : Vector3.down;
            int centre = v.Count;
            v.Add(new Vector3(0f, y, 0f));
            n.Add(normal);
            for (int i = 0; i <= segments; i++)
            {
                v.Add(Dir(i, segments) * radius + Vector3.up * y);
                n.Add(normal);
            }
            for (int i = 0; i < segments; i++)
            {
                int a = centre + 1 + i, b = a + 1;
                if (top)
                {
                    t.Add(centre); t.Add(b); t.Add(a);
                }
                else
                {
                    t.Add(centre); t.Add(a); t.Add(b);
                }
            }
        }

        /// <summary>One latitude ring (phi from the top pole) of segments + 1 vertices, shifted up by yOffset.</summary>
        private static void AddRing(List<Vector3> v, List<Vector3> n, float phi, int segments, float radius, float yOffset)
        {
            for (int s = 0; s <= segments; s++)
            {
                float th = 2f * Mathf.PI * s / segments;
                var d = new Vector3(Mathf.Sin(phi) * Mathf.Cos(th), Mathf.Cos(phi), Mathf.Sin(phi) * Mathf.Sin(th));
                v.Add(d * radius + Vector3.up * yOffset);
                n.Add(d);
            }
        }

        /// <summary>Triangles between consecutive rows of a (rows × width) vertex grid (outward for top-down rings).</summary>
        private static void Grid(List<int> t, int rows, int width)
        {
            for (int r = 0; r < rows - 1; r++)
            {
                for (int s = 0; s < width - 1; s++)
                {
                    int a = r * width + s, b = a + width;
                    t.Add(a); t.Add(a + 1); t.Add(b);
                    t.Add(a + 1); t.Add(b + 1); t.Add(b);
                }
            }
        }

        private static Mesh Finish(string name, List<Vector3> v, List<Vector3> n, List<int> t)
        {
            var m = new Mesh { name = name };
            m.SetVertices(v);
            m.SetNormals(n);
            m.SetTriangles(t, 0);
            m.RecalculateBounds();
            return m;
        }
    }
}
