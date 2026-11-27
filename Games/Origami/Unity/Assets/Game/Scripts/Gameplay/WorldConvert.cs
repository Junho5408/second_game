using Origami.Core.Geometry;
using UnityEngine;

namespace Origami.Runtime.Gameplay
{
    /// <summary>The single Vec2 (double) ↔ Unity (float) conversion point for world (x, z) coordinates (CODING_CONVENTION).</summary>
    internal static class WorldConvert
    {
        public static Vector2 ToVector2(Vec2 v) => new Vector2((float)v.X, (float)v.Y);

        public static Vec2 ToVec2(Vector2 v) => new Vec2(v.x, v.y);

        /// <summary>World (x, z) at height y.</summary>
        public static Vector3 ToWorld(Vector2 xz, float y) => new Vector3(xz.x, y, xz.y);

        public static Vector3 ToWorld(Vec2 xz, float y) => new Vector3((float)xz.X, y, (float)xz.Y);
    }
}
