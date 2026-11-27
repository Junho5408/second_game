namespace Origami.Core.Economy
{
    /// <summary>
    /// Economy and run rule numbers (C-130, GAME_RULES "기본 수치"). The only place these values live; other
    /// modules hold their own constants (FoldPlanner, DropSnap, Grading, RoomLayout).
    /// </summary>
    public static class RuleConstants
    {
        /// <summary>TOTAL_DAYS, START_MONEY, PAPER_COST, DAILY_SHEETS (free tutorial sheet included).</summary>
        public const int TotalDays = 7, StartMoney = 20, PaperCost = 2, DailySheets = 5;

        /// <summary>GRADE_PCT (%) per grade.</summary>
        public const int GradePctStar3 = 150, GradePctStar2 = 100, GradePctStar1 = 60;

        /// <summary>Demand percent (popular / normal) and the price divisor (100 × 100).</summary>
        public const int PopularPct = 150, NormalPct = 100, PriceDivisor = 10000;

        /// <summary>R-018 thresholds: E-01 money ≤ 20, E-02 tulip total ≥ 12, E-04 money ≥ 600.</summary>
        public const int EmptyHandedMoney = 20, TulipGardenCount = 12, TycoonMoney = 600;
    }
}
