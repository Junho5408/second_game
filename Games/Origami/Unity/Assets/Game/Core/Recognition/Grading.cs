using System;

namespace Origami.Core.Recognition
{
    /// <summary>Match percent and grade thresholds (C-110, R-011/R-012). Grades are decided from the percent only.</summary>
    public static class Grading
    {
        public const int MatchMin = 85, Grade2Min = 93, Grade3Min = 97;

        private const double PercentEps = 1e-9;

        /// <summary>floor(score × 100 + 1e-9).</summary>
        public static int Percent(double score) => (int)Math.Floor(score * 100 + PercentEps);

        /// <summary>≥ 97 ★3, ≥ 93 ★2, ≥ 85 ★1, else None.</summary>
        public static Grade FromPercent(int percent) =>
            percent >= Grade3Min ? Grade.Star3 : percent >= Grade2Min ? Grade.Star2 : percent >= MatchMin ? Grade.Star1 : Grade.None;

        /// <summary>FromPercent(Percent(score)).</summary>
        public static Grade FromScore(double score) => FromPercent(Percent(score));
    }
}
