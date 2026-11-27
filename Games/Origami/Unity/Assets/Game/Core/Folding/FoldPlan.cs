using System.Collections.Generic;
using System.Collections.ObjectModel;
using Origami.Core.Geometry;

namespace Origami.Core.Folding
{
    /// <summary>
    /// Immutable result of planning a fold or crease (C-103). When <see cref="Valid"/> is false only <see cref="Error"/>
    /// (and the echoed inputs) are meaningful: the lists are empty and <see cref="Result"/> is null.
    /// </summary>
    public sealed class FoldPlan
    {
        private static readonly IReadOnlyList<Facet> NoFacets = new ReadOnlyCollection<Facet>(new Facet[0]);
        private static readonly IReadOnlyList<CreaseSegment> NoCreases = new ReadOnlyCollection<CreaseSegment>(new CreaseSegment[0]);

        private FoldPlan(bool valid, FoldError error, bool isCrease, Vec2 grab, Vec2 drop, bool mountain, int layers,
            FoldLine line, bool partial, IReadOnlyList<Facet> moving, IReadOnlyList<Facet> stat, IReadOnlyList<Facet> moved,
            IReadOnlyList<CreaseSegment> newCreases, PaperState result)
        {
            Valid = valid;
            Error = error;
            IsCrease = isCrease;
            Grab = grab;
            Drop = drop;
            Mountain = mountain;
            Layers = layers;
            Line = line;
            Partial = partial;
            Moving = moving;
            Static = stat;
            Moved = moved;
            NewCreases = newCreases;
            Result = result;
        }

        public bool Valid { get; }
        public FoldError Error { get; }
        public bool IsCrease { get; }
        public Vec2 Grab { get; }
        public Vec2 Drop { get; }
        public bool Mountain { get; }
        public int Layers { get; }
        public FoldLine Line { get; }

        /// <summary>Fewer facets moved than lie on the grab side (Python <c>partial</c>).</summary>
        public bool Partial { get; }

        /// <summary>Moving parts before the fold (animation start).</summary>
        public IReadOnlyList<Facet> Moving { get; }

        /// <summary>Staying parts, layers before the fold.</summary>
        public IReadOnlyList<Facet> Static { get; }

        /// <summary>The moved parts after the fold (same objects as in <see cref="Result"/>). Empty for a crease.</summary>
        public IReadOnlyList<Facet> Moved { get; }

        /// <summary>Marks a crease left (empty for a fold).</summary>
        public IReadOnlyList<CreaseSegment> NewCreases { get; }

        /// <summary>New state, or null when invalid.</summary>
        public PaperState Result { get; }

        internal static FoldPlan Invalid(FoldError error, bool isCrease, Vec2 grab, Vec2 drop, bool mountain, int layers) =>
            new FoldPlan(false, error, isCrease, grab, drop, mountain, layers, default, false, NoFacets, NoFacets, NoFacets,
                NoCreases, null);

        internal static FoldPlan Create(bool isCrease, Vec2 grab, Vec2 drop, bool mountain, int layers, FoldLine line,
            bool partial, List<Facet> moving, List<Facet> stat, List<Facet> moved, List<CreaseSegment> newCreases,
            PaperState result) =>
            new FoldPlan(true, FoldError.None, isCrease, grab, drop, mountain, layers, line, partial,
                new ReadOnlyCollection<Facet>(moving.ToArray()), new ReadOnlyCollection<Facet>(stat.ToArray()),
                new ReadOnlyCollection<Facet>(moved.ToArray()), new ReadOnlyCollection<CreaseSegment>(newCreases.ToArray()),
                result);
    }
}
