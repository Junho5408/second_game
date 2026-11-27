using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Origami.Core.Texts
{
    /// <summary>HUD values and display lines (C-170, F-013). Built by <see cref="GameTexts.Hud"/>. Immutable.</summary>
    public sealed class HudData
    {
        internal HudData(int day, int money, int sheetsLeft, string popularName, List<string> lines)
        {
            Day = day;
            Money = money;
            SheetsLeft = sheetsLeft;
            PopularName = popularName;
            Lines = new ReadOnlyCollection<string>(lines.ToArray());
        }

        public int Day { get; }
        public int Money { get; }
        public int SheetsLeft { get; }

        /// <summary>Display name of today's popular model.</summary>
        public string PopularName { get; }

        /// <summary>"N일차 / 7", "돈 N원", "오늘 남은 종이 N / 5", "오늘의 인기: 이름" (+ seated with a sheet: "접은 횟수 N · 자국 N").</summary>
        public IReadOnlyList<string> Lines { get; }
    }
}
