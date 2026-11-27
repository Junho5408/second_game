namespace Origami.Core.Screens
{
    /// <summary>Screen layer drawn over the workshop (C-171). While it is not None, game commands are rejected (A2-11).</summary>
    public enum Overlay
    {
        None = 0,
        PauseMenu,
        ConfirmNewRun,
        Settlement,
        Result,
    }
}
