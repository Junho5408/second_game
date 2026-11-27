using UnityEngine;

namespace Origami.Runtime.Folding
{
    /// <summary>Maps a paper-plane point (x, z) at height y to its rendered local position (e.g. the rotated flap).</summary>
    public delegate Vector3 VertexXform(double x, double z, float y);
}
