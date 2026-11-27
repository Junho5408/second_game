using Origami.Core.Geometry;

namespace Origami.Core.Room
{
    /// <summary>
    /// One piece of furniture of the workshop (C-180, D-130 = CONTENT §6). <see cref="Center"/> is world (x, z) — its
    /// <c>Y</c> component is the world z. Sizes in metres: width along x, depth along z, height up. Wall decorations
    /// (window, door) have a nominal depth and never block.
    /// </summary>
    public sealed class FurnitureSpec
    {
        internal FurnitureSpec(string id, Vec2 center, double width, double depth, double height, bool blocks)
        {
            Id = id;
            Center = center;
            Width = width;
            Depth = depth;
            Height = height;
            Blocks = blocks;
        }

        public string Id { get; }
        public Vec2 Center { get; }
        public double Width { get; }
        public double Depth { get; }
        public double Height { get; }

        /// <summary>The floor box (<see cref="RoomLayout.Obstacles"/>) stops the character.</summary>
        public bool Blocks { get; }
    }
}
