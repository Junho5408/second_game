using System.Collections.Generic;
using UnityEngine;

namespace Origami.Runtime.Folding
{
    /// <summary>Low-poly UV sphere (radius 0.5) for marker balls, so no physics primitives are needed (Fold3DSpike port).</summary>
    public static class SphereMesh
    {
        public static Mesh Create(int rings = 10, int sectors = 16)
        {
            if (rings < 2) rings = 2;
            if (sectors < 3) sectors = 3;
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
