namespace Origami.Core.Rules
{
    /// <summary>Why a run ended (R-012, R-013). Lives in Rules because ending evaluation (R-014) takes it as input.</summary>
    public enum RunEndReason
    {
        /// <summary>Run still in progress.</summary>
        None = 0,
        /// <summary>Day-7 EndDay (완주).</summary>
        Completed = 1,
        /// <summary>Money 0 at the start of a new day (파산).</summary>
        Bankrupt = 2,
    }
}
