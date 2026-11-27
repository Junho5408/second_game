using System;
using Origami.Core.Catalog;
using Origami.Core.Recognition;

namespace Origami.Core.Economy
{
    /// <summary>R-015 sale price (C-130). Pure functions, integer arithmetic (multiply, then floor-divide).</summary>
    public static class Pricing
    {
        /// <summary>★3 150, ★2 100, ★1 60.</summary>
        /// <exception cref="ArgumentOutOfRangeException"><see cref="Grade.None"/> or an undefined grade value.</exception>
        public static int GradePct(Grade grade)
        {
            switch (grade)
            {
                case Grade.Star3: return RuleConstants.GradePctStar3;
                case Grade.Star2: return RuleConstants.GradePctStar2;
                case Grade.Star1: return RuleConstants.GradePctStar1;
                default: throw new ArgumentOutOfRangeException(nameof(grade), grade, "no price for this grade");
            }
        }

        /// <summary>floor(basePrice × GradePct(grade) × (popular ? 150 : 100) / 10000). Max product 26 × 150 × 150 fits int.</summary>
        /// <exception cref="ArgumentOutOfRangeException">basePrice &lt; 0, or invalid grade.</exception>
        public static int SalePrice(int basePrice, Grade grade, bool popular)
        {
            if (basePrice < 0) throw new ArgumentOutOfRangeException(nameof(basePrice));
            int demand = popular ? RuleConstants.PopularPct : RuleConstants.NormalPct;
            return basePrice * GradePct(grade) * demand / RuleConstants.PriceDivisor;
        }

        /// <summary>
        /// Price of <paramref name="modelId"/> sold on <paramref name="day"/>; popular = <see cref="ModelCatalog.PopularModelId"/>(day)
        /// (Ordinal), whether or not the player owns that page.
        /// </summary>
        /// <exception cref="ArgumentException">unknown or null model ID.</exception>
        /// <exception cref="ArgumentOutOfRangeException">day not in 1..7, or invalid grade.</exception>
        public static int SalePrice(string modelId, Grade grade, int day)
        {
            var model = ModelCatalog.Find(modelId);
            if (model == null) throw new ArgumentException("unknown model ID " + (modelId ?? "null"), nameof(modelId));
            if (day < 1 || day > RuleConstants.TotalDays) throw new ArgumentOutOfRangeException(nameof(day));
            bool popular = string.Equals(ModelCatalog.PopularModelId(day), model.Id, StringComparison.Ordinal);
            return SalePrice(model.BasePrice, grade, popular);
        }
    }
}
