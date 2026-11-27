using System;

namespace Sandbox.Core.Scoring
{
    /// <summary>
    /// Immutable view of the counter at one query time (F-001: score, combo, multiplier).
    /// Plain value type: no behavior beyond equality. Contract: INTERFACE_CONTRACTS.md C-003.
    /// </summary>
    public readonly struct ScoreSnapshot : IEquatable<ScoreSnapshot>
    {
        /// <summary>State of a new or reset counter: score 0, combo 0, multiplier 1.</summary>
        public static readonly ScoreSnapshot Initial = new ScoreSnapshot(0, 0, 1);

        public ScoreSnapshot(int score, int combo, int multiplier)
        {
            Score = score;
            Combo = combo;
            Multiplier = multiplier;
        }

        /// <summary>Total score, &gt;= 0 (R-006).</summary>
        public int Score { get; }

        /// <summary>Reported combo count; 0 when there is no press or the combo has expired (R-004).</summary>
        public int Combo { get; }

        /// <summary>Reported multiplier, 1..4; 1 whenever Combo is 0.</summary>
        public int Multiplier { get; }

        public bool Equals(ScoreSnapshot other) =>
            Score == other.Score && Combo == other.Combo && Multiplier == other.Multiplier;

        public override bool Equals(object obj) => obj is ScoreSnapshot other && Equals(other);

        public override int GetHashCode() => (Score * 397 ^ Combo) * 397 ^ Multiplier;

        public static bool operator ==(ScoreSnapshot left, ScoreSnapshot right) => left.Equals(right);

        public static bool operator !=(ScoreSnapshot left, ScoreSnapshot right) => !left.Equals(right);

        public override string ToString() => $"Score={Score} Combo={Combo} x{Multiplier}";
    }
}
