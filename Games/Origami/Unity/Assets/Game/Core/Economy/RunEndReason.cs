namespace Origami.Core.Economy
{
    /// <summary>Why the run ended (C-130). Same values as v1.</summary>
    public enum RunEndReason
    {
        /// <summary>Run in progress.</summary>
        None = 0,
        /// <summary>Slept on day 7 (R-016).</summary>
        Completed = 1,
        /// <summary>Money &lt; paper cost at the start of a new day (R-017).</summary>
        Bankrupt = 2,
    }
}
