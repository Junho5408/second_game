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
            throw new NotImplementedException();
        }

        /// <summary>0 → ★3, 1..2 → ★2, ≥ 3 → ★1. Negative penalty → ArgumentOutOfRangeException.</summary>
        public static Grade GradeFor(int penalty)
        {
            throw new NotImplementedException();
        }

        /// <summary>★3 150, ★2 100, ★1 60. Invalid grade → ArgumentOutOfRangeException.</summary>
        public static int GradePct(Grade grade)
        {
            throw new NotImplementedException();
        }
    }
}
