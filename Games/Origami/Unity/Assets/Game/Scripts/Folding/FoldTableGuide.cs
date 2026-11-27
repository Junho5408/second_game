using Origami.Core.Geometry;

namespace Origami.Runtime.Folding
{
    /// <summary>
    /// Tutorial guide target the host shows on the table (C-191, F-006): grab point (yellow ball), drop point
    /// (green ring, also the guide drop magnet), ideal crease line A–B (red dashes). Paper coordinates.
    /// </summary>
    public readonly struct FoldTableGuide
    {
        public FoldTableGuide(Vec2 grab, Vec2 drop, Vec2 lineA, Vec2 lineB, bool isCrease)
        {
            Grab = grab;
            Drop = drop;
            LineA = lineA;
            LineB = lineB;
            IsCrease = isCrease;
        }

        public Vec2 Grab { get; }
        public Vec2 Drop { get; }
        public Vec2 LineA { get; }
        public Vec2 LineB { get; }

        /// <summary>The step is a crease (fold and unfold, Ctrl on release).</summary>
        public bool IsCrease { get; }
    }
}
