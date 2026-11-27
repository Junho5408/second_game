using System;
using Origami.Core.Catalog;
using Origami.Core.Geometry;

namespace Origami.Core.Room
{
    /// <summary>Interaction candidate (C-180, R-004).</summary>
    public static class Interaction
    {
        /// <summary>
        /// The target whose interaction point is within <see cref="RoomLayout.InteractRange"/> (≤ 1.2 m) of
        /// <paramref name="position"/> (world x, z) and nearest; ties (distances rounded to 9 decimals) go to the
        /// <see cref="InteractTargets.All"/> order (table &gt; bookshelf &gt; counter &gt; bed). Null when none is in range.
        /// </summary>
        public static string Nearest(Vec2 position)
        {
            string best = null;
            double bestKey = 0;
            foreach (var t in InteractTargets.All)
            {
                double key = Math.Round(Vec2.Distance(position, RoomLayout.InteractPoint(t)), 9, MidpointRounding.ToEven);
                if (!(key <= RoomLayout.InteractRange)) continue;   // also NaN
                if (best == null || key < bestKey)
                {
                    best = t;
                    bestKey = key;
                }
            }
            return best;
        }
    }
}
