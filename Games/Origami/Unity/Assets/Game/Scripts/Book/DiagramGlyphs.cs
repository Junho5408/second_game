using System.Globalization;

namespace Origami.Runtime.Book
{
    /// <summary>
    /// Tiny built-in bitmap glyphs for the diagram label "위 n겹" (C-192 <see cref="DiagramRaster.Render"/>) so that the
    /// texture needs no font (fonts are not available in headless CI, A-5). Rows are top → bottom, '#' = ink.
    /// The panel also prints the same label as text next to the picture.
    /// </summary>
    internal static class DiagramGlyphs
    {
        public static readonly string[] Wi =
        {
            ".###..#.",
            "#...#.#.",
            "#...#.#.",
            ".###..#.",
            "......#.",
            "#####.#.",
            "..#...#.",
            "..#...#.",
        };

        public static readonly string[] Gyeop =
        {
            "###...#.",
            "..#..##.",
            "..#...#.",
            "..#..##.",
            "......#.",
            ".#....#.",
            ".######.",
            ".#....#.",
            ".######.",
        };

        private static readonly string[][] Digits =
        {
            new[] { "###", "#.#", "#.#", "#.#", "###" },
            new[] { ".#.", "##.", ".#.", ".#.", "###" },
            new[] { "###", "..#", "###", "#..", "###" },
            new[] { "###", "..#", "###", "..#", "###" },
            new[] { "#.#", "#.#", "###", "..#", "..#" },
            new[] { "###", "#..", "###", "..#", "###" },
            new[] { "###", "#..", "###", "#.#", "###" },
            new[] { "###", "..#", "..#", "..#", "..#" },
            new[] { "###", "#.#", "###", "#.#", "###" },
            new[] { "###", "#.#", "###", "..#", "###" },
        };

        /// <summary>Glyph rows of a decimal digit 0..9.</summary>
        public static string[] Digit(int d) => Digits[d];

        /// <summary>Decimal digits of a non-negative number (invariant culture).</summary>
        public static string DigitsOf(int n) => (n < 0 ? 0 : n).ToString(CultureInfo.InvariantCulture);
    }
}
