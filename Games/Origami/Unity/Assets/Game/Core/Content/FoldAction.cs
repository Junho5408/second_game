namespace Origami.Core.Content
{
    /// <summary>
    /// The five fold actions (GAME_RULES "접기 동작"). There is deliberately no zero member:
    /// <c>default(FoldAction)</c> and any other undefined value are invalid and rejected with
    /// <c>RejectReason.InvalidAction</c> (R-005).
    /// </summary>
    public enum FoldAction
    {
        U = 1,
        D = 2,
        L = 3,
        R = 4,
        F = 5,
    }
}
