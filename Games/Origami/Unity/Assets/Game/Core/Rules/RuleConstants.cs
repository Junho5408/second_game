namespace Origami.Core.Rules
{
    /// <summary>
    /// All rule numbers from GAME_RULES.md "기본 수치" / "등급" and the R-014 table. Rule code must use these
    /// constants instead of literals. Values change only through a game-designer rule change.
    /// </summary>
    public static class RuleConstants
    {
        public const int TotalDays = 7;
        public const int StartMoney = 20;
        public const int DailyAttempts = 5;
        public const int MistakeLimit = 3;
        public const int MistakePenalty = 2;
        public const int HintPenalty = 1;
        public const int PopularPct = 150;
        public const int NormalPct = 100;
        public const int UnlockCount = 3;

        /// <summary>★3 requires penalty 0; ★2 is penalty 1..<see cref="Star2MaxPenalty"/>; ★1 above.</summary>
        public const int Star2MaxPenalty = 2;
        public const int GradePctStar3 = 150;
        public const int GradePctStar2 = 100;
        public const int GradePctStar1 = 60;

        /// <summary>Divisor of R-011: floor(base × gradePct × demandPct / 10000).</summary>
        public const int PriceDivisor = 10000;

        /// <summary>E-01: final money at or below this value (inclusive).</summary>
        public const int EmptyHandedMoney = 20;
        /// <summary>E-03: final money at or above this value (inclusive).</summary>
        public const int TycoonMoney = 1000;
        /// <summary>E-02: crane completions (any grade) at or above this value (inclusive).</summary>
        public const int CraneCount = 15;
    }
}
