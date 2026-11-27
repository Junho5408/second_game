namespace Origami.Runtime.Folding
{
    /// <summary>Kind of the host's last sheet change (C-191). Folded/Creased/Flipped animate, Replaced redraws at once.</summary>
    public enum FoldTableChangeKind
    {
        None = 0,
        Replaced,
        Folded,
        Creased,
        Flipped,
    }
}
