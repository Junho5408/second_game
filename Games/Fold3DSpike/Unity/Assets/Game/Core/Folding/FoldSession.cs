using System.Collections.Generic;

namespace Fold3DSpike.Core.Folding
{
    /// <summary>
    /// The playable state: current paper, undo history and guided paper-plane progress with per-step scores.
    /// Folds beyond the last guided step are allowed (free folding, not scored).
    /// </summary>
    public sealed class FoldSession
    {
        private sealed class Snapshot
        {
            public PaperState State;
            public int StepIndex;
            public int ScoreCount;
            public int FoldCount;
        }

        private readonly Stack<Snapshot> _history = new Stack<Snapshot>();
        private readonly List<StepScore> _scores = new List<StepScore>();

        public FoldSession()
        {
            Restart();
        }

        public PaperState State { get; private set; }
        public StepTarget[] Steps => PaperPlaneGuide.Steps;
        /// <summary>Index of the next guided step (== Steps.Length when the guide is done).</summary>
        public int StepIndex { get; private set; }
        public IReadOnlyList<StepScore> Scores => _scores;
        public StepScore LastScore { get; private set; }
        public int FoldCount { get; private set; }
        public bool CanUndo => _history.Count > 0;
        public bool GuideComplete => StepIndex >= Steps.Length;
        public StepTarget CurrentStep => GuideComplete ? null : Steps[StepIndex];

        /// <summary>Average of all step scores (0..100), 0 if none yet.</summary>
        public int TotalScore
        {
            get
            {
                if (_scores.Count == 0) return 0;
                int sum = 0;
                foreach (var s in _scores) sum += s.Score;
                return (int)System.Math.Round(sum / (double)_scores.Count);
            }
        }

        public void Restart()
        {
            _history.Clear();
            _scores.Clear();
            State = PaperState.CreateSquare(PaperPlaneGuide.Half, false); // white side up, colour underneath
            StepIndex = 0;
            FoldCount = 0;
            LastScore = null;
        }

        public FoldPlan Preview(Vec2 grab, Vec2 drop, bool mountain) => FoldPlanner.Plan(State, grab, drop, mountain);

        /// <summary>Plans and applies a fold. Returns the plan (check <see cref="FoldPlan.Valid"/>).</summary>
        public FoldPlan Fold(Vec2 grab, Vec2 drop, bool mountain)
        {
            var plan = Preview(grab, drop, mountain);
            Commit(plan);
            return plan;
        }

        /// <summary>Applies a plan made from the current state. Returns false (and changes nothing) if invalid.</summary>
        public bool Commit(FoldPlan plan)
        {
            if (plan == null || !plan.Valid || plan.Result == null) return false;
            Push();
            State = plan.Result;
            FoldCount++;
            if (!GuideComplete)
            {
                LastScore = FoldScoring.Evaluate(Steps[StepIndex], plan.Line, plan.Grab, plan.Drop);
                _scores.Add(LastScore);
                StepIndex++;
            }
            else
            {
                LastScore = null;
            }
            return true;
        }

        /// <summary>Turns the paper over left-to-right around the vertical line x = centre of its bounds.</summary>
        public double Flip()
        {
            double cx = FlipAxisX();
            Push();
            int max = State.MaxLayer();
            var next = new PaperState();
            foreach (var f in State.Facets)
            {
                var w = new List<Vec2>(f.World.Count);
                var p = new List<Vec2>(f.World.Count);
                for (int i = f.World.Count - 1; i >= 0; i--)
                {
                    w.Add(new Vec2(2 * cx - f.World[i].X, f.World[i].Y));
                    p.Add(f.Paper[i]);
                }
                next.Facets.Add(new Facet(w, p, max - f.Layer, !f.FrontUp));
            }
            State = next;
            return cx;
        }

        /// <summary>X of the vertical line <see cref="Flip"/> turns the paper around (centre of its bounds).</summary>
        public double FlipAxisX()
        {
            double minX = double.MaxValue, maxX = double.MinValue;
            foreach (var f in State.Facets)
            foreach (var v in f.World)
            {
                if (v.X < minX) minX = v.X;
                if (v.X > maxX) maxX = v.X;
            }
            return minX > maxX ? 0 : 0.5 * (minX + maxX);
        }

        public bool Undo()
        {
            if (_history.Count == 0) return false;
            var s = _history.Pop();
            State = s.State;
            StepIndex = s.StepIndex;
            while (_scores.Count > s.ScoreCount) _scores.RemoveAt(_scores.Count - 1);
            LastScore = _scores.Count > 0 ? _scores[_scores.Count - 1] : null;
            FoldCount = s.FoldCount;
            return true;
        }

        private void Push()
        {
            _history.Push(new Snapshot { State = State, StepIndex = StepIndex, ScoreCount = _scores.Count, FoldCount = FoldCount });
        }
    }
}
