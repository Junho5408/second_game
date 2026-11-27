using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Origami.Core.Content
{
    /// <summary>
    /// Code-defined content catalog: the 5 models, the day → popular model table and the 5 endings,
    /// transcribed from CONTENT_SPECS.md (DATA_STRUCTURE.md "콘텐츠 정의 방식"). Immutable, no I/O,
    /// identical under dotnet and Unity. Changing a value here is a content change (CONTENT_SPECS first).
    /// </summary>
    public static class OrigamiCatalog
    {
        private const FoldAction U = FoldAction.U;
        private const FoldAction D = FoldAction.D;
        private const FoldAction L = FoldAction.L;
        private const FoldAction R = FoldAction.R;
        private const FoldAction F = FoldAction.F;

        /// <summary>All models in workbench order (plane, boat, frog, crane, dragon).</summary>
        public static readonly IReadOnlyList<ModelDefinition> Models = new ReadOnlyCollection<ModelDefinition>(new[]
        {
            new ModelDefinition(1, ModelIds.Plane, "종이비행기", new[] { U, L, R }, 1, 4, null),
            new ModelDefinition(2, ModelIds.Boat, "종이배", new[] { D, U, F, U }, 1, 6, null),
            new ModelDefinition(3, ModelIds.Frog, "개구리", new[] { L, R, F, D, D, U }, 2, 10, ModelIds.Boat),
            new ModelDefinition(4, ModelIds.Crane, "학", new[] { U, L, F, R, D, F, L, U }, 3, 18, ModelIds.Frog),
            new ModelDefinition(5, ModelIds.Dragon, "용", new[] { R, R, U, F, L, D, F, U, L, D }, 6, 40, ModelIds.Crane),
        });

        /// <summary>Popular model per day; index 0 = day 1 … index 6 = day 7.</summary>
        public static readonly IReadOnlyList<string> PopularByDay = new ReadOnlyCollection<string>(new[]
        {
            ModelIds.Plane, ModelIds.Boat, ModelIds.Frog, ModelIds.Boat, ModelIds.Crane, ModelIds.Frog, ModelIds.Dragon,
        });

        /// <summary>All endings in priority order (E-01 first).</summary>
        public static readonly IReadOnlyList<EndingDefinition> Endings = new ReadOnlyCollection<EndingDefinition>(new[]
        {
            new EndingDefinition(EndingIds.EmptyHanded, 1, "빈손", "종이는 다 떨어졌지만, 종이접기는 취미로 남았다."),
            new EndingDefinition(EndingIds.CraneWish, 2, "학의 소원", "가게 천장을 가득 메운 학들. 작은 소원 하나가 이루어졌다."),
            new EndingDefinition(EndingIds.Tycoon, 3, "종이 재벌", "종이 한 장으로 시작해 거리의 명물 가게가 되었다."),
            new EndingDefinition(EndingIds.Master, 4, "종이접기 명인", "어떤 작품이든 도안 없이 완벽하게. 사람들은 당신을 명인이라 부른다."),
            new EndingDefinition(EndingIds.LocalShop, 5, "동네 종이접기 가게", "소박하지만 단골이 생긴 동네 가게. 다음 주도 문을 연다."),
        });

        /// <summary>The model with this exact ID, or null for null, "" or an unknown ID (ordinal, case-sensitive).</summary>
        public static ModelDefinition FindModel(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            for (int i = 0; i < Models.Count; i++)
            {
                if (string.Equals(Models[i].Id, id, System.StringComparison.Ordinal)) return Models[i];
            }
            return null;
        }

        /// <summary>0-based catalog index of the model, or -1 if unknown (same matching as <see cref="FindModel"/>).</summary>
        public static int IndexOf(string id)
        {
            var model = FindModel(id);
            return model == null ? -1 : model.Order - 1;
        }

        /// <summary>Popular model ID of <paramref name="day"/> (1..7), or null outside that range.</summary>
        public static string PopularModelId(int day)
        {
            if (day < 1 || day > PopularByDay.Count) return null;
            return PopularByDay[day - 1];
        }

        /// <summary>The ending with this ID, or null if unknown.</summary>
        public static EndingDefinition FindEnding(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            for (int i = 0; i < Endings.Count; i++)
            {
                if (string.Equals(Endings[i].Id, id, System.StringComparison.Ordinal)) return Endings[i];
            }
            return null;
        }
    }
}
