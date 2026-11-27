namespace Origami.Core.Run
{
    /// <summary>
    /// Why a command was rejected (R-015). A rejected command never changes state.
    /// Check orders: Start RunEnded → FoldInProgress → UnknownModel → Locked → NoAttemptsLeft → NotEnoughMoney (R-004);
    /// Fold RunEnded → NoFold → InvalidAction (R-005); Hint/Abandon RunEnded → NoFold; EndDay RunEnded → FoldInProgress.
    /// </summary>
    public enum RejectReason
    {
        /// <summary>Not rejected.</summary>
        None = 0,
        RunEnded = 1,
        FoldInProgress = 2,
        UnknownModel = 3,
        Locked = 4,
        NoAttemptsLeft = 5,
        NotEnoughMoney = 6,
        NoFold = 7,
        InvalidAction = 8,
    }
}
