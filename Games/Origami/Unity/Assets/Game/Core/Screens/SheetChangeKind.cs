namespace Origami.Core.Screens
{
    /// <summary>What happened to the sheet on the table (C-171).</summary>
    public enum SheetChangeKind
    {
        None = 0,
        Taken,
        Folded,
        Creased,
        Flipped,
        Undone,
        Unfolded,
        Removed,
        Reset,
    }
}
