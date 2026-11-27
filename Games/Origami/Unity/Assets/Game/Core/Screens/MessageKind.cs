namespace Origami.Core.Screens
{
    /// <summary>Kind of the latest toast message (C-171). Info is reserved: no ScreenFlow rule produces it yet.</summary>
    public enum MessageKind
    {
        None = 0,
        Reject,
        Recognition,
        Info,
    }
}
