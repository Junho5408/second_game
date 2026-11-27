using System.Collections.Generic;
using UnityEngine;

namespace Origami.Runtime.Folding
{
    /// <summary>
    /// Flat unlit overlay geometry (lines, dashes, discs, rings, arrow heads) in one vertex-coloured mesh, lying in the
    /// horizontal plane (ported from Fold3DSpike <c>OverlayBuilder</c>).
    /// </summary>
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

        /// <summary>Flat strip a–b of half width <paramref name="hw"/> lying in the horizontal plane.</summary>
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
            if (len < 1e-5f || dash + gap <= 0f) return;
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

        /// <summary>Flat arrow head at <paramref name="tip"/> pointing along from → tip.</summary>
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

        /// <summary>Replaces the contents of <paramref name="mesh"/> with the overlay.</summary>
        public void Apply(Mesh mesh)
        {
            mesh.Clear();
            mesh.SetVertices(_v);
            mesh.SetColors(_c);
            mesh.SetTriangles(_t, 0, true);
        }
    }
}
