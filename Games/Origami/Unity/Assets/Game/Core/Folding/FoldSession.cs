using System.Collections.Generic;
using System.Collections.ObjectModel;
using Origami.Core.Geometry;

namespace Origami.Core.Folding
{
    /// <summary>
    /// One sheet on the table: current state, undo history and counts (C-104, R-007, D-104).
    /// Replaying <see cref="AppliedSteps"/> from <see cref="PaperState.Initial"/> gives <see cref="State"/>.
    /// </summary>
    public sealed class FoldSession
    {
        private readonly List<PaperState> _before = new List<PaperState>();
        private readonly List<FoldStep> _steps = new List<FoldStep>();

        public FoldSession()
        {
            State = PaperState.Initial;
        }

        public PaperState State { get; private set; }

        /// <summary>Folds applied to the current state (R-007 "접은 횟수").</summary>
        public int FoldCount { get; private set; }

        /// <summary>Creases applied (R-007 "자국 수"; one per action even with several segments).</summary>
        public int CreaseCount { get; private set; }

        /// <summary>FoldCount + CreaseCount (flips excluded; R-008 guide step).</summary>
        public int ActionCount => FoldCount + CreaseCount;

        /// <summary>History is not empty (Undo/Unfold allowed).</summary>
        public bool CanUndo => _steps.Count > 0;

        /// <summary>Applied actions in order (read-only copy).</summary>
        public IReadOnlyList<FoldStep> AppliedSteps => new ReadOnlyCollection<FoldStep>(_steps.ToArray());

        /// <summary>Plans and, if valid, applies a fold. Invalid → state unchanged.</summary>
        public FoldPlan Fold(Vec2 grab, Vec2 drop, bool mountain, int layers)
        {
            var plan = FoldPlanner.PlanFold(State, grab, drop, mountain, layers);
            if (plan.Valid)
            {
                Push(FoldStep.Fold(grab, drop, mountain, layers), plan.Result);
                FoldCount++;
            }
            return plan;
        }

        /// <summary>Plans and, if valid, applies a crease. Invalid → state unchanged.</summary>
        public FoldPlan Crease(Vec2 grab, Vec2 drop, int layers)
        {
            var plan = FoldPlanner.PlanCrease(State, grab, drop, layers);
            if (plan.Valid)
            {
                Push(FoldStep.Crease(grab, drop, layers), plan.Result);
                CreaseCount++;
            }
            return plan;
        }

        /// <summary>Turns the sheet over (always applied, recorded).</summary>
        public void Flip()
        {
            Push(FoldStep.Flip(), FoldPlanner.Flip(State));
        }

        /// <summary>Cancels the last Fold/Crease/Flip. False (no change) when the history is empty.</summary>
        public bool Undo()
        {
            int n = _steps.Count;
            if (n == 0) return false;
            var step = _steps[n - 1];
            State = _before[n - 1];
            _steps.RemoveAt(n - 1);
            _before.RemoveAt(n - 1);
            if (step.Kind == FoldStepKind.Fold) FoldCount--;
            else if (step.Kind == FoldStepKind.Crease) CreaseCount--;
            return true;
        }

        /// <summary>Back to <see cref="PaperState.Initial"/>, counts 0, history cleared. False when the history is empty.</summary>
        public bool Unfold()
        {
            if (_steps.Count == 0) return false;
            _steps.Clear();
            _before.Clear();
            State = PaperState.Initial;
            FoldCount = 0;
            CreaseCount = 0;
            return true;
        }

        private void Push(FoldStep step, PaperState next)
        {
            _before.Add(State);
            _steps.Add(step);
            State = next;
        }
    }
}
