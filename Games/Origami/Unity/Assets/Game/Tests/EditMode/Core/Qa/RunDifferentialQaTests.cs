using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using Origami.Core.Content;
using Origami.Core.Rules;
using Origami.Core.Run;

namespace Origami.Tests.Qa
{
    // QA ORIGAMI-003: differential testing of OrigamiRun against RunReferenceModel (independent spec model).
    // Thousands of seeded, deterministic command streams (own xorshift PRNG — no System.Random, so the streams
    // are identical under .NET and Unity/Mono) mix valid play, invalid inputs (bogus IDs, undefined actions,
    // commands in the wrong state), hints, abandons, mistakes, day ends, bankruptcy paths and mid-run NewRun.
    // After EVERY command the command result (accept/reason/outcome/piece/settlement) and the full snapshot are
    // compared, and the C-010 invariants are checked. A coverage gate proves the streams reach every ending,
    // every reject reason and every outcome, so the comparison is not vacuous.
    public class RunDifferentialQaTests
    {
        private const int Streams = 2500;
        private const int CommandsPerStream = 400;

        private sealed class Rng
        {
            private ulong _s;
            public Rng(int seed) { _s = (ulong)(uint)seed * 0x9E3779B97F4A7C15UL + 0x2545F4914F6CDD1DUL; if (_s == 0) _s = 1; }
            public uint Next()
            {
                _s ^= _s << 13; _s ^= _s >> 7; _s ^= _s << 17;
                return (uint)(_s >> 16);
            }
            public int Int(int n) => (int)(Next() % (uint)n);
            public bool Chance(int perMille) => Int(1000) < perMille;
        }

        private static readonly string[] BogusIds = { null, "", "unicorn", "Plane", "BOAT", " frog", "crane ", "dragon\0" };
        private static readonly int[] BogusActions = { 0, 6, 7, -1, 99, int.MinValue, int.MaxValue };

        private sealed class Profile
        {
            public int Correct;    // ‰ chance a fold input is the right action
            public int Hint;       // ‰ chance to press Hint while folding
            public int Abandon;    // ‰ chance to abandon while folding
            public int Invalid;    // ‰ chance of an undefined action value / wrong-state command
            public int EndEarly;   // ‰ chance to end the day while attempts remain
            public int Bogus;      // ‰ chance of a bogus model ID on Start
            public int NewRun;     // ‰ chance of NewRun per command
            public int Prefer;     // 0 highest unlocked, 1 crane lover, 2 random, 3 plane only, 4 rotate all models
        }

        private static Profile MakeProfile(Rng rng, int seed)
        {
            var p = new Profile
            {
                Correct = 650 + rng.Int(351),
                Hint = rng.Int(4) == 0 ? 0 : rng.Int(120),
                Abandon = rng.Int(3) == 0 ? 0 : rng.Int(60),
                Invalid = rng.Int(60),
                EndEarly = rng.Int(4) == 0 ? 0 : rng.Int(150),
                Bogus = rng.Int(60),
                NewRun = rng.Int(5) == 0 ? rng.Int(4) : 0,
                Prefer = seed % 5,
            };
            if (seed % 7 == 0) { p.Correct = 1000; p.Hint = 0; p.Abandon = 0; p.EndEarly = 0; } // perfect players
            if (seed % 11 == 0) { p.Correct = 300; p.Abandon = 300; } // wasters → bankruptcy
            return p;
        }

        private struct Cmd
        {
            public int Kind; // 0 NewRun, 1 Start, 2 Fold, 3 Hint, 4 Abandon, 5 EndDay
            public string Id;
            public int Action;
            public override string ToString()
            {
                switch (Kind)
                {
                    case 0: return "NewRun";
                    case 1: return "Start(" + (Id == null ? "null" : "\"" + Id + "\"") + ")";
                    case 2: return "Fold(" + Action + ")";
                    case 3: return "Hint";
                    case 4: return "Abandon";
                    default: return "EndDay";
                }
            }
        }

        private static Cmd Next(Rng rng, Profile p, RunReferenceModel m, ref int rotate)
        {
            if (p.NewRun > 0 && rng.Chance(p.NewRun)) return new Cmd { Kind = 0 };
            if (m.Ended) return rng.Chance(300) ? new Cmd { Kind = 0 } : new Cmd { Kind = 1 + rng.Int(5), Id = "plane", Action = 1 + rng.Int(5) };
            if (rng.Chance(p.Invalid))
            {
                // Wrong-state or malformed command.
                switch (rng.Int(5))
                {
                    case 0: return new Cmd { Kind = 2, Action = BogusActions[rng.Int(BogusActions.Length)] };
                    case 1: return new Cmd { Kind = 1, Id = RunReferenceModel.Ids[rng.Int(5)] };
                    case 2: return new Cmd { Kind = 5 };
                    case 3: return new Cmd { Kind = 3 };
                    default: return new Cmd { Kind = 4 };
                }
            }
            if (m.Folding)
            {
                if (rng.Chance(p.Abandon)) return new Cmd { Kind = 4 };
                if (rng.Chance(p.Hint)) return new Cmd { Kind = 3 };
                int correct = RunReferenceModel.Steps[m.FoldModel][m.Progress];
                if (rng.Chance(p.Correct)) return new Cmd { Kind = 2, Action = correct };
                int wrong = 1 + rng.Int(4);
                if (wrong >= correct) wrong++;
                return new Cmd { Kind = 2, Action = wrong };
            }
            if (m.Attempts == 0 || rng.Chance(p.EndEarly)) return new Cmd { Kind = 5 };
            if (rng.Chance(p.Bogus)) return new Cmd { Kind = 1, Id = BogusIds[rng.Int(BogusIds.Length)] };
            return new Cmd { Kind = 1, Id = RunReferenceModel.Ids[ChooseModel(rng, p, m, ref rotate)] };
        }

        private static int ChooseModel(Rng rng, Profile p, RunReferenceModel m, ref int rotate)
        {
            int best = 0;
            for (int i = 4; i >= 0; i--)
            {
                if (m.Unlocked(i) && RunReferenceModel.Paper[i] <= m.Money) { best = i; break; }
            }
            switch (p.Prefer)
            {
                case 0: return best;
                case 1: return m.Unlocked(3) && m.Money >= 3 ? 3 : (best > 2 ? 2 : best);
                case 2: return rng.Int(5); // may be locked / unaffordable
                case 3: return 0;
                default:
                    // Rotate through all unlocked models (aims at E-04: every model ★3), else build the chain.
                    if (!m.Unlocked(4)) return best;
                    rotate = (rotate + 1) % 5;
                    return rotate;
            }
        }

        private static CommandResult Apply(OrigamiRun run, Cmd c)
        {
            switch (c.Kind)
            {
                case 0: return run.NewRun();
                case 1: return run.Start(c.Id);
                case 2: return run.Fold((FoldAction)c.Action);
                case 3: return run.Hint();
                case 4: return run.Abandon();
                default: return run.EndDay();
            }
        }

        private static RunReferenceModel.Expected ApplyModel(RunReferenceModel m, Cmd c)
        {
            switch (c.Kind)
            {
                case 0: return m.NewRun();
                case 1: return m.Start(c.Id);
                case 2: return m.Fold(c.Action);
                case 3: return m.Hint();
                case 4: return m.Abandon();
                default: return m.EndDay();
            }
        }

        private sealed class Coverage
        {
            public readonly HashSet<string> Endings = new HashSet<string>();
            public readonly HashSet<string> Reasons = new HashSet<string>();
            public readonly HashSet<string> Outcomes = new HashSet<string>();
            public int Commands, Bankrupt, Completed, MaxMoney, MaxCrane, RuinedOnLastAttempt, RuinedOnLastStep,
                CompletedWithTwoMistakes, SameDayUnlockStarts, MoneyZeroDuringDay, NewRunMidFold;
        }

        [Test]
        public void OrigamiRun_MatchesReferenceModel_OnSeededCommandStreams()
        {
            var cov = new Coverage();
            for (int seed = 1; seed <= Streams; seed++)
            {
                string failure = RunStream(seed, CommandsPerStream, cov);
                if (failure != null) Assert.Fail(failure);
            }

            TestContext.WriteLine("commands " + cov.Commands + ", completed runs " + cov.Completed + ", bankrupt " + cov.Bankrupt
                + ", endings " + string.Join(",", new List<string>(cov.Endings).ToArray()) + ", maxMoney " + cov.MaxMoney + ", maxCrane " + cov.MaxCrane
                + ", ruinLastAttempt " + cov.RuinedOnLastAttempt + ", ruinLastStep " + cov.RuinedOnLastStep + ", complete2Mistakes " + cov.CompletedWithTwoMistakes
                + ", sameDayUnlockStarts " + cov.SameDayUnlockStarts + ", moneyZeroDuringDay " + cov.MoneyZeroDuringDay + ", newRunMidFold " + cov.NewRunMidFold);

            // Coverage gate: the comparison above is only meaningful if the streams reach all of this.
            foreach (var e in new[] { "E-01", "E-02", "E-03", "E-04", "E-05" })
                Assert.IsTrue(cov.Endings.Contains(e), "ending never reached: " + e);
            foreach (var r in new[] { "RunEnded", "FoldInProgress", "UnknownModel", "Locked", "NoAttemptsLeft", "NotEnoughMoney", "NoFold", "InvalidAction" })
                Assert.IsTrue(cov.Reasons.Contains(r), "reject reason never produced: " + r);
            foreach (var o in new[] { "RunStarted", "FoldStarted", "StepCorrect", "StepMistake", "FoldCompleted", "FoldRuined", "HintRevealed", "FoldAbandoned", "DayEnded", "RunFinished" })
                Assert.IsTrue(cov.Outcomes.Contains(o), "outcome never produced: " + o);
            Assert.Greater(cov.Bankrupt, 0, "bankruptcy");
            Assert.Greater(cov.Completed, 0, "completed runs");
            Assert.GreaterOrEqual(cov.MaxMoney, 1000, "tycoon money reached");
            Assert.GreaterOrEqual(cov.MaxCrane, 15, "crane wish reached");
            Assert.Greater(cov.RuinedOnLastAttempt, 0, "ruin on the last attempt of a day");
            Assert.Greater(cov.RuinedOnLastStep, 0, "ruin on the last step");
            Assert.Greater(cov.CompletedWithTwoMistakes, 0, "completion with 2 mistakes");
            Assert.Greater(cov.SameDayUnlockStarts, 0, "start of a model unlocked the same day");
            Assert.Greater(cov.MoneyZeroDuringDay, 0, "money 0 during a day");
            Assert.Greater(cov.NewRunMidFold, 0, "NewRun while folding");
        }

        [Test]
        public void OrigamiRun_StreamsAreDeterministic_TwoInstancesInterleaved()
        {
            // R-001 / F-001 AC-10 at scale: two runs fed the same stream, interleaved command by command,
            // must agree on every result and snapshot (also proves no shared static mutable state).
            for (int seed = 1; seed <= 200; seed++)
            {
                var rng = new Rng(seed);
                var p = MakeProfile(rng, seed);
                var model = new RunReferenceModel();
                var a = new OrigamiRun();
                var b = new OrigamiRun();
                int rotate = 0;
                for (int i = 0; i < CommandsPerStream; i++)
                {
                    var c = Next(rng, p, model, ref rotate);
                    ApplyModel(model, c);
                    var ra = Apply(a, c);
                    var rb = Apply(b, c);
                    Assert.AreEqual(ra.ToString(), rb.ToString(), "seed " + seed + " cmd " + i);
                    string diff = Diff(a.GetSnapshot(), b.GetSnapshot());
                    if (diff != null) Assert.Fail("seed " + seed + " cmd " + i + " " + c + ": " + diff);
                }
            }
        }

        private static string RunStream(int seed, int count, Coverage cov)
        {
            var rng = new Rng(seed);
            var p = MakeProfile(rng, seed);
            var model = new RunReferenceModel();
            var run = new OrigamiRun();
            var log = new List<string>();
            int rotate = 0;
            var unlockedToday = new bool[5];
            for (int i = 0; i < count; i++)
            {
                var c = Next(rng, p, model, ref rotate);
                log.Add(c.ToString());

                bool lastAttempt = model.Folding && model.Attempts == 0;
                bool lastStep = model.Folding && model.Progress == RunReferenceModel.Steps[model.FoldModel].Length - 1;
                bool twoMistakes = model.Folding && model.Mistakes == 2;
                bool wasFolding = model.Folding;
                var unlockedBefore = new bool[5];
                for (int m = 0; m < 5; m++) unlockedBefore[m] = model.Unlocked(m);
                int dayBefore = model.Day;

                var before = run.GetSnapshot();
                var exp = ApplyModel(model, c);
                var act = Apply(run, c);
                var after = run.GetSnapshot();

                string err = CompareResult(exp, act) ?? CompareState(model, after) ?? Invariants(after);
                if (err == null && !act.IsAccepted) err = Diff(before, after);
                if (err != null)
                {
                    int from = log.Count > 40 ? log.Count - 40 : 0;
                    var sb = new StringBuilder();
                    sb.Append("seed ").Append(seed).Append(" command #").Append(i).Append(' ').Append(c).Append(": ").Append(err);
                    sb.Append("\nlast commands: ").Append(string.Join(", ", log.GetRange(from, log.Count - from).ToArray()));
                    return sb.ToString();
                }

                // Coverage bookkeeping.
                cov.Commands++;
                if (act.IsAccepted) cov.Outcomes.Add(act.Outcome.ToString()); else cov.Reasons.Add(act.Reason.ToString());
                if (act.Outcome == CommandOutcome.RunFinished)
                {
                    cov.Endings.Add(after.EndingId);
                    if (after.EndReason == RunEndReason.Bankrupt) cov.Bankrupt++; else cov.Completed++;
                }
                if (after.Money > cov.MaxMoney) cov.MaxMoney = after.Money;
                if (after.Completions.Total(ModelIds.Crane) > cov.MaxCrane) cov.MaxCrane = after.Completions.Total(ModelIds.Crane);
                if (act.Outcome == CommandOutcome.FoldRuined && lastAttempt) cov.RuinedOnLastAttempt++;
                if (act.Outcome == CommandOutcome.FoldRuined && lastStep) cov.RuinedOnLastStep++;
                if (act.Outcome == CommandOutcome.FoldCompleted && twoMistakes) cov.CompletedWithTwoMistakes++;
                if (act.Outcome == CommandOutcome.RunStarted && wasFolding) cov.NewRunMidFold++;
                if (after.Money == 0 && !after.IsRunEnded) cov.MoneyZeroDuringDay++;
                if (model.Day != dayBefore || act.Outcome == CommandOutcome.RunStarted) for (int m = 0; m < 5; m++) unlockedToday[m] = false;
                for (int m = 0; m < 5; m++) if (!unlockedBefore[m] && model.Unlocked(m)) unlockedToday[m] = true;
                if (act.Outcome == CommandOutcome.FoldStarted && unlockedToday[RunReferenceModel.IndexOf(c.Id)]) cov.SameDayUnlockStarts++;
            }
            return null;
        }

        private static string CompareResult(RunReferenceModel.Expected e, CommandResult r)
        {
            if (e.Accepted != r.IsAccepted) return "accepted expected " + e.Accepted + " got " + r;
            if (e.Reason != r.Reason.ToString()) return "reason expected " + e.Reason + " got " + r.Reason;
            if (e.Outcome != r.Outcome.ToString()) return "outcome expected " + e.Outcome + " got " + r.Outcome;
            if (!r.IsAccepted && r.Reason == RejectReason.None) return "rejected with None";
            if (e.PieceModel >= 0)
            {
                if (r.Piece.ModelId != RunReferenceModel.Ids[e.PieceModel] || (int)r.Piece.Grade != e.PieceGrade)
                    return "piece expected " + RunReferenceModel.Ids[e.PieceModel] + "/" + e.PieceGrade + " got " + r.Piece;
            }
            else if (r.Piece.ModelId != null) return "unexpected piece " + r.Piece;
            if (e.HasSettlement != (r.Settlement != null)) return "settlement presence expected " + e.HasSettlement;
            if (!e.HasSettlement) return null;
            var s = r.Settlement;
            if (s.SettledDay != e.SettledDay) return "settledDay expected " + e.SettledDay + " got " + s.SettledDay;
            if (s.Total != e.Total) return "total expected " + e.Total + " got " + s.Total;
            if (s.MoneyAfter != e.MoneyAfter) return "moneyAfter expected " + e.MoneyAfter + " got " + s.MoneyAfter;
            if (s.EndReason.ToString() != e.SettleEnd) return "settlement endReason expected " + e.SettleEnd + " got " + s.EndReason;
            if (s.RunEnded != (e.SettleEnd != RunReferenceModel.None)) return "settlement RunEnded";
            if (s.NextPopularModelId != e.NextPopular) return "nextPopular expected " + e.NextPopular + " got " + s.NextPopularModelId;
            if (s.Lines.Count != e.Lines.Count) return "line count expected " + e.Lines.Count + " got " + s.Lines.Count;
            int sum = 0;
            for (int i = 0; i < e.Lines.Count; i++)
            {
                var l = s.Lines[i];
                var x = e.Lines[i];
                if (l.ModelId != RunReferenceModel.Ids[x[0]] || (int)l.Grade != x[1] || l.Popular != (x[2] == 1) || l.Price != x[3])
                    return "line " + i + " expected " + RunReferenceModel.Ids[x[0]] + "/" + x[1] + "/" + x[2] + "/" + x[3]
                        + " got " + l.ModelId + "/" + (int)l.Grade + "/" + l.Popular + "/" + l.Price;
                sum += l.Price;
            }
            if (sum != s.Total) return "total != sum of lines";
            return null;
        }

        private static string CompareState(RunReferenceModel m, RunSnapshot s)
        {
            if (s.Day != m.Day) return "Day expected " + m.Day + " got " + s.Day;
            if (s.Money != m.Money) return "Money expected " + m.Money + " got " + s.Money;
            if (s.AttemptsLeft != m.Attempts) return "AttemptsLeft expected " + m.Attempts + " got " + s.AttemptsLeft;
            if (s.PopularModelId != RunReferenceModel.Ids[RunReferenceModel.PopularByDay[m.Day - 1]]) return "PopularModelId " + s.PopularModelId;
            var unlocked = new List<string>();
            for (int i = 0; i < 5; i++) if (m.Unlocked(i)) unlocked.Add(RunReferenceModel.Ids[i]);
            if (string.Join(",", unlocked.ToArray()) != string.Join(",", ToArray(s.UnlockedModelIds))) return "Unlocked expected " + string.Join(",", unlocked.ToArray());
            for (int i = 0; i < 5; i++)
            {
                if (s.IsUnlocked(RunReferenceModel.Ids[i]) != m.Unlocked(i)) return "IsUnlocked " + RunReferenceModel.Ids[i];
                for (int g = 1; g <= 3; g++)
                    if (s.Completions.Get(RunReferenceModel.Ids[i], (Grade)g) != m.Counts[i, g]) return "Completions " + RunReferenceModel.Ids[i] + "/" + g;
            }
            if (s.FailureCount != m.Failures) return "FailureCount expected " + m.Failures + " got " + s.FailureCount;
            if (s.TotalSales != m.Sales) return "TotalSales expected " + m.Sales + " got " + s.TotalSales;
            if (s.TotalPaperCost != m.PaperCost) return "TotalPaperCost expected " + m.PaperCost + " got " + s.TotalPaperCost;
            if (s.TodayPieces.Count != m.Today.Count) return "TodayPieces count expected " + m.Today.Count + " got " + s.TodayPieces.Count;
            for (int i = 0; i < m.Today.Count; i++)
                if (s.TodayPieces[i].ModelId != RunReferenceModel.Ids[m.Today[i][0]] || (int)s.TodayPieces[i].Grade != m.Today[i][1]) return "TodayPieces[" + i + "]";
            if (s.EndReason.ToString() != m.EndReason) return "EndReason expected " + m.EndReason + " got " + s.EndReason;
            if (s.IsRunEnded != m.Ended) return "IsRunEnded";
            if (s.EndingId != m.Ending) return "EndingId expected " + m.Ending + " got " + s.EndingId;
            if (s.IsFolding != m.Folding) return "IsFolding expected " + m.Folding;
            if (!m.Folding) return s.Fold == null ? null : "Fold not null";
            var f = s.Fold;
            int[] steps = RunReferenceModel.Steps[m.FoldModel];
            if (f.ModelId != RunReferenceModel.Ids[m.FoldModel]) return "Fold.ModelId";
            if (f.StepCount != steps.Length) return "Fold.StepCount";
            if (f.Progress != m.Progress) return "Fold.Progress expected " + m.Progress + " got " + f.Progress;
            if (f.CorrectSteps.Count != m.Progress) return "Fold.CorrectSteps count";
            for (int i = 0; i < m.Progress; i++) if ((int)f.CorrectSteps[i] != steps[i]) return "Fold.CorrectSteps[" + i + "]";
            if (f.Mistakes != m.Mistakes) return "Fold.Mistakes expected " + m.Mistakes + " got " + f.Mistakes;
            if (f.Hints != m.Hints) return "Fold.Hints expected " + m.Hints + " got " + f.Hints;
            if (f.IsCurrentStepRevealed != m.Revealed) return "Fold.IsCurrentStepRevealed expected " + m.Revealed;
            if ((int)f.RevealedAction != (m.Revealed ? steps[m.Progress] : 0)) return "Fold.RevealedAction " + f.RevealedAction;
            if (f.Penalty != m.Mistakes * 2 + m.Hints) return "Fold.Penalty";
            if ((int)f.ExpectedGrade != RunReferenceModel.GradeOf(m.Mistakes, m.Hints)) return "Fold.ExpectedGrade";
            return null;
        }

        private static string Invariants(RunSnapshot s)
        {
            if (s.Money < 0) return "invariant: money < 0";
            if (s.AttemptsLeft < 0 || s.AttemptsLeft > 5) return "invariant: attempts out of 0..5";
            if (s.Day < 1 || s.Day > 7) return "invariant: day out of 1..7";
            if (s.Fold != null && s.Fold.Mistakes > 2) return "invariant: mistakes > 2 while folding";
            if (s.IsRunEnded == (s.EndingId == null)) return "invariant: ending iff ended";
            if (s.IsRunEnded && s.IsFolding) return "invariant: folding after the run ended";
            if (s.Money != 20 - s.TotalPaperCost + s.TotalSales) return "invariant: money = 20 − paper + sales";
            return null;
        }

        /// <summary>Field-by-field snapshot difference (null = same).</summary>
        private static string Diff(RunSnapshot a, RunSnapshot b)
        {
            if (a.Day != b.Day || a.Money != b.Money || a.AttemptsLeft != b.AttemptsLeft || a.PopularModelId != b.PopularModelId
                || a.FailureCount != b.FailureCount || a.TotalSales != b.TotalSales || a.TotalPaperCost != b.TotalPaperCost
                || a.EndReason != b.EndReason || a.EndingId != b.EndingId || a.IsFolding != b.IsFolding
                || !a.Completions.Equals(b.Completions)
                || string.Join(",", ToArray(a.UnlockedModelIds)) != string.Join(",", ToArray(b.UnlockedModelIds))
                || a.TodayPieces.Count != b.TodayPieces.Count)
                return "snapshot changed (scalar fields)";
            for (int i = 0; i < a.TodayPieces.Count; i++) if (!a.TodayPieces[i].Equals(b.TodayPieces[i])) return "snapshot changed (today)";
            if (a.Fold != null)
            {
                var x = a.Fold; var y = b.Fold;
                if (x.ModelId != y.ModelId || x.Progress != y.Progress || x.Mistakes != y.Mistakes || x.Hints != y.Hints
                    || x.RevealedAction != y.RevealedAction || x.Penalty != y.Penalty || x.ExpectedGrade != y.ExpectedGrade)
                    return "snapshot changed (fold)";
            }
            return null;
        }

        private static string[] ToArray(IReadOnlyList<string> list)
        {
            var arr = new string[list.Count];
            for (int i = 0; i < arr.Length; i++) arr[i] = list[i];
            return arr;
        }

        [Test]
        public void EndingRules_MatchesReferenceModel_OnGeneratedInputs()
        {
            // R-014 exhaustively around every boundary: money × crane totals × ★3 coverage patterns × reason.
            int[] moneys = { 0, 1, 19, 20, 21, 500, 998, 999, 1000, 1001, 1709, 5000 };
            int[] cranes = { 0, 1, 14, 15, 16, 29 };
            int checkedCases = 0;
            foreach (var reason in new[] { RunEndReason.Completed, RunEndReason.Bankrupt })
            foreach (int money in moneys)
            foreach (int crane in cranes)
            for (int mask = 0; mask < 32; mask++)       // which models have a ★3
            for (int lowGrade = 1; lowGrade <= 2; lowGrade++) // grade used for the crane bulk and the non-★3 models
            {
                var refCounts = new int[5, 4];
                var counts = CompletionCounts.Empty;
                for (int m = 0; m < 5; m++)
                {
                    string id = RunReferenceModel.Ids[m];
                    int star3 = (mask & (1 << m)) != 0 ? 1 : 0;
                    // crane: total exactly `crane`; other models: ★3 marker or 2 lower-grade pieces.
                    int low = m == 3 ? crane - star3 : (star3 == 1 ? 0 : 2);
                    if (low < 0) { low = 0; star3 = 0; }
                    refCounts[m, 3] = star3;
                    refCounts[m, lowGrade] = low;
                    counts = counts.With(id, Grade.Star3, star3).With(id, (Grade)lowGrade, low);
                }

                string expected = RunReferenceModel.EndingFor(reason.ToString(), money, refCounts);
                string actual = EndingRules.Evaluate(reason, money, counts);
                Assert.AreEqual(expected, actual, reason + " money " + money + " crane " + crane + " mask " + mask + " low " + lowGrade);
                checkedCases++;
            }
            Assert.Greater(checkedCases, 4000);
        }

        [Test]
        public void Pricing_MatchesReferenceFormula_ForEveryModelGradeAndDay()
        {
            for (int m = 0; m < 5; m++)
            for (int g = 1; g <= 3; g++)
            for (int day = 1; day <= 7; day++)
                Assert.AreEqual(RunReferenceModel.Price(m, g, day), Pricing.SalePrice(RunReferenceModel.Ids[m], (Grade)g, day),
                    RunReferenceModel.Ids[m] + " ★" + g + " day " + day);

            // Base-price form: floor semantics against exact rational arithmetic (base × gp × dp vs 10000·q).
            for (int b = 0; b <= 500; b++)
            foreach (var g in new[] { Grade.Star1, Grade.Star2, Grade.Star3 })
            foreach (bool popular in new[] { false, true })
            {
                int gp = g == Grade.Star3 ? 150 : g == Grade.Star2 ? 100 : 60;
                int dp = popular ? 150 : 100;
                int q = Pricing.SalePrice(b, g, popular);
                long num = (long)b * gp * dp;
                Assert.IsTrue(q * 10000L <= num && num < (q + 1) * 10000L, "floor b=" + b + " " + g + " " + popular + " → " + q);
            }
        }
    }
}
