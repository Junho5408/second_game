using System;
using Origami.Core.Geometry;

namespace Origami.Core.Folding
{
    /// <summary>A crease mark: segment in original (unfolded) paper coordinates (C-102, D-101).</summary>
    public readonly struct CreaseSegment : IEquatable<CreaseSegment>
    {
        public CreaseSegment(Vec2 a, Vec2 b)
        {
            A = a;
            B = b;
        }

        public Vec2 A { get; }
        public Vec2 B { get; }
        public double Length => Vec2.Distance(A, B);

        /// <summary>Bitwise equality of both end points (direction matters).</summary>
        public bool Equals(CreaseSegment other) => A.Equals(other.A) && B.Equals(other.B);
        public override bool Equals(object obj) => obj is CreaseSegment c && Equals(c);
        public override int GetHashCode() => A.GetHashCode() * 31 ^ B.GetHashCode();
    }
}
