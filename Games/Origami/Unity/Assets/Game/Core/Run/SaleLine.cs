using Origami.Core.Content;

namespace Origami.Core.Run
{
    /// <summary>One sold piece in a settlement (R-011/R-012). Immutable value type.</summary>
    public readonly struct SaleLine
    {
        public SaleLine(string modelId, Grade grade, bool popular, int price)
        {
            ModelId = modelId;
            Grade = grade;
            Popular = popular;
            Price = price;
        }

        public string ModelId { get; }
        public Grade Grade { get; }

        /// <summary>True if the model was the popular model of the settled day.</summary>
        public bool Popular { get; }

        public int Price { get; }
    }
}
