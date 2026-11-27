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
            throw new NotImplementedException();
        }

        /// <summary>floor(basePrice × GradePct(grade) × DemandPct(popular) / 10000). Invalid grade → ArgumentOutOfRangeException.</summary>
        public static int SalePrice(int basePrice, Grade grade, bool popular)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Price of <paramref name="modelId"/> sold on <paramref name="day"/> (popular = that day's catalog entry).
        /// Unknown model → ArgumentException; day outside 1..7 → ArgumentOutOfRangeException.
        /// </summary>
        public static int SalePrice(string modelId, Grade grade, int day)
        {
            throw new NotImplementedException();
        }
    }
}
