using Origami.Core.Geometry;

namespace Origami.Core.Folding
{
    /// <summary>A crease mark clipped to one facet, in table (world) coordinates (C-106, Python <c>mark_pieces</c>).</summary>
    public readonly struct MarkPiece
    {
        internal MarkPiece(Vec2 a, Vec2 b, int facetIndex)
        {
            A = a;
            B = b;
            FacetIndex = facetIndex;
        }

        public Vec2 A { get; }
        public Vec2 B { get; }

        /// <summary>Index into <see cref="PaperState.Facets"/>.</summary>
        public int FacetIndex { get; }
    }
}
