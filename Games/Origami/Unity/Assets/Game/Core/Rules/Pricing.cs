using System;
using Origami.Core.Content;

namespace Origami.Core.Rules
{
    /// <summary>R-011 sale price. Pure functions, integer arithmetic.</summary>
    public static class Pricing
    {
        /// <summary>150 if popular, else 100.</summary>
        public static int DemandPct(bool popular)
        {
            return popular ? RuleConstants.PopularPct : RuleConstants.NormalPct;
        }

        /// <summary>floor(basePrice × GradePct(grade) × DemandPct(popular) / 10000). Invalid grade → ArgumentOutOfRangeException.</summary>
        public static int SalePrice(int basePrice, Grade grade, bool popular)
        {
            return basePrice * Grading.GradePct(grade) * DemandPct(popular) / RuleConstants.PriceDivisor;
        }

        /// <summary>
        /// Price of <paramref name="modelId"/> sold on <paramref name="day"/> (popular = that day's catalog entry).
        /// Unknown model → ArgumentException; day outside 1..7 → ArgumentOutOfRangeException.
        /// </summary>
        public static int SalePrice(string modelId, Grade grade, int day)
        {
            var model = OrigamiCatalog.FindModel(modelId);
            if (model == null) throw new ArgumentException("Unknown model ID: " + (modelId ?? "null"), nameof(modelId));
            if (day < 1 || day > RuleConstants.TotalDays) throw new ArgumentOutOfRangeException(nameof(day));
            bool popular = string.Equals(OrigamiCatalog.PopularModelId(day), model.Id, StringComparison.Ordinal);
            return SalePrice(model.BasePrice, grade, popular);
        }
    }
}
