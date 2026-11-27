using System.Globalization;

namespace Origami.Core.Folding
{
    /// <summary>Korean texts of the folding table (C-108). <c>GameTexts</c> reuses these.</summary>
    public static class FoldTexts
    {
        public const string CreaseLabel = "접었다 펴기";

        /// <summary>Message for a fold error; None and InvalidLayers → "".</summary>
        public static string ErrorMessage(FoldError error)
        {
            switch (error)
            {
                case FoldError.DragTooShort: return "조금 더 끌어 주세요";
                case FoldError.NothingGrabbed: return "잡은 곳에 종이가 없어요";
                case FoldError.NothingToFold: return "접히는 부분이 없어요";
                case FoldError.LineOutsidePaper: return "접는 선이 종이 밖이에요";
                default: return "";
            }
        }

        /// <summary>layers 0 → "겹: 전체", n ≥ 1 → "겹: 위 {n} / {stackCount}".</summary>
        public static string LayerLabel(int layers, int stackCount) =>
            layers <= 0
                ? "겹: 전체"
                : "겹: 위 " + layers.ToString(CultureInfo.InvariantCulture) + " / " + stackCount.ToString(CultureInfo.InvariantCulture);
    }
}
