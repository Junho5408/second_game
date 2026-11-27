using Origami.Core.Geometry;

namespace Origami.Core.Folding
{
    /// <summary>One recorded action: reference sequences, undo history, save data (C-103, D-104). Immutable.</summary>
    public sealed class FoldStep
    {
        private FoldStep(FoldStepKind kind, Vec2 grab, Vec2 drop, bool mountain, int layers)
        {
            Kind = kind;
            Grab = grab;
            Drop = drop;
            Mountain = mountain;
            Layers = layers;
        }

        /// <summary><c>Fold(grab, drop, mountain, layers)</c>; layers 0 = all layers.</summary>
        public static FoldStep Fold(Vec2 grab, Vec2 drop, bool mountain = false, int layers = 0) =>
            new FoldStep(FoldStepKind.Fold, grab, drop, mountain, layers);

        /// <summary><c>Crease(grab, drop, layers)</c>.</summary>
        public static FoldStep Crease(Vec2 grab, Vec2 drop, int layers = 0) =>
            new FoldStep(FoldStepKind.Crease, grab, drop, false, layers);

        /// <summary>Turn the sheet over. Grab/Drop (0,0), not mountain, 0 layers.</summary>
        public static FoldStep Flip() => new FoldStep(FoldStepKind.Flip, new Vec2(0, 0), new Vec2(0, 0), false, 0);

        public FoldStepKind Kind { get; }
        public Vec2 Grab { get; }
        public Vec2 Drop { get; }
        public bool Mountain { get; }
        public int Layers { get; }
    }
}
