using Origami.Core.Folding;
using Origami.Core.Geometry;

namespace Origami.Core.Tutorial
{
    /// <summary>One tutorial guide target (C-140, D-115): the plane reference step <see cref="Number"/>. Immutable.</summary>
    public sealed class GuideStep
    {
        internal GuideStep(int number, FoldStepKind kind, Vec2 grab, Vec2 drop, Vec2 lineA, Vec2 lineB, bool eitherSide, string text)
        {
            Number = number;
            Kind = kind;
            Grab = grab;
            Drop = drop;
            LineA = lineA;
            LineB = lineB;
            EitherSide = eitherSide;
            Text = text;
        }

        /// <summary>1..6.</summary>
        public int Number { get; }

        /// <summary>Step 1 Crease, steps 2~6 Fold.</summary>
        public FoldStepKind Kind { get; }

        /// <summary>Target grab point (yellow ball).</summary>
        public Vec2 Grab { get; }

        /// <summary>Target drop point (green circle, guide magnet target).</summary>
        public Vec2 Drop { get; }

        /// <summary>Ideal crease ends = LineOnPaper(StateBeforeStep(plane, Number), Grab, Drop) (red dashed line).</summary>
        public Vec2 LineA { get; }

        /// <summary>See <see cref="LineA"/>.</summary>
        public Vec2 LineB { get; }

        /// <summary>Folding the other half over is equally correct (steps 1 and 6 only, A2-9).</summary>
        public bool EitherSide { get; }

        /// <summary>Plane StepTexts[Number − 1].</summary>
        public string Text { get; }
    }
}
