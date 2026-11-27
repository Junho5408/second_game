using System.Collections.Generic;

namespace Origami.Tests.Qa
{
    // QA ORIGAMI-003: an independent reference model of GAME_RULES R-001..R-015 + CONTENT_SPECS.
    // Deliberately written WITHOUT any Origami.Core type or constant (only ints and strings, data
    // copied by hand from the spec tables) so that a shared bug in Core cannot hide itself.
    // Used by RunDifferentialQaTests to compare OrigamiRun against the spec over generated command streams.
    internal sealed class RunReferenceModel
    {
        // CONTENT_SPECS "작품 (모델) 표". Actions: U=1, D=2, L=3, R=4, F=5 (FoldAction enum values).
        public static readonly string[] Ids = { "plane", "boat", "frog", "crane", "dragon" };
        public static readonly int[][] Steps =
        {
            new[] { 1, 3, 4 },                         // U L R
            new[] { 2, 1, 5, 1 },                      // D U F U
            new[] { 3, 4, 5, 2, 2, 1 },                // L R F D D U
            new[] { 1, 3, 5, 4, 2, 5, 3, 1 },          // U L F R D F L U
            new[] { 4, 4, 1, 5, 3, 2, 5, 1, 3, 2 },    // R R U F L D F U L D
        };
        public static readonly int[] Paper = { 1, 1, 2, 3, 6 };
        public static readonly int[] BasePrice = { 4, 6, 10, 18, 40 };
        public static readonly int[] Prereq = { -1, -1, 1, 2, 3 };
        // CONTENT_SPECS "일차별 인기 작품": plane boat frog boat crane frog dragon.
        public static readonly int[] PopularByDay = { 0, 1, 2, 1, 3, 2, 4 };

        // Reject reasons / outcomes / end reasons as names, compared against the enums' ToString().
        public const string None = "None";

        public int Day, Money, Attempts, Failures, Sales, PaperCost;
        public readonly int[,] Counts = new int[5, 4]; // [model, grade 1..3]
        public readonly List<int[]> Today = new List<int[]>(); // {model, grade}
        public int FoldModel = -1, Progress, Mistakes, Hints;
        public bool Revealed;
        public string EndReason = None; // None / Completed / Bankrupt
        public string Ending;

        public RunReferenceModel() { NewRunState(); }

        public bool Folding => FoldModel >= 0;
        public bool Ended => EndReason != None;

        public static int IndexOf(string id)
        {
            if (id == null) return -1;
            for (int i = 0; i < Ids.Length; i++) if (Ids[i] == id) return i;
            return -1;
        }

        public int Total(int model) => Counts[model, 1] + Counts[model, 2] + Counts[model, 3];

        public bool Unlocked(int model) => Prereq[model] < 0 || Total(Prereq[model]) >= 3;

        public static int GradeOf(int mistakes, int hints)
        {
            int penalty = mistakes * 2 + hints;
            if (penalty == 0) return 3;
            return penalty <= 2 ? 2 : 1;
        }

        public static int Price(int model, int grade, int day)
        {
            int gradePct = grade == 3 ? 150 : grade == 2 ? 100 : 60;
            int demandPct = PopularByDay[day - 1] == model ? 150 : 100;
            return BasePrice[model] * gradePct * demandPct / 10000;
        }

        public static string EndingFor(string reason, int money, int[,] counts)
        {
            if (reason == "Bankrupt" || money <= 20) return "E-01";
            if (counts[3, 1] + counts[3, 2] + counts[3, 3] >= 15) return "E-02";
            if (money >= 1000) return "E-03";
            bool all = true;
            for (int m = 0; m < 5; m++) if (counts[m, 3] < 1) all = false;
            return all ? "E-04" : "E-05";
        }

        /// <summary>Expected result of one command.</summary>
        internal sealed class Expected
        {
            public bool Accepted;
            public string Reason = None;
            public string Outcome;
            public int PieceModel = -1, PieceGrade;
            public bool HasSettlement;
            public int SettledDay, Total, MoneyAfter;
            public string SettleEnd, NextPopular;
            public List<int[]> Lines; // {model, grade, popular 0/1, price}

            public static Expected Reject(string reason) => new Expected { Accepted = false, Reason = reason, Outcome = "Rejected" };
            public static Expected Ok(string outcome) => new Expected { Accepted = true, Outcome = outcome };
        }

        public Expected NewRun()
        {
            NewRunState();
            return Expected.Ok("RunStarted");
        }

        public Expected Start(string id)
        {
            if (Ended) return Expected.Reject("RunEnded");
            if (Folding) return Expected.Reject("FoldInProgress");
            int m = IndexOf(id);
            if (m < 0) return Expected.Reject("UnknownModel");
            if (!Unlocked(m)) return Expected.Reject("Locked");
            if (Attempts == 0) return Expected.Reject("NoAttemptsLeft");
            if (Money < Paper[m]) return Expected.Reject("NotEnoughMoney");
            Money -= Paper[m];
            PaperCost += Paper[m];
            Attempts--;
            FoldModel = m; Progress = 0; Mistakes = 0; Hints = 0; Revealed = false;
            return Expected.Ok("FoldStarted");
        }

        public Expected Fold(int action)
        {
            if (Ended) return Expected.Reject("RunEnded");
            if (!Folding) return Expected.Reject("NoFold");
            if (action < 1 || action > 5) return Expected.Reject("InvalidAction");
            int[] steps = Steps[FoldModel];
            if (steps[Progress] == action)
            {
                Progress++;
                Revealed = false;
                if (Progress < steps.Length) return Expected.Ok("StepCorrect");
                int g = GradeOf(Mistakes, Hints);
                var e = Expected.Ok("FoldCompleted");
                e.PieceModel = FoldModel; e.PieceGrade = g;
                Today.Add(new[] { FoldModel, g });
                Counts[FoldModel, g]++;
                FoldModel = -1;
                return e;
            }
            Mistakes++;
            if (Mistakes < 3) return Expected.Ok("StepMistake");
            Failures++;
            FoldModel = -1;
            return Expected.Ok("FoldRuined");
        }

        public Expected Hint()
        {
            if (Ended) return Expected.Reject("RunEnded");
            if (!Folding) return Expected.Reject("NoFold");
            if (!Revealed) { Revealed = true; Hints++; }
            return Expected.Ok("HintRevealed");
        }

        public Expected Abandon()
        {
            if (Ended) return Expected.Reject("RunEnded");
            if (!Folding) return Expected.Reject("NoFold");
            Failures++;
            FoldModel = -1;
            return Expected.Ok("FoldAbandoned");
        }

        public Expected EndDay()
        {
            if (Ended) return Expected.Reject("RunEnded");
            if (Folding) return Expected.Reject("FoldInProgress");
            var e = new Expected { Accepted = true, HasSettlement = true, SettledDay = Day, Lines = new List<int[]>() };
            int total = 0;
            foreach (var p in Today)
            {
                int price = Price(p[0], p[1], Day);
                e.Lines.Add(new[] { p[0], p[1], PopularByDay[Day - 1] == p[0] ? 1 : 0, price });
                total += price;
            }
            Today.Clear();
            Money += total;
            Sales += total;
            e.Total = total;
            if (Day == 7)
            {
                EndReason = "Completed";
            }
            else
            {
                Day++;
                Attempts = 5;
                int cheapest = int.MaxValue;
                for (int m = 0; m < 5; m++) if (Unlocked(m) && Paper[m] < cheapest) cheapest = Paper[m];
                if (Money < cheapest) EndReason = "Bankrupt";
            }
            if (Ended) Ending = EndingFor(EndReason, Money, Counts);
            e.MoneyAfter = Money;
            e.SettleEnd = EndReason;
            e.NextPopular = Ended ? null : Ids[PopularByDay[Day - 1]];
            e.Outcome = Ended ? "RunFinished" : "DayEnded";
            return e;
        }

        private void NewRunState()
        {
            Day = 1; Money = 20; Attempts = 5; Failures = 0; Sales = 0; PaperCost = 0;
            for (int m = 0; m < 5; m++) for (int g = 0; g < 4; g++) Counts[m, g] = 0;
            Today.Clear();
            FoldModel = -1; Progress = 0; Mistakes = 0; Hints = 0; Revealed = false;
            EndReason = None;
            Ending = null;
        }
    }
}
