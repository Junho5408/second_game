using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Origami.Core.Catalog
{
    /// <summary>The 5 endings in priority order (C-120, D-114 = CONTENT_SPECS §8).</summary>
    public static class EndingCatalog
    {
        private static readonly EndingDefinition[] EndingArray =
        {
            new EndingDefinition(EndingIds.EmptyHanded, 1, "빈손", "종이는 다 떨어졌지만, 종이접기는 취미로 남았다."),
            new EndingDefinition(EndingIds.TulipGarden, 2, "튤립 꽃밭",
                "공방 창가가 종이 튤립으로 가득 찼다. 사람들이 꽃밭 공방이라 부르기 시작했다."),
            new EndingDefinition(EndingIds.Master, 3, "종이접기 명인", "책 없이도 손이 먼저 움직인다. 사람들은 당신을 명인이라 부른다."),
            new EndingDefinition(EndingIds.Tycoon, 4, "종이 재벌", "종이 한 장으로 시작해 거리의 명물 공방이 되었다."),
            new EndingDefinition(EndingIds.LocalWorkshop, 5, "동네 종이접기 공방", "소박하지만 단골이 생긴 동네 공방. 다음 주도 문을 연다."),
        };

        private static readonly IReadOnlyList<EndingDefinition> EndingList = new ReadOnlyCollection<EndingDefinition>(EndingArray);

        /// <summary>Priority order (E-01 first).</summary>
        public static IReadOnlyList<EndingDefinition> Endings => EndingList;

        /// <summary>The ending, or null for null/unknown IDs (Ordinal).</summary>
        public static EndingDefinition Find(string id)
        {
            if (id == null) return null;
            foreach (var e in EndingArray)
                if (string.Equals(e.Id, id, StringComparison.Ordinal)) return e;
            return null;
        }
    }
}
