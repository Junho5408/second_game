using System.Collections.Generic;
using System.Collections.ObjectModel;
using Origami.Core.Folding;
using Origami.Core.Geometry;

namespace Origami.Core.Book
{
    /// <summary>Diagram data of one step page (C-160, CONTENT §3), computed from the reference sequence. Immutable.</summary>
    public sealed class StepDiagram
    {
        internal StepDiagram(FoldStep step, PaperState before, PaperState after, int beforeFolds, int beforeCreases,
            Vec2 lineA, Vec2 lineB, SnapKind magnet, Vec2[] anchors, string text)
        {
            Step = step;
            Before = before;
            After = after;
            BeforeFolds = beforeFolds;
            BeforeCreases = beforeCreases;
            LineA = lineA;
            LineB = lineB;
            Magnet = magnet;
            Anchors = new ReadOnlyCollection<Vec2>((Vec2[])anchors.Clone());
            Text = text;
        }

        /// <summary>The reference action (grab → drop arrow, mountain, layers, kind).</summary>
        public FoldStep Step { get; }

        /// <summary>State right before the step, creases included (main picture).</summary>
        public PaperState Before { get; }

        /// <summary>State after the step (small picture at the bottom right).</summary>
        public PaperState After { get; }

        /// <summary>Fold steps before this one.</summary>
        public int BeforeFolds { get; }

        /// <summary>Crease steps before this one.</summary>
        public int BeforeCreases { get; }

        /// <summary>Fold line end points: <c>LineOnPaper.TryGet(Before, Grab, Drop)</c>.</summary>
        public Vec2 LineA { get; }
        public Vec2 LineB { get; }

        /// <summary><c>DropSnap.Snap(Before, Grab, Drop).Kind</c> for the exact book input.</summary>
        public SnapKind Magnet { get; }

        /// <summary>Point → [the point], Line → [front, back], otherwise empty (small circles of the diagram).</summary>
        public IReadOnlyList<Vec2> Anchors { get; }

        /// <summary>CONTENT §2 "책 설명".</summary>
        public string Text { get; }
    }
}
