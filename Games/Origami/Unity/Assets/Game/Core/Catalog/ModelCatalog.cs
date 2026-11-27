using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Origami.Core.Folding;
using Origami.Core.Geometry;
using Origami.Core.Recognition;

namespace Origami.Core.Catalog
{
    /// <summary>
    /// Code-defined model catalogue (C-120, D-110/D-111/D-113 = CONTENT_SPECS §1, §2, §5). Reference states and the
    /// recognition catalogue are computed from the reference sequences with the Core engine (never hard-coded) and
    /// cached once (<see cref="Lazy{T}"/>, thread-safe, deterministic).
    /// </summary>
    public static class ModelCatalog
    {
        // D-111: k and x0 are computed doubles (no rounding); tulip/cicada drop points are the 6-digit CONTENT literals.
        private static readonly double K = 1 - Math.Sqrt(2);
        private static readonly double X0 = 2 * Math.Tan(Math.PI / 8);
        private static readonly Vec2 C = new Vec2(-1, -1);

        private static readonly ModelDefinition[] ModelArray = CreateModels();

        private static readonly IReadOnlyList<ModelDefinition> ModelList = new ReadOnlyCollection<ModelDefinition>(ModelArray);

        private static readonly IReadOnlyList<string> Popular = new ReadOnlyCollection<string>(new[]
        {
            ModelIds.Plane, ModelIds.House, ModelIds.Tulip, ModelIds.House, ModelIds.Dog, ModelIds.Tulip, ModelIds.Cicada,
        });

        // StatesByModel[i][n] = state before step n + 1 (index 0 = Initial, index N = reference state).
        private static readonly Lazy<PaperState[][]> StatesByModel = new Lazy<PaperState[][]>(ComputeStates);

        private static readonly Lazy<RecognitionCatalog> RecognitionLazy = new Lazy<RecognitionCatalog>(() =>
        {
            var refs = new List<KeyValuePair<string, PaperState>>();
            foreach (var m in ModelArray) refs.Add(new KeyValuePair<string, PaperState>(m.Id, ReferenceState(m.Id)));
            return new RecognitionCatalog(refs);
        });

        /// <summary>Catalogue order (plane, house, tulip, dog, cicada).</summary>
        public static IReadOnlyList<ModelDefinition> Models => ModelList;

        /// <summary>The model, or null for null/unknown IDs.</summary>
        public static ModelDefinition Find(string id)
        {
            int i = IndexOf(id);
            return i < 0 ? null : ModelArray[i];
        }

        /// <summary>Catalogue index (Ordinal), −1 if unknown or null.</summary>
        public static int IndexOf(string id)
        {
            if (id == null) return -1;
            for (int i = 0; i < ModelArray.Length; i++)
                if (string.Equals(ModelArray[i].Id, id, StringComparison.Ordinal)) return i;
            return -1;
        }

        /// <summary>ApplyAll(Steps) of the model (shared immutable instance).</summary>
        /// <exception cref="ArgumentException">unknown ID.</exception>
        public static PaperState ReferenceState(string id)
        {
            var states = StatesOf(id);
            return states[states.Length - 1];
        }

        /// <summary>1-based: 1 → Initial, N + 1 → reference state.</summary>
        /// <exception cref="ArgumentException">unknown ID.</exception>
        /// <exception cref="ArgumentOutOfRangeException">stepNumber not in 1..N+1.</exception>
        public static PaperState StateBeforeStep(string id, int stepNumber)
        {
            var states = StatesOf(id);
            if (stepNumber < 1 || stepNumber > states.Length) throw new ArgumentOutOfRangeException(nameof(stepNumber));
            return states[stepNumber - 1];
        }

        /// <summary>Recognition catalogue of the 5 reference states (catalogue order).</summary>
        public static RecognitionCatalog Recognition => RecognitionLazy.Value;

        /// <summary>Popular model per day; [0] = day 1.</summary>
        public static IReadOnlyList<string> PopularByDay => Popular;

        /// <summary>Popular model of day 1..7.</summary>
        /// <exception cref="ArgumentOutOfRangeException">day not in 1..7.</exception>
        public static string PopularModelId(int day)
        {
            if (day < 1 || day > Popular.Count) throw new ArgumentOutOfRangeException(nameof(day));
            return Popular[day - 1];
        }

        private static PaperState[] StatesOf(string id)
        {
            int i = IndexOf(id);
            if (i < 0) throw new ArgumentException("unknown model ID " + (id ?? "null"), nameof(id));
            return StatesByModel.Value[i];
        }

        private static PaperState[][] ComputeStates()
        {
            var all = new PaperState[ModelArray.Length][];
            for (int m = 0; m < ModelArray.Length; m++)
            {
                var steps = ModelArray[m].Steps;
                var states = new PaperState[steps.Count + 1];
                states[0] = PaperState.Initial;
                for (int s = 0; s < steps.Count; s++) states[s + 1] = FoldPlanner.Apply(states[s], steps[s]);
                all[m] = states;
            }
            return all;
        }

        private static Vec2 V(double x, double y) => new Vec2(x, y);
        private static FoldStep F(Vec2 g, Vec2 d, bool mountain = false, int layers = 0) => FoldStep.Fold(g, d, mountain, layers);
        private static FoldStep Cr(Vec2 g, Vec2 d) => FoldStep.Crease(g, d);

        private static ModelDefinition[] CreateModels()
        {
            var plane = new ModelDefinition(1, ModelIds.Plane, "종이비행기", 6, 0, true, 2, "자국, 자국 자석", 0,
                new[]
                {
                    Cr(V(-1, 1), V(1, 1)),
                    F(V(-1, 1), V(0, 0)),
                    F(V(1, 1), V(0, 0)),
                    F(V(-1, 0), V(0, K)),
                    F(V(1, 0), V(0, K)),
                    F(V(-X0, -1), V(X0, -1)),
                },
                new[]
                {
                    "왼쪽 변을 오른쪽 변에 맞춰 반으로 접었다가 펴요. 가운데 세로 자국이 생겨요. (Ctrl을 누른 채 놓기)",
                    "왼쪽 위 모서리를 가운데 자국 위, 종이 한가운데로 접어요.",
                    "오른쪽 위 모서리도 같은 점(왼쪽 모서리 끝)으로 접어요.",
                    "왼쪽 비스듬한 변을 가운데 자국에 맞춰 접어요. 접는 선은 위 꼭짓점에서 시작해요.",
                    "오른쪽도 똑같이. 모서리를 왼쪽 모서리 끝에 맞춰요.",
                    "가운데 자국을 따라 반으로 접어요.",
                });

            var house = new ModelDefinition(2, ModelIds.House, "집", 8, 0, true, 1, "자국 2개로 지붕 선 맞춤", 0,
                new[]
                {
                    F(V(-1, 1), V(-1, -1)),
                    Cr(V(-1, 0), V(1, 0)),
                    Cr(V(-1, -1), V(-1, 0)),
                    F(V(-1, 0), V(-0.6, -0.8)),
                    F(V(1, 0), V(0.6, -0.8)),
                },
                new[]
                {
                    "위쪽 변을 아래쪽 변에 맞춰 반으로 접어요. 가로로 긴 직사각형이 돼요.",
                    "왼쪽 변을 오른쪽 변에 맞춰 접었다 펴서 가운데 세로 자국을 만들어요(두 겹 함께).",
                    "아래쪽 변을 위쪽 변에 맞춰 접었다 펴서 가로 자국을 만들어요.",
                    "왼쪽 위 모서리를 접어 내려요. 접는 선이 위쪽 변의 세로 자국 끝과 왼쪽 변의 가로 자국 끝을 지나요.",
                    "오른쪽도 똑같이. 지붕 완성!",
                });

            var tulip = new ModelDefinition(3, ModelIds.Tulip, "튤립", 12, 20, false, 2, "위 2겹 접기(꽃잎)", 45,
                new[]
                {
                    F(V(1, 1), C),
                    F(V(-1, 1), C),
                    F(V(1, -1), C),
                    F(C, V(0.689655, -0.275862), false, 2),
                    F(C, V(-0.275862, 0.689655), false, 2),
                },
                new[]
                {
                    "대각선으로 반 접어 삼각형을 만들어요.",
                    "왼쪽 위 꼭짓점을 직각 꼭짓점에 맞춰 접어요.",
                    "오른쪽 꼭짓점도 같은 점으로. 작은 정사각형(다이아몬드 기본형)이 돼요.",
                    "맨 위 덮개(2겹)만 집어서 끝을 오른쪽 바깥으로 접어 꽃잎을 만들어요. 접는 선은 오른쪽 아래 꼭짓점에서 가운데 대각선 위 (−0.3,−0.3)(꼭짓점 C에서 대각선을 따라 9.9cm)을 지나요. 아래 바닥은 그대로!",
                    "남은 덮개(2겹)도 똑같이. 접는 선은 왼쪽 위 꼭짓점과, 첫 꽃잎 접는 선이 대각선과 만난 점을 지나요.",
                });

            var dog = new ModelDefinition(4, ModelIds.Dog, "강아지 얼굴", 18, 50, false, 3, "자국 교차점(턱)", 0,
                new[]
                {
                    F(V(1, 1), C),
                    Cr(V(-1, 1), V(1, -1)),
                    Cr(C, V(0, 0)),
                    F(V(-1, 1), V(-1.4, 0.0)),
                    F(V(1, -1), V(0.0, -1.4)),
                    F(C, V(-0.5, -0.5)),
                },
                new[]
                {
                    "대각선으로 반 접어 삼각형을 만들어요. 긴 변이 강아지 머리 윗선이에요.",
                    "삼각형을 반으로 접었다 펴서 가운데 자국을 만들어요.",
                    "아래 꼭짓점을 긴 변 가운데에 맞춰 접었다 펴서 가로 자국을 만들어요.",
                    "왼쪽 꼭짓점을 바깥 아래로 접어 귀를 만들어요. 접는 선은 짧은 변 위 꼭짓점에서 5.8cm 지점부터 긴 변 가운데 바로 옆(0.5cm)까지.",
                    "오른쪽 귀도 똑같이.",
                    "아래 꼭짓점을 두 자국이 만나는 점까지 접어 올려 턱을 만들어요.",
                });

            var cicada = new ModelDefinition(5, ModelIds.Cicada, "매미", 26, 150, false, 4,
                "자국 2개(대각선·세로), 위 2겹(날개), 산접기", 45,
                new[]
                {
                    F(V(1, 1), C),
                    F(V(-1, 1), C),
                    F(V(1, -1), C),
                    Cr(V(-1, 0), V(0, -1)),
                    Cr(V(-1, 0), V(0, 0)),
                    F(C, V(0.453858, -0.108926), false, 2),
                    F(C, V(-0.108926, 0.453858), false, 2),
                    F(C, V(-0.38, -0.38)),
                    F(V(-1, 0), V(-0.5, -0.5), true),
                    F(V(0, -1), V(-0.5, -0.5), true),
                },
                new[]
                {
                    "대각선으로 반 접어 삼각형.",
                    "왼쪽 위 꼭짓점을 직각 꼭짓점 C에 맞춰요.",
                    "오른쪽 꼭짓점도 C로. 다이아몬드 기본형.",
                    "다이아몬드를 대각선으로 반 접었다 펴서 몸통 가운데 자국을 만들어요.",
                    "왼쪽 변을 오른쪽 변에 맞춰 접었다 펴서 세로 자국을 만들어요. 두 자국이 가운데 (−0.5,−0.5)에서 만나요(9·10단계 기준점).",
                    "맨 위 덮개(2겹)만 집어 끝 C를 오른쪽 바깥으로 접어 날개를 만들어요. 접는 선은 오른쪽 아래 꼭짓점에서 대각선 자국 위 (−0.38,−0.38)(C에서 대각선을 따라 8.8cm)을 지나요. 아래 바닥은 그대로!",
                    "남은 덮개(2겹)로 반대쪽 날개. 접는 선은 왼쪽 위 꼭짓점과, 첫 날개 선이 대각선 자국과 만난 점을 지나요.",
                    "머리: 바닥에 남은 C 끝을 두 날개 선이 만난 점까지 접어 올려요.",
                    "왼쪽 옆 꼭짓점을 뒤로 접어 두 자국이 만나는 가운데 점에 맞춰요.",
                    "오른쪽 옆도 뒤로 같은 점에 맞춰요. 매미 완성!",
                });

            return new[] { plane, house, tulip, dog, cicada };
        }
    }
}
