using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Origami.Core.Catalog
{
    /// <summary>Interaction target IDs (C-120, R-004).</summary>
    public static class InteractTargets
    {
        public const string Table = "table", Bookshelf = "bookshelf", Counter = "counter", Bed = "bed";

        private static readonly IReadOnlyList<string> AllTargets =
            new ReadOnlyCollection<string>(new[] { Table, Bookshelf, Counter, Bed });

        /// <summary>table, bookshelf, counter, bed (R-004 tie order).</summary>
        public static IReadOnlyList<string> All => AllTargets;

        /// <summary>Ordinal membership; null → false.</summary>
        public static bool IsValid(string target)
        {
            if (target == null) return false;
            foreach (var t in AllTargets)
                if (string.Equals(t, target, StringComparison.Ordinal)) return true;
            return false;
        }
    }
}
