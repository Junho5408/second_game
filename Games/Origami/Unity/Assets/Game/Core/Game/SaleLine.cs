using Origami.Core.Recognition;

namespace Origami.Core.Game
{
    /// <summary>One sold piece of the evening settlement (C-151, D-123). Immutable.</summary>
    public sealed class SaleLine
    {
        internal SaleLine(string modelId, Grade grade, bool popular, int price)
        {
            ModelId = modelId;
            Grade = grade;
            Popular = popular;
            Price = price;
        }

        public string ModelId { get; }
        public Grade Grade { get; }

        /// <summary>The model was the popular model of the sale day.</summary>
        public bool Popular { get; }

        /// <summary><c>Pricing.SalePrice(ModelId, Grade, sale day)</c>.</summary>
        public int Price { get; }
    }
}
