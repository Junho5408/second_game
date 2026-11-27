namespace Origami.Core.Folding
{
    /// <summary>Which drop magnet fired (C-106, R-021). GAME_RULES notation: −, (tutorial), P, L, M.</summary>
    public enum SnapKind
    {
        None = 0,
        Guide = 1,
        Point = 2,
        Line = 3,
        Mark = 4,
    }
}
