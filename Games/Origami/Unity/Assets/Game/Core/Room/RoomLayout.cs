using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Origami.Core.Catalog;
using Origami.Core.Geometry;

namespace Origami.Core.Room
{
    /// <summary>
    /// Workshop layout numbers (C-180, D-130 = CONTENT §6, F-002). World (x, z) metres: x east, z north (away from the
    /// walking camera); a <see cref="Vec2"/> here carries (x, z) in (X, Y). All values are constants of the design.
    /// </summary>
    public static class RoomLayout
    {
        public const double MinX = -4, MaxX = 4, MinZ = -3, MaxZ = 3, WallHeight = 2.5;
        public const double CharacterRadius = 0.3, WalkSpeed = 3.0, InteractRange = 1.2, CharacterHeight = 1.6;

        /// <summary>Furniture IDs that are not interaction targets (decoration).</summary>
        internal const string Chair = "chair", Trash = "trash", Window = "window", Door = "door";

        private const double WallDecorDepth = 0.05;

        private static readonly IReadOnlyList<FurnitureSpec> FurnitureList = new ReadOnlyCollection<FurnitureSpec>(new[]
        {
            new FurnitureSpec(InteractTargets.Table, new Vec2(2.5, 1.6), 1.4, 0.9, 0.75, true),
            new FurnitureSpec(Chair, new Vec2(2.5, 0.8), 0.5, 0.5, 0.45, false),
            new FurnitureSpec(InteractTargets.Bookshelf, new Vec2(0.0, 2.75), 1.6, 0.4, 2.0, true),
            new FurnitureSpec(InteractTargets.Counter, new Vec2(2.5, -2.3), 2.0, 0.6, 1.0, true),
            new FurnitureSpec(InteractTargets.Bed, new Vec2(-3.0, 2.0), 1.0, 2.0, 0.5, true),
            new FurnitureSpec(Trash, new Vec2(3.6, 0.6), 0.4, 0.4, 0.5, true),
            new FurnitureSpec(Window, new Vec2(-1.8, MaxZ), 1.2, WallDecorDepth, 1.0, false),
            new FurnitureSpec(Door, new Vec2(-3.0, MinZ), 1.0, WallDecorDepth, 2.1, false),
        });

        // D-130 floor boxes as literals (not centre ± size/2, which would round differently), Blocks furniture in Furniture order.
        private static readonly IReadOnlyList<Box> ObstacleList = new ReadOnlyCollection<Box>(new[]
        {
            new Box(1.8, 1.15, 3.2, 2.05),    // table
            new Box(-0.8, 2.55, 0.8, 2.95),   // bookshelf
            new Box(1.5, -2.6, 3.5, -2.0),    // counter
            new Box(-3.5, 1.0, -2.5, 3.0),    // bed
            new Box(3.4, 0.4, 3.8, 0.8),      // trash
        });

        /// <summary>Start / morning position (−1.6, 0.0); the character faces east (+x).</summary>
        public static Vec2 StartPosition => new Vec2(-1.6, 0.0);

        /// <summary>Seated position (2.5, 0.8) (on the chair).</summary>
        public static Vec2 ChairPosition => new Vec2(2.5, 0.8);

        /// <summary>Position after standing up (2.5, 0.2); the character faces south.</summary>
        public static Vec2 StandUpPosition => new Vec2(2.5, 0.2);

        /// <summary>table, chair, bookshelf, counter, bed, trash, window, door (read-only).</summary>
        public static IReadOnlyList<FurnitureSpec> Furniture => FurnitureList;

        /// <summary>Floor boxes of the blocking furniture: table, bookshelf, counter, bed, trash (read-only).</summary>
        public static IReadOnlyList<Box> Obstacles => ObstacleList;

        /// <summary>Interaction point of a target (R-004, D-130).</summary>
        /// <exception cref="ArgumentException">Unknown target (ordinal; null → <see cref="ArgumentNullException"/>).</exception>
        public static Vec2 InteractPoint(string target)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            switch (target)
            {
                case InteractTargets.Table: return new Vec2(2.5, 0.8);
                case InteractTargets.Bookshelf: return new Vec2(0.0, 1.9);
                case InteractTargets.Counter: return new Vec2(2.5, -1.5);
                case InteractTargets.Bed: return new Vec2(-2.0, 2.0);
                default: throw new ArgumentException("Unknown interaction target: " + target, nameof(target));
            }
        }
    }
}
