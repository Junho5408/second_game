using System;
using Origami.Core.Geometry;

namespace Origami.Core.Room
{
    /// <summary>
    /// Walking movement with circle (r = 0.3) vs box collision (C-180, F-002, A2-6). Presentation helper: the only
    /// time-dependent pure function in Core; it does not touch rule state.
    /// </summary>
    public static class RoomMotion
    {
        /// <summary>Longest sub-step in seconds (0.15 m at walking speed — less than any obstacle, so no tunnelling).</summary>
        public const double MaxSubstep = 0.05;

        /// <summary>
        /// Upper bound of simulated time per call. After 600 s any constant input has long reached its end position
        /// (the room is 8 m wide); the bound keeps an infinite or absurd dt from looping forever.
        /// </summary>
        private const double MaxSimulatedTime = 600;

        private const double MinMoveLength = 1e-6;

        /// <summary>
        /// Moves <paramref name="position"/> (world x, z) by <paramref name="move"/> (x east, y north) for <paramref name="dt"/>
        /// seconds at <see cref="RoomLayout.WalkSpeed"/>. dt ≤ 0 or NaN → position. Components are clamped to [−1, 1] (NaN → 0),
        /// a longer vector is normalised (diagonal at the same speed), length &lt; 1e-6 → position. dt is split into
        /// ⌈dt / MaxSubstep⌉ equal parts; in each, x moves first (clamped to the walls, pushed back onto the face of an obstacle
        /// grown by the radius when it ends strictly inside), then z the same way.
        /// </summary>
        public static Vec2 Step(Vec2 position, Vec2 move, double dt)
        {
            if (!(dt > 0)) return position;   // also NaN
            if (dt > MaxSimulatedTime) dt = MaxSimulatedTime;

            double mx = Clamp1(move.X), mz = Clamp1(move.Y);
            double len = Math.Sqrt(mx * mx + mz * mz);
            if (len < MinMoveLength) return position;
            if (len > 1)
            {
                mx /= len;
                mz /= len;
            }

            int parts = (int)Math.Ceiling(dt / MaxSubstep);
            if (parts < 1) parts = 1;
            double h = dt / parts;
            double r = RoomLayout.CharacterRadius;
            double x = position.X, z = position.Y;
            var boxes = RoomLayout.Obstacles;

            for (int i = 0; i < parts; i++)
            {
                if (mx != 0)
                {
                    x = Clamp(x + mx * RoomLayout.WalkSpeed * h, RoomLayout.MinX + r, RoomLayout.MaxX - r);
                    for (int b = 0; b < boxes.Count; b++)
                    {
                        var o = boxes[b];
                        if (Inside(x, z, o, r)) x = mx > 0 ? o.MinX - r : o.MaxX + r;
                    }
                }
                if (mz != 0)
                {
                    z = Clamp(z + mz * RoomLayout.WalkSpeed * h, RoomLayout.MinZ + r, RoomLayout.MaxZ - r);
                    for (int b = 0; b < boxes.Count; b++)
                    {
                        var o = boxes[b];
                        if (Inside(x, z, o, r)) z = mz > 0 ? o.MinZ - r : o.MaxZ + r;
                    }
                }
            }
            return new Vec2(x, z);
        }

        /// <summary>Strictly inside the box grown by r (the boundary is outside).</summary>
        private static bool Inside(double x, double z, Box o, double r) =>
            x > o.MinX - r && x < o.MaxX + r && z > o.MinZ - r && z < o.MaxZ + r;

        private static double Clamp1(double v)
        {
            if (double.IsNaN(v)) return 0;
            return v < -1 ? -1 : (v > 1 ? 1 : v);
        }

        private static double Clamp(double v, double lo, double hi) => v < lo ? lo : (v > hi ? hi : v);
    }
}
