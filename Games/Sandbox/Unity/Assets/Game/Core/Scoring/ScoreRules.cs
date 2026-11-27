namespace Sandbox.Core.Scoring
{
    /// <summary>
    /// Tuning values and pure rule functions for F-001 (GAME_RULES.md "기본 수치", R-002, R-003).
    /// Contract: Docs/tech/INTERFACE_CONTRACTS.md C-002.
    /// </summary>
    public static class ScoreRules
    {
        /// <summary>BASE_POINTS: points per valid press before the multiplier (R-002).</summary>
        public const int BasePoints = 1;

        /// <summary>COMBO_WINDOW in seconds. A gap &lt;= this keeps the combo (R-003, R-004).</summary>
        public const double ComboWindowSeconds = 1.0;

        /// <summary>Multiplier cap (x4 for combo 20+).</summary>
        public const int MaxMultiplier = 4;

        /// <summary>
        /// Multiplier for a combo count: combo &lt;= 4 (including 0 and negatives) → 1,
        /// 5..9 → 2, 10..19 → 3, 20+ → 4. Pure, never throws.
        /// </summary>
        public static int MultiplierForCombo(int combo)
        {
            if (combo >= 20) return MaxMultiplier;
            if (combo >= 10) return 3;
            if (combo >= 5) return 2;
            return 1;
        }
    }
}
