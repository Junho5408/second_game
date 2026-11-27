using System;

namespace Origami.Core.Catalog
{
    /// <summary>
    /// Book page layout (C-120, D-112, R-010, CONTENT §3): [0 contents][1 symbols] + per model in catalogue order
    /// [cover, step 1 … step N, complete], N = ActionCount. Spreads are (even, odd).
    /// </summary>
    public static class BookLayout
    {
        public const int ContentsPage = 0, SymbolsPage = 1;

        private static readonly int[] Covers = ComputeCovers();
        private static readonly int Pages = Covers[Covers.Length - 1] + ModelCatalog.Models[Covers.Length - 1].ActionCount + 2;

        /// <summary>2 + Σ(ActionCount + 2) = 44.</summary>
        public static int PageCount => Pages;

        /// <summary>Cover page of the model (plane 2, house 10, tulip 17, dog 24, cicada 32).</summary>
        /// <exception cref="ArgumentException">unknown or null ID.</exception>
        public static int CoverPage(string modelId)
        {
            int i = ModelCatalog.IndexOf(modelId);
            if (i < 0) throw new ArgumentException("unknown model ID " + (modelId ?? "null"), nameof(modelId));
            return Covers[i];
        }

        /// <exception cref="ArgumentOutOfRangeException">page not in 0..PageCount−1.</exception>
        public static BookPageKind KindOf(int page)
        {
            int m = Locate(page, out int offset);
            if (m < 0) return page == ContentsPage ? BookPageKind.Contents : BookPageKind.Symbols;
            if (offset == 0) return BookPageKind.Cover;
            return offset <= ModelCatalog.Models[m].ActionCount ? BookPageKind.Step : BookPageKind.Complete;
        }

        /// <summary>Model ID of the page; null for pages 0 and 1.</summary>
        /// <exception cref="ArgumentOutOfRangeException">page not in 0..PageCount−1.</exception>
        public static string ModelAt(int page)
        {
            int m = Locate(page, out _);
            return m < 0 ? null : ModelCatalog.Models[m].Id;
        }

        /// <summary>1-based step number of a Step page, else 0.</summary>
        /// <exception cref="ArgumentOutOfRangeException">page not in 0..PageCount−1.</exception>
        public static int StepAt(int page)
        {
            int m = Locate(page, out int offset);
            if (m < 0 || offset == 0 || offset > ModelCatalog.Models[m].ActionCount) return 0;
            return offset;
        }

        /// <summary>q = clamp(page, 0, PageCount − 1); returns q − q mod 2 (R-010 TurnPage).</summary>
        public static int ClampToSpread(int page)
        {
            int q = Math.Max(0, Math.Min(Pages - 1, page));
            return q - q % 2;
        }

        private static int Locate(int page, out int offset)
        {
            if (page < 0 || page >= Pages) throw new ArgumentOutOfRangeException(nameof(page));
            offset = 0;
            for (int i = Covers.Length - 1; i >= 0; i--)
            {
                if (page >= Covers[i])
                {
                    offset = page - Covers[i];
                    return i;
                }
            }
            return -1;
        }

        private static int[] ComputeCovers()
        {
            var models = ModelCatalog.Models;
            var covers = new int[models.Count];
            int p = 2;
            for (int i = 0; i < models.Count; i++)
            {
                covers[i] = p;
                p += models[i].ActionCount + 2;
            }
            return covers;
        }
    }
}
