using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Origami.Core.Book;
using Origami.Core.Folding;

namespace Origami.Runtime.Book
{
    /// <summary>
    /// Fixed book wording (DECISIONS "책·대화상자 고정 문구", review ORIGAMI-011 W-2). The three CONTENT §3 sentences
    /// (<see cref="Sealed"/>, <see cref="CompletePrice"/>, the contents "보유" / "N원") are copied verbatim from the spec and
    /// checked as strings by the PlayMode tests. The other labels are presentation only. Numbers use the invariant culture.
    /// </summary>
    internal static class BookTexts
    {
        /// <summary>CONTENT §3: the only content of a sealed step/complete page.</summary>
        public const string Sealed = "봉인됨 — 표지에서 페이지를 사세요";

        /// <summary>CONTENT §3 contents entry of an owned model.</summary>
        public const string Owned = "보유";

        /// <summary>CONTENT §3 / F-008 buy button on an unowned cover.</summary>
        public const string Buy = "사기";

        public const string ContentsTitle = "목차";
        public const string SymbolsTitle = "접기 기호";
        public const string PrevButton = "← 앞";
        public const string NextButton = "뒤 →";

        /// <summary>Row labels of the page-1 symbol picture (CONTENT §3 "1쪽 접기 기호"), top to bottom.</summary>
        public static readonly IReadOnlyList<string> SymbolLabels = new[]
        {
            "골접기 — 긴 점선 (— — —)",
            "산접기 — 점-선 (—·—·)",
            "접었다 펴기 — 점선 + 왕복 화살표 (Ctrl을 누른 채 놓기)",
            "위 n겹만 — 화살표 꼬리에 \"n겹\" (끄는 중 휠)",
            "자국 선 — 가는 실선",
            "자석 기준점 — 작은 원",
        };

        /// <summary>CONTENT §3 complete page: "판매대에서 기본 N원".</summary>
        public static string CompletePrice(int basePrice) => "판매대에서 기본 " + Won(basePrice);

        /// <summary>"N원".</summary>
        public static string Won(int amount) => amount.ToString(CultureInfo.InvariantCulture) + "원";

        /// <summary>Contents entry status: "보유" or "N원" (CONTENT §3).</summary>
        public static string ContentsStatus(ContentsEntry e) => e.Owned ? Owned : Won(e.PagePrice);

        /// <summary>Contents button: "이름  보유  N쪽" / "이름  N원  N쪽".</summary>
        public static string ContentsLabel(ContentsEntry e) =>
            e.Name + "  " + ContentsStatus(e) + "  " + e.CoverPage.ToString(CultureInfo.InvariantCulture) + "쪽";

        public static string Stars(int n)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < n; i++) sb.Append('★');
            return sb.ToString();
        }

        public static string Difficulty(int n) => "난이도 " + Stars(n);
        public static string ActionCount(int n) => "동작 " + n.ToString(CultureInfo.InvariantCulture) + "번";
        public static string Techniques(string t) => "기법: " + (t ?? "");
        public static string BasePrice(int n) => "기본가 " + Won(n);
        public static string PagePrice(int n) => "페이지 가격 " + Won(n);
        public static string Money(int n) => "가진 돈 " + Won(n);
        public static string CompleteTitle(string name) => name + " 완성";
        public static string PageNumber(int page) => page.ToString(CultureInfo.InvariantCulture);

        /// <summary>"위 n겹" for top-n folds (also drawn in the diagram), "" for whole-stack folds.</summary>
        public static string LayerLabel(int layers) =>
            layers > 0 ? "위 " + layers.ToString(CultureInfo.InvariantCulture) + "겹" : "";

        /// <summary>Action summary of a step: "골접기 · 전체", "산접기 · 위 2겹", "접었다 펴기 · 전체".</summary>
        public static string ActionLine(FoldStep step)
        {
            string kind = step.Kind == FoldStepKind.Crease ? "접었다 펴기" : step.Mountain ? "산접기" : "골접기";
            return kind + " · " + (step.Layers > 0 ? LayerLabel(step.Layers) : "전체");
        }
    }
}
