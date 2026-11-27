using Origami.Core.Geometry;

namespace Origami.Core.Folding
{
    /// <summary>Corrected drop point and the anchors to highlight (C-106).</summary>
    public readonly struct SnapResult
    {
        internal SnapResult(Vec2 point, SnapKind kind, int anchorCount, Vec2 anchorA, Vec2 anchorB)
        {
            Point = point;
            Kind = kind;
            AnchorCount = anchorCount;
            AnchorA = anchorA;
            AnchorB = anchorB;
        }

        /// <summary>The corrected drop point.</summary>
        public Vec2 Point { get; }
        public SnapKind Kind { get; }

        /// <summary>Guide/Point 1 (the point), Line 2 (front/back landmarks), Mark 2 (mark segment ends), None 0.</summary>
        public int AnchorCount { get; }
        public Vec2 AnchorA { get; }
        public Vec2 AnchorB { get; }

        internal static SnapResult Free(Vec2 drop) => new SnapResult(drop, SnapKind.None, 0, default, default);
    }
}
