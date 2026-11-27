using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Origami.Core.Content
{
    /// <summary>Helpers for <see cref="FoldAction"/> (pure, no state).</summary>
    public static class FoldActions
    {
        /// <summary>The five valid actions in symbol order U, D, L, R, F.</summary>
        public static readonly IReadOnlyList<FoldAction> All =
            new ReadOnlyCollection<FoldAction>(new[] { FoldAction.U, FoldAction.D, FoldAction.L, FoldAction.R, FoldAction.F });

        /// <summary>True only for U/D/L/R/F; false for 0 and every other undefined value.</summary>
        public static bool IsValid(FoldAction action) => action >= FoldAction.U && action <= FoldAction.F;

        /// <summary>Spec symbol "U"/"D"/"L"/"R"/"F"; "?" for an invalid value.</summary>
        public static string Symbol(FoldAction action)
        {
            switch (action)
            {
                case FoldAction.U: return "U";
                case FoldAction.D: return "D";
                case FoldAction.L: return "L";
                case FoldAction.R: return "R";
                case FoldAction.F: return "F";
                default: return "?";
            }
        }
    }
}
