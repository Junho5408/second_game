using System;
using Origami.Core.Folding;

namespace Origami.Core.Recognition
{
    /// <summary>R-011 match score (C-110).</summary>
    public static class Similarity
    {
        /// <summary>
        /// Both outlines moved to their own centroid; max over the 8 D4 maps of b (applied to b's vertices) of the IoU.
        /// Uses only facet World polygons (layers, sides and creases are ignored).
        /// </summary>
        /// <exception cref="ArgumentNullException">a or b is null.</exception>
        public static double Centered(PaperState a, PaperState b)
        {
            if (a == null) throw new ArgumentNullException(nameof(a));
            if (b == null) throw new ArgumentNullException(nameof(b));
            var oa = Outline.FromPolygons(Outline.Centered(Outline.WorldPolygons(a), 0));
            var pb = Outline.WorldPolygons(b);
            double best = 0;
            for (int g = 0; g < D4.Count; g++)
            {
                double v = Outline.IoU(oa, Outline.FromPolygons(Outline.Centered(pb, g)));
                if (v > best) best = v;
            }
            return best;
        }
    }
}
