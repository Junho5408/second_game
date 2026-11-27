namespace Sandbox.Core.Scoring
{
    /// <summary>
    /// F-001 score counter (rapid-press combo). Engine-free and deterministic: it never reads a clock;
    /// every press and query receives the time in seconds from the caller (R-001).
    /// Not thread-safe; one owner calls it from one thread. Contract: INTERFACE_CONTRACTS.md C-001.
    /// </summary>
    public sealed class ScoreCounter
    {
        private int _score;
        private int _combo;          // combo right after the last valid press
        private bool _hasLastPress;
        private double _lastPress;   // time of the last valid press; meaningful only when _hasLastPress

        /// <summary>Creates a counter in the initial state (score 0, combo 0, no previous press). AC-01.</summary>
        public ScoreCounter()
        {
        }

        /// <summary>
        /// Registers one press at <paramref name="time"/> seconds (R-002, R-003).
        /// Valid when <paramref name="time"/> is finite and either there is no previous press or
        /// time &gt;= the previous valid press time. Invalid presses (NaN, ±Infinity, time going backwards)
        /// change nothing. Never throws.
        /// </summary>
        /// <returns>true if the press was applied; false if it was ignored.</returns>
        public bool Press(double time)
        {
            if (!IsFinite(time)) return false;
            if (_hasLastPress && time < _lastPress) return false;

            _combo = _hasLastPress && time - _lastPress <= ScoreRules.ComboWindowSeconds ? _combo + 1 : 1;
            _score += ScoreRules.BasePoints * ScoreRules.MultiplierForCombo(_combo);
            _lastPress = time;
            _hasLastPress = true;
            return true;
        }

        /// <summary>
        /// Reports the state as seen at <paramref name="now"/> seconds without changing it (R-004, R-001).
        /// Finite now &gt; lastPress + ComboWindowSeconds → combo 0, multiplier 1, score kept.
        /// now &lt;= that (including now &lt; lastPress), NaN or ±Infinity → state right after the last valid press.
        /// No valid press since creation/reset → <see cref="ScoreSnapshot.Initial"/> for any now. Never throws.
        /// </summary>
        public ScoreSnapshot GetState(double now)
        {
            if (!_hasLastPress) return ScoreSnapshot.Initial;
            // +Infinity is never "expired" (R-001): the expiry check applies to finite now only.
            if (IsFinite(now) && now - _lastPress > ScoreRules.ComboWindowSeconds)
                return new ScoreSnapshot(_score, 0, ScoreRules.MultiplierForCombo(0));
            return new ScoreSnapshot(_score, _combo, ScoreRules.MultiplierForCombo(_combo));
        }

        /// <summary>
        /// Returns to the initial state: score 0, combo 0, previous press forgotten (R-005). Takes no time;
        /// the next valid press is a first press at any finite time. Idempotent, never throws.
        /// </summary>
        public void Reset()
        {
            _score = 0;
            _combo = 0;
            _lastPress = 0.0;
            _hasLastPress = false;
        }

        private static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
