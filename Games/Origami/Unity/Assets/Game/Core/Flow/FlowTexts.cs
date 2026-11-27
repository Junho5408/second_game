using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using Origami.Core.Content;
using Origami.Core.Rules;
using Origami.Core.Run;

namespace Origami.Core.Flow
{
    /// <summary>
    /// All player-facing strings (Korean) as pure functions, so F-007 display criteria are testable under dotnet.
    /// Views draw these strings and add buttons; they do not build rule-dependent text themselves.
    /// Exact formats: INTERFACE_CONTRACTS.md C-021. Invariant culture, no thousands separators.
    /// </summary>
    /// <remarks>
    /// Decisions (ORIGAMI-004) for points C-021 leaves open:
    /// <list type="bullet">
    /// <item><see cref="ScreenLines"/>(null) throws <see cref="ArgumentNullException"/> (programmer error).</item>
    /// <item><see cref="UnlockCondition"/>: model null or initially unlocked → "" (counts not needed);
    /// a locked model with counts null throws <see cref="ArgumentNullException"/> (same as <see cref="Unlocking.Progress"/>).</item>
    /// <item>Folding "맞힌 단계: " line joins glyphs with one space (same as the preview sequence), "-" when none.</item>
    /// </list>
    /// </remarks>
    public static class FlowTexts
    {
        /// <summary>F-007 bankrupt settlement line.</summary>
        public const string BankruptNotice = "돈이 없어 가게 문을 닫았습니다";

        private const string AbandonQuestion = "정말 포기할까요? (Enter: 포기한다 / Esc: 계속 접기)";

        /// <summary>"n/7".</summary>
        public static string DayText(int day) => Num(day) + "/" + Num(RuleConstants.TotalDays);

        /// <summary>"k/5".</summary>
        public static string AttemptsText(int attemptsLeft) => Num(attemptsLeft) + "/" + Num(RuleConstants.DailyAttempts);

        /// <summary>"{money}원".</summary>
        public static string MoneyText(int money) => Num(money) + "원";

        /// <summary>"★★★" / "★★☆" / "★☆☆".</summary>
        public static string Stars(Grade grade)
        {
            switch (grade)
            {
                case Grade.Star3: return "★★★";
                case Grade.Star2: return "★★☆";
                case Grade.Star1: return "★☆☆";
                default: return "";
            }
        }

        /// <summary>Arrow/letter used in sequences: U "↑", D "↓", L "←", R "→", F "F".</summary>
        public static string ActionGlyph(FoldAction action)
        {
            switch (action)
            {
                case FoldAction.U: return "↑";
                case FoldAction.D: return "↓";
                case FoldAction.L: return "←";
                case FoldAction.R: return "→";
                case FoldAction.F: return "F";
                default: return "?";
            }
        }

        /// <summary>Button label: "위로 접기", "아래로 접기", "왼쪽으로 접기", "오른쪽으로 접기", "뒤집기".</summary>
        public static string ActionName(FoldAction action)
        {
            switch (action)
            {
                case FoldAction.U: return "위로 접기";
                case FoldAction.D: return "아래로 접기";
                case FoldAction.L: return "왼쪽으로 접기";
                case FoldAction.R: return "오른쪽으로 접기";
                case FoldAction.F: return "뒤집기";
                default: return "";
            }
        }

        /// <summary>"{prerequisite name} 3개 완성 시 ({progress}/3)"; "" for initially unlocked models.</summary>
        /// <exception cref="ArgumentNullException">counts is null for a model that has a prerequisite.</exception>
        public static string UnlockCondition(ModelDefinition model, CompletionCounts counts)
        {
            if (model == null || model.IsInitiallyUnlocked) return "";
            if (counts == null) throw new ArgumentNullException(nameof(counts));
            var prerequisite = OrigamiCatalog.FindModel(model.UnlockPrerequisiteId);
            string prerequisiteName = prerequisite != null ? prerequisite.DisplayName : model.UnlockPrerequisiteId;
            int progress = Unlocking.Progress(model.Id, counts);
            return prerequisiteName + " " + Num(RuleConstants.UnlockCount) + "개 완성 시 ("
                + Num(progress) + "/" + Num(RuleConstants.UnlockCount) + ")";
        }

        /// <summary>One guidance line per rejection reason (C-021 table). <paramref name="model"/> is used for Locked.</summary>
        public static string Rejection(RejectReason reason, ModelDefinition model, CompletionCounts counts)
        {
            switch (reason)
            {
                case RejectReason.RunEnded: return "런이 끝났습니다";
                case RejectReason.FoldInProgress: return "접는 중에는 할 수 없습니다";
                case RejectReason.UnknownModel: return "없는 작품입니다";
                case RejectReason.Locked: return UnlockCondition(model, counts);
                case RejectReason.NoAttemptsLeft: return "오늘은 더 접을 수 없습니다";
                case RejectReason.NotEnoughMoney: return "돈이 부족합니다";
                case RejectReason.NoFold: return "접는 중이 아닙니다";
                case RejectReason.InvalidAction: return "알 수 없는 동작입니다";
                default: return "";
            }
        }

        /// <summary>All text lines of the flow's current screen, top to bottom (C-021). Buttons are not included.</summary>
        /// <returns>A new read-only list on every call.</returns>
        /// <exception cref="ArgumentNullException">flow is null.</exception>
        public static IReadOnlyList<string> ScreenLines(OrigamiFlow flow)
        {
            if (flow == null) throw new ArgumentNullException(nameof(flow));
            var lines = new List<string>();
            switch (flow.Screen)
            {
                case FlowScreen.Workbench:
                    AddWorkbench(lines, flow.Snapshot);
                    AddMessage(lines, flow);
                    break;
                case FlowScreen.Preview:
                    AddPreview(lines, OrigamiCatalog.FindModel(flow.SelectedModelId));
                    AddMessage(lines, flow);
                    break;
                case FlowScreen.Folding:
                    AddFolding(lines, flow.Snapshot.Fold);
                    AddMessage(lines, flow);
                    break;
                case FlowScreen.AbandonConfirm:
                    AddFolding(lines, flow.Snapshot.Fold);
                    AddMessage(lines, flow);
                    lines.Add(AbandonQuestion);
                    break;
                case FlowScreen.Notice:
                    AddNotice(lines, flow);
                    break;
                case FlowScreen.Settlement:
                    AddSettlement(lines, flow.Settlement);
                    break;
                case FlowScreen.Result:
                    AddResult(lines, flow.Snapshot);
                    break;
            }
            return new ReadOnlyCollection<string>(lines);
        }

        private static void AddWorkbench(List<string> lines, RunSnapshot s)
        {
            lines.Add("일차 " + DayText(s.Day) + "   돈 " + MoneyText(s.Money) + "   시도 " + AttemptsText(s.AttemptsLeft));
            lines.Add("오늘의 인기 작품: " + ModelName(s.PopularModelId));
            bool validDay = s.Day >= 1 && s.Day <= RuleConstants.TotalDays;
            foreach (var m in OrigamiCatalog.Models)
            {
                string head = Num(m.Order) + ". " + m.DisplayName + "  ";
                if (s.IsUnlocked(m.Id))
                {
                    string line = head + Num(m.StepCount) + "단계  종이 " + MoneyText(m.PaperCost);
                    if (validDay) line += "  오늘 ★3 " + MoneyText(Pricing.SalePrice(m.Id, Grade.Star3, s.Day));
                    lines.Add(line);
                }
                else
                {
                    lines.Add(head + "잠김 - " + UnlockCondition(m, s.Completions));
                }
            }
            if (s.TodayPieces.Count == 0)
            {
                lines.Add("오늘 완성: 없음");
            }
            else
            {
                var sb = new StringBuilder("오늘 완성: ");
                for (int i = 0; i < s.TodayPieces.Count; i++)
                {
                    if (i > 0) sb.Append(", ");
                    sb.Append(ModelName(s.TodayPieces[i].ModelId)).Append(' ').Append(Stars(s.TodayPieces[i].Grade));
                }
                lines.Add(sb.ToString());
            }
        }

        private static void AddPreview(List<string> lines, ModelDefinition model)
        {
            if (model == null) return;
            lines.Add(model.DisplayName + " 도안");
            lines.Add(Glyphs(model.Steps, model.StepCount));
            lines.Add("종이값 " + MoneyText(model.PaperCost));
        }

        /// <summary>Only the snapshot is used: it exposes the correct steps so far and the hinted step, never the rest.</summary>
        private static void AddFolding(List<string> lines, FoldSnapshot fold)
        {
            if (fold == null) return;
            lines.Add(ModelName(fold.ModelId) + " 접는 중");
            lines.Add("진행 " + Num(fold.Progress) + "/" + Num(fold.StepCount));
            lines.Add("맞힌 단계: " + (fold.Progress == 0 ? "-" : Glyphs(fold.CorrectSteps, fold.Progress)));
            lines.Add("실수 " + Num(fold.Mistakes) + "/" + Num(RuleConstants.MistakeLimit));
            lines.Add("힌트 " + Num(fold.Hints));
            lines.Add("예상 등급 " + Stars(fold.ExpectedGrade));
            if (fold.IsCurrentStepRevealed)
            {
                lines.Add("힌트: " + ActionGlyph(fold.RevealedAction) + " " + ActionName(fold.RevealedAction));
            }
        }

        private static void AddNotice(List<string> lines, OrigamiFlow flow)
        {
            var r = flow.LastResult;
            switch (r.Outcome)
            {
                case CommandOutcome.FoldCompleted:
                    lines.Add(ModelName(r.Piece.ModelId) + " 완성! " + Stars(r.Piece.Grade));
                    break;
                case CommandOutcome.FoldRuined:
                    lines.Add("종이를 망쳤다");
                    break;
                case CommandOutcome.FoldAbandoned:
                    lines.Add("접기를 포기했다");
                    break;
            }
        }

        private static void AddSettlement(List<string> lines, SettlementResult s)
        {
            if (s == null) return;
            lines.Add(Num(s.SettledDay) + "일차 정산");
            foreach (var line in s.Lines)
            {
                lines.Add(ModelName(line.ModelId) + " " + Stars(line.Grade) + (line.Popular ? " (인기)" : "") + " " + MoneyText(line.Price));
            }
            lines.Add("합계 " + MoneyText(s.Total));
            lines.Add("정산 후 돈 " + MoneyText(s.MoneyAfter));
            if (s.EndReason == RunEndReason.Bankrupt) lines.Add(BankruptNotice);
            else if (!s.RunEnded) lines.Add("내일의 인기 작품: " + ModelName(s.NextPopularModelId));
        }

        private static void AddResult(List<string> lines, RunSnapshot s)
        {
            var ending = OrigamiCatalog.FindEnding(s.EndingId);
            lines.Add(ending != null ? ending.DisplayName : "");
            lines.Add(ending != null ? ending.Text : "");
            lines.Add("최종 돈 " + MoneyText(s.Money));
            foreach (var m in OrigamiCatalog.Models)
            {
                lines.Add(m.DisplayName + " ★3 " + Num(s.Completions.Get(m.Id, Grade.Star3))
                    + " / ★2 " + Num(s.Completions.Get(m.Id, Grade.Star2))
                    + " / ★1 " + Num(s.Completions.Get(m.Id, Grade.Star1)));
            }
            lines.Add("실패 " + Num(s.FailureCount) + "회");
            lines.Add("총 판매액 " + MoneyText(s.TotalSales));
            lines.Add("총 종이값 " + MoneyText(s.TotalPaperCost));
        }

        private static void AddMessage(List<string> lines, OrigamiFlow flow)
        {
            if (!string.IsNullOrEmpty(flow.Message)) lines.Add(flow.Message);
        }

        private static string Glyphs(IReadOnlyList<FoldAction> steps, int count)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < count && i < steps.Count; i++)
            {
                if (i > 0) sb.Append(' ');
                sb.Append(ActionGlyph(steps[i]));
            }
            return sb.ToString();
        }

        private static string ModelName(string modelId)
        {
            var model = OrigamiCatalog.FindModel(modelId);
            return model != null ? model.DisplayName : (modelId ?? "");
        }

        private static string Num(int value) => value.ToString(CultureInfo.InvariantCulture);
    }
}
