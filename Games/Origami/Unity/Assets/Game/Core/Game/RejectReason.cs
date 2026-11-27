namespace Origami.Core.Game
{
    /// <summary>Why a command was rejected (C-151, R-019). <see cref="None"/> for accepted commands.</summary>
    public enum RejectReason
    {
        None = 0,
        RunEnded,
        WrongMode,
        UnknownTarget,
        SheetOnTable,
        NoSheetsLeft,
        NotEnoughMoney,
        NoSheet,
        InvalidFold,
        NothingToUndo,
        NoFolds,
        NoGuide,
        UnknownModel,
        AlreadyOwned,
    }
}
