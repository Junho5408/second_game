using System;
using Origami.Core.Content;

namespace Origami.Core.Rules
{
    /// <summary>R-009 grade rules. Pure functions.</summary>
    public static class Grading
    {
        /// <summary>Penalty = mistakes × <see cref="RuleConstants.MistakePenalty"/> + hints × <see cref="RuleConstants.HintPenalty"/>. Negative inputs → ArgumentOutOfRangeException.</summary>
        public static int Penalty(int mistakes, int hints)
        {
            if (mistakes < 0) throw new ArgumentOutOfRangeException(nameof(mistakes));
            if (hints < 0) throw new ArgumentOutOfRangeException(nameof(hints));
            return mistakes * RuleConstants.MistakePenalty + hints * RuleConstants.HintPenalty;
        }

        /// <summary>0 → ★3, 1..2 → ★2, ≥ 3 → ★1. Negative penalty → ArgumentOutOfRangeException.</summary>
        public static Grade GradeFor(int penalty)
        {
            if (penalty < 0) throw new ArgumentOutOfRangeException(nameof(penalty));
            if (penalty == 0) return Grade.Star3;
            return penalty <= RuleConstants.Star2MaxPenalty ? Grade.Star2 : Grade.Star1;
        }

        /// <summary>★3 150, ★2 100, ★1 60. Invalid grade → ArgumentOutOfRangeException.</summary>
        public static int GradePct(Grade grade)
        {
            switch (grade)
            {
                case Grade.Star3: return RuleConstants.GradePctStar3;
                case Grade.Star2: return RuleConstants.GradePctStar2;
                case Grade.Star1: return RuleConstants.GradePctStar1;
                default: throw new ArgumentOutOfRangeException(nameof(grade));
            }
        }
    }
}
