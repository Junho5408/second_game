using System;
using Origami.Core.Geometry;

namespace Origami.Core.Folding
{
    /// <summary>
    /// Input state of one drag (C-107, F-005): grab magnet, stack count M fixed at grab time, layer choice, drop magnet,
    /// preview. On release the Runtime calls the host's Fold/Crease with the same arguments as <see cref="Preview"/>.
    /// </summary>
    public sealed class FoldGesture
    {
        public const double GrabCornerRadius = 0.16, GrabEdgeRadius = 0.08;

        private FoldGesture(Vec2 grab, bool corner, int stackCount)
        {
            Grab = grab;
            GrabbedCorner = corner;
            StackCount = stackCount;
            Layers = 0;
            Drop = SnapResult.Free(grab);
        }

        /// <summary>Starts a drag at <paramref name="pointer"/>; null when nothing can be grabbed there.</summary>
        /// <exception cref="ArgumentNullException">state is null.</exception>
        public static FoldGesture TryBegin(PaperState state, Vec2 pointer)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (!state.TrySnapGrab(pointer, GrabCornerRadius, GrabEdgeRadius, out Vec2 grab, out bool corner)) return null;
            return new FoldGesture(grab, corner, state.StackCountAt(grab));
        }

        public Vec2 Grab { get; }
        public bool GrabbedCorner { get; }

        /// <summary>StackCountAt(Grab) when the drag started (M).</summary>
        public int StackCount { get; }

        /// <summary>0 = all layers, 1..M−1 = top n layers.</summary>
        public int Layers { get; private set; }

        /// <summary>Current (magnet-corrected) drop. Starts as (Grab, None).</summary>
        public SnapResult Drop { get; private set; }

        /// <summary>all → top 1 → … → top M−1 → all (negative notches go backwards). M ≤ 1 → always 0.</summary>
        public void CycleLayers(int notches)
        {
            int m = StackCount;
            if (m <= 1)
            {
                Layers = 0;
                return;
            }
            Layers = ((Layers + notches) % m + m) % m;
        }

        /// <summary>Drop = DropSnap.Snap(state, Grab, pointer).</summary>
        /// <exception cref="ArgumentNullException">state is null.</exception>
        public SnapResult MoveTo(PaperState state, Vec2 pointer)
        {
            Drop = DropSnap.Snap(state, Grab, pointer);
            return Drop;
        }

        /// <summary>Drop = DropSnap.Snap(state, Grab, pointer, guideDrop) (tutorial guide visible).</summary>
        /// <exception cref="ArgumentNullException">state is null.</exception>
        public SnapResult MoveTo(PaperState state, Vec2 pointer, Vec2 guideDrop)
        {
            Drop = DropSnap.Snap(state, Grab, pointer, guideDrop);
            return Drop;
        }

        /// <summary>PlanCrease / PlanFold with (Grab, Drop.Point, mountain, Layers).</summary>
        /// <exception cref="ArgumentNullException">state is null.</exception>
        public FoldPlan Preview(PaperState state, bool mountain, bool crease) =>
            crease
                ? FoldPlanner.PlanCrease(state, Grab, Drop.Point, Layers)
                : FoldPlanner.PlanFold(state, Grab, Drop.Point, mountain, Layers);
    }
}
