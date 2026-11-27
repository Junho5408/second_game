using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using Origami.Core.Catalog;
using Origami.Core.Economy;
using Origami.Core.Folding;
using Origami.Core.Game;
using Origami.Core.Recognition;
using Origami.Core.Tutorial;

namespace Origami.Core.Texts
{
    /// <summary>
    /// All Korean display texts of the workshop (C-170; sources FEATURE_SPECS F-013, R-012, CONTENT §6·§8). Pure functions.
    /// Numbers use <see cref="CultureInfo.InvariantCulture"/> without separators. Grades are written "★n" (n = 1..3).
    /// </summary>
    /// <remarks>
    /// Line formats fixed by ORIGAMI-011 (C-170 leaves them to this task; tests pin them):
    /// <list type="bullet">
    /// <item>Settlement: "N일차 정산", per sale "이름 ★n P원" (+ " (인기)"), or "팔린 작품이 없어요", "합계 N원", "정산 후 돈 N원",
    /// then "내일의 인기: 이름" / "7일이 모두 지났어요" (completed) / "돈이 모자라 공방 문을 닫았어요" (bankrupt).</item>
    /// <item>Counter: "N일차 판매대", "오늘의 인기: 이름", per piece "이름 ★n 일치도 xx% · 오늘 팔면 P원" or "진열된 작품이 없어요", "합계 예상 N원".</item>
    /// <item>Result: ending name, ending text (only when the run has an ending), "최종 돈 N원", per model "이름: ★3 a · ★2 b · ★1 c",
    /// "실패 N", "총 판매액 N원", "총 종이값 N원", "총 책값 N원".</item>
    /// </list>
    /// </remarks>
    public static class GameTexts
    {
        public const string WalkHint = "WASD/방향키로 걸어 작업 테이블로 가세요";
        public const string TakeSheetHint = "Space: 종이 꺼내기";
        public const string PostTutorialHint = "이제부터 접는 법은 책에서만 볼 수 있어요. 앉아서 B 또는 책장에서 E";

        private const string CreaseGuideSuffix = " — Ctrl을 누른 채 놓으세요";

        /// <summary>Catalogue display name; "" for null/unknown IDs.</summary>
        public static string ModelName(string modelId)
        {
            var m = ModelCatalog.Find(modelId);
            return m == null ? "" : m.DisplayName;
        }

        /// <summary>F-013 rejection text (shown 2 s); "" for reasons that are silently ignored. InvalidFold → <see cref="FoldTexts.ErrorMessage"/>.</summary>
        public static string RejectMessage(RejectReason reason, FoldError foldError)
        {
            switch (reason)
            {
                case RejectReason.SheetOnTable: return "테이블 위 종이를 먼저 완성하거나 버리세요";
                case RejectReason.NoSheetsLeft: return "오늘은 종이를 다 썼어요. 침대에서 쉬세요";
                case RejectReason.NotEnoughMoney: return "돈이 모자라요";
                case RejectReason.NoFolds: return "아직 접지 않았어요";
                case RejectReason.NothingToUndo: return "되돌릴 것이 없어요";
                case RejectReason.AlreadyOwned: return "이미 가진 페이지예요";
                case RejectReason.InvalidFold: return FoldTexts.ErrorMessage(foldError);
                default: return "";
            }
        }

        /// <summary>HUD of the snapshot (F-013).</summary>
        /// <exception cref="ArgumentNullException">s is null.</exception>
        public static HudData Hud(GameSnapshot s)
        {
            if (s == null) throw new ArgumentNullException(nameof(s));
            string popular = ModelName(s.PopularModelId);
            var lines = new List<string>
            {
                Num(s.Day) + "일차 / " + Num(RuleConstants.TotalDays),
                "돈 " + Won(s.Money),
                "오늘 남은 종이 " + Num(s.SheetsLeft) + " / " + Num(RuleConstants.DailySheets),
                "오늘의 인기: " + popular,
            };
            if (s.Mode == WorkshopMode.Seated && s.Sheet != null)
                lines.Add("접은 횟수 " + Num(s.Sheet.FoldCount) + " · 자국 " + Num(s.Sheet.CreaseCount));
            return new HudData(s.Day, s.Money, s.SheetsLeft, popular, lines);
        }

        /// <summary>"E: 앉기" / "E: 책 읽기" / "E: 판매대 보기" / "E: 잠자기" (CONTENT §6); "" for anything else (Ordinal).</summary>
        public static string InteractPrompt(string target)
        {
            switch (target)
            {
                case InteractTargets.Table: return "E: 앉기";
                case InteractTargets.Bookshelf: return "E: 책 읽기";
                case InteractTargets.Counter: return "E: 판매대 보기";
                case InteractTargets.Bed: return "E: 잠자기";
                default: return "";
            }
        }

        /// <summary>R-012: "이름 ★n (일치도 xx%)" or "구겨진 종이 (가장 비슷한 작품: 이름 xx%)" ("구겨진 종이" when there is no candidate).</summary>
        /// <exception cref="ArgumentNullException">r is null.</exception>
        public static string RecognitionLine(RecognitionResult r)
        {
            if (r == null) throw new ArgumentNullException(nameof(r));
            if (r.IsPiece) return ModelName(r.ModelId) + " " + Stars(r.Grade) + " (일치도 " + Num(r.Percent) + "%)";
            if (r.BestModelId == null) return "구겨진 종이";
            return "구겨진 종이 (가장 비슷한 작품: " + ModelName(r.BestModelId) + " " + Num(r.Percent) + "%)";
        }

        /// <summary>F-006 step title "종이비행기 n / 6" (+ " — Ctrl을 누른 채 놓으세요" for a crease step).</summary>
        /// <exception cref="ArgumentNullException">step is null.</exception>
        public static string GuideTitle(GuideStep step)
        {
            if (step == null) throw new ArgumentNullException(nameof(step));
            string title = ModelName(ModelIds.Plane) + " " + Num(step.Number) + " / " + Num(PlaneGuide.StepCount);
            return step.Kind == FoldStepKind.Crease ? title + CreaseGuideSuffix : title;
        }

        /// <summary>Settlement screen lines (R-016, F-011 AC-06). Format: class remarks.</summary>
        /// <exception cref="ArgumentNullException">r is null.</exception>
        public static IReadOnlyList<string> SettlementLines(SettlementResult r)
        {
            if (r == null) throw new ArgumentNullException(nameof(r));
            var lines = new List<string> { Num(r.SettledDay) + "일차 정산" };
            foreach (var sale in r.Lines)
                lines.Add(ModelName(sale.ModelId) + " " + Stars(sale.Grade) + " " + Won(sale.Price) + (sale.Popular ? " (인기)" : ""));
            if (r.Lines.Count == 0) lines.Add("팔린 작품이 없어요");
            lines.Add("합계 " + Won(r.Total));
            lines.Add("정산 후 돈 " + Won(r.MoneyAfter));
            switch (r.EndReason)
            {
                case RunEndReason.Completed: lines.Add("7일이 모두 지났어요"); break;
                case RunEndReason.Bankrupt: lines.Add("돈이 모자라 공방 문을 닫았어요"); break;
                default: lines.Add("내일의 인기: " + ModelName(r.NextPopularModelId)); break;
            }
            return ReadOnly(lines);
        }

        /// <summary>Counter screen lines (CONTENT §6, F-011 AC-07): today's prices of the display (R-015). Format: class remarks.</summary>
        /// <exception cref="ArgumentNullException">s is null.</exception>
        public static IReadOnlyList<string> CounterLines(GameSnapshot s)
        {
            if (s == null) throw new ArgumentNullException(nameof(s));
            var lines = new List<string> { Num(s.Day) + "일차 판매대", "오늘의 인기: " + ModelName(s.PopularModelId) };
            int total = 0;
            foreach (var piece in s.Display)
            {
                int price = Pricing.SalePrice(piece.ModelId, piece.Grade, s.Day);
                total += price;
                lines.Add(ModelName(piece.ModelId) + " " + Stars(piece.Grade) + " 일치도 " + Num(piece.Percent) + "% · 오늘 팔면 " + Won(price));
            }
            if (s.Display.Count == 0) lines.Add("진열된 작품이 없어요");
            lines.Add("합계 예상 " + Won(total));
            return ReadOnly(lines);
        }

        /// <summary>Result screen lines (R-020, F-012, CONTENT §8). The two ending lines appear only when the run has an ending.</summary>
        /// <exception cref="ArgumentNullException">s is null.</exception>
        public static IReadOnlyList<string> ResultLines(GameSnapshot s)
        {
            if (s == null) throw new ArgumentNullException(nameof(s));
            var lines = new List<string>();
            var ending = s.EndingId == null ? null : EndingCatalog.Find(s.EndingId);
            if (ending != null)
            {
                lines.Add(ending.DisplayName);
                lines.Add(ending.Text);
            }
            lines.Add("최종 돈 " + Won(s.Money));
            foreach (var m in ModelCatalog.Models)
            {
                var c = s.Completions;
                lines.Add(m.DisplayName + ": ★3 " + Num(c.Get(m.Id, Grade.Star3)) + " · ★2 " + Num(c.Get(m.Id, Grade.Star2)) +
                          " · ★1 " + Num(c.Get(m.Id, Grade.Star1)));
            }
            lines.Add("실패 " + Num(s.FailureCount));
            lines.Add("총 판매액 " + Won(s.TotalSales));
            lines.Add("총 종이값 " + Won(s.TotalPaperCost));
            lines.Add("총 책값 " + Won(s.TotalBookCost));
            return ReadOnly(lines);
        }

        private static string Stars(Grade grade) => grade == Grade.None ? "" : "★" + Num((int)grade);

        private static string Num(int v) => v.ToString(CultureInfo.InvariantCulture);

        private static string Won(int v) => Num(v) + "원";

        private static IReadOnlyList<string> ReadOnly(List<string> lines) => new ReadOnlyCollection<string>(lines.ToArray());
    }
}
