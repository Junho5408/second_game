using Origami.Core.Folding;
using Origami.Core.Geometry;

namespace Origami.Runtime.Folding
{
    /// <summary>
    /// Connection of the <see cref="FoldTable"/> to the game rules (C-191, A2-7). The table never calls
    /// <c>ScreenFlow</c>/<c>OrigamiGame</c> directly; the game implements this with <c>FoldHostAdapter</c> (task 014),
    /// tests with a fake over <see cref="FoldSession"/>.
    /// </summary>
    public interface IFoldTableHost
    {
        /// <summary>The sheet on the table, null if none.</summary>
        PaperState Sheet { get; }

        /// <summary>False → the table ignores the pointer and cancels a drag (not seated, overlay open).</summary>
        bool InputEnabled { get; }

        /// <summary>True only while the tutorial guide is visible.</summary>
        bool TryGetGuide(out FoldTableGuide guide);

        /// <summary>Increases on every sheet change.</summary>
        int ChangeSerial { get; }

        /// <summary>Kind of the last change.</summary>
        FoldTableChangeKind ChangeKind { get; }

        /// <summary>State right before the last change (flip animation), null if none.</summary>
        PaperState ChangePrevious { get; }

        /// <summary>
        /// Applies a fold with exactly these arguments (the table passes the preview's arguments). Returns false if refused;
        /// <paramref name="error"/> is the fold reason, or <see cref="FoldError.None"/> for a rule rejection that is not a
        /// fold error (the host reports that itself).
        /// </summary>
        bool Fold(Vec2 grab, Vec2 drop, bool mountain, int layers, out FoldError error);

        /// <summary>Applies a crease (fold and unfold); same conventions as <see cref="Fold"/>.</summary>
        bool Crease(Vec2 grab, Vec2 drop, int layers, out FoldError error);
    }
}
