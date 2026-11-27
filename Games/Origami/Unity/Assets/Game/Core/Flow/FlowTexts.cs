using System;
using System.Collections.Generic;
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
    public static class FlowTexts
    {
        /// <summary>F-007 bankrupt settlement line.</summary>
        public const string BankruptNotice = "돈이 없어 가게 문을 닫았습니다";

        /// <summary>"n/7".</summary>
        public static string DayText(int day) => throw new NotImplementedException();

        /// <summary>"k/5".</summary>
        public static string AttemptsText(int attemptsLeft) => throw new NotImplementedException();

        /// <summary>"{money}원".</summary>
        public static string MoneyText(int money) => throw new NotImplementedException();

        /// <summary>"★★★" / "★★☆" / "★☆☆".</summary>
        public static string Stars(Grade grade) => throw new NotImplementedException();

        /// <summary>Arrow/letter used in sequences: U "↑", D "↓", L "←", R "→", F "F".</summary>
        public static string ActionGlyph(FoldAction action) => throw new NotImplementedException();

        /// <summary>Button label: "위로 접기", "아래로 접기", "왼쪽으로 접기", "오른쪽으로 접기", "뒤집기".</summary>
        public static string ActionName(FoldAction action) => throw new NotImplementedException();

        /// <summary>"{prerequisite name} 3개 완성 시 ({progress}/3)"; "" for initially unlocked models.</summary>
        public static string UnlockCondition(ModelDefinition model, CompletionCounts counts) => throw new NotImplementedException();

        /// <summary>One guidance line per rejection reason (C-021 table). <paramref name="model"/> is used for Locked.</summary>
        public static string Rejection(RejectReason reason, ModelDefinition model, CompletionCounts counts) => throw new NotImplementedException();

        /// <summary>All text lines of the flow's current screen, top to bottom (C-021). Buttons are not included.</summary>
        public static IReadOnlyList<string> ScreenLines(OrigamiFlow flow) => throw new NotImplementedException();
    }
}
