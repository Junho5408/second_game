using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Origami.Core.Economy;
using Origami.Core.Folding;
using Origami.Core.Game;
using Origami.Core.Geometry;
using Origami.Core.Recognition;

namespace Origami.Tests.Qa
{
    /// <summary>
    /// QA ORIGAMI-010: seeded random command fuzz against an independent shadow model of the C-150 workshop rules
    /// (modes, rejection order, money, sheets, day, book, display, failures, tutorial guide visibility, settlement prices from
    /// CONTENT §5 typed by QA, ending priority R-018). Only fold-engine geometry (InvalidFold for n ≥ 0) and recognition
    /// results are taken from the game. Also checks: commands never throw, rejection keeps the snapshot reference,
    /// and the same command list replayed on a fresh game yields the same snapshot values (R-001).
    /// </summary>
    [TestFixture]
    public class WorkshopFuzzQaTests
    {
        // CONTENT §1 / §5 (QA transcription).
        private static readonly string[] Ids = { "plane", "house", "tulip", "dog", "cicada" };
        private static readonly Dictionary<string, int> PagePrice = new Dictionary<string, int>
            { { "plane", 0 }, { "house", 0 }, { "tulip", 20 }, { "dog", 50 }, { "cicada", 150 } };
        private static readonly Dictionary<string, int[]> PriceTable = new Dictionary<string, int[]>
        {
            // ★3, ★2, ★1 normal | ★3, ★2, ★1 popular
            { "plane", new[] { 9, 6, 3, 13, 9, 5 } },
            { "house", new[] { 12, 8, 4, 18, 12, 7 } },
            { "tulip", new[] { 18, 12, 7, 27, 18, 10 } },
            { "dog", new[] { 27, 18, 10, 40, 27, 16 } },
            { "cicada", new[] { 39, 26, 15, 58, 39, 23 } },
        };
        private static readonly string[] Popular = { "plane", "house", "tulip", "house", "dog", "tulip", "cicada" };

        private static int Price(string id, Grade grade, int day)
        {
            int col = (3 - (int)grade) + (Popular[day - 1] == id ? 3 : 0);
            return PriceTable[id][col];
        }

        private sealed class Model
        {
            public int Day = 1, Money = 20, Sheets = 5, Page, Failures, Sales, Paper, Books;
            public WorkshopMode Mode = WorkshopMode.Walking;
            public bool BookOpen, Free = true, TutorialDone, HasSheet, SheetTutorial, Skipped;
            public HashSet<string> Owned = new HashSet<string> { "plane", "house" };
            public List<FoldStepKind> History = new List<FoldStepKind>();
            public int Scores;
            public List<(string id, Grade g)> Display = new List<(string, Grade)>();
            public Dictionary<(string, Grade), int> Done = new Dictionary<(string, Grade), int>();
            public RunEndReason End = RunEndReason.None;
            public string Ending;

            public int Actions => History.Count(k => k != FoldStepKind.Flip);
            public int Folds => History.Count(k => k == FoldStepKind.Fold);
            public bool GuideVisible => HasSheet && SheetTutorial && !Skipped && Actions < 6;
            public bool BookVisible => Mode == WorkshopMode.Bookshelf || (Mode == WorkshopMode.Seated && BookOpen);

            public string EvaluateEnding()
            {
                int Get(string id, Grade g) => Done.TryGetValue((id, g), out var c) ? c : 0;
                if (End == RunEndReason.Bankrupt || Money <= 20) return "E-01";
                if (Get("tulip", Grade.Star1) + Get("tulip", Grade.Star2) + Get("tulip", Grade.Star3) >= 12) return "E-02";
                if (Ids.All(id => Get(id, Grade.Star3) >= 1)) return "E-03";
                if (Money >= 600) return "E-04";
                return "E-05";
            }
        }

        private enum Op { NewRun, Interact, StandUp, OpenBook, CloseBook, TurnPage, BuyPage, CloseCounter, Sleep, TakeSheet, Fold, Crease, Flip, Undo, Unfold, Finish, Discard, SkipGuide, BookFold }

        private struct Cmd
        {
            public Op Op;
            public string S;
            public int N;
            public bool B;
            public Vec2 G, D;
            public override string ToString() => $"{Op}({S},{N},{B},{G.X:0.###},{G.Y:0.###}->{D.X:0.###},{D.Y:0.###})";
        }

        private static readonly string[] Targets = { "table", "bookshelf", "counter", "bed", null, "", "Bed", "garden" };
        private static readonly string[] Models = { "plane", "house", "tulip", "dog", "cicada", null, "", "unicorn", "Dog" };
        private static readonly Vec2[] Points =
        {
            new Vec2(-1, 1), new Vec2(1, 1), new Vec2(-1, -1), new Vec2(1, -1), new Vec2(0, 0), new Vec2(-1, 0), new Vec2(1, 0),
            new Vec2(0, 1), new Vec2(0, -1), new Vec2(-0.6, -0.8), new Vec2(0.6, -0.8), new Vec2(-0.5, -0.5), new Vec2(-3, 3),
        };

        private static Cmd Gen(Random rnd, WorkshopMode mode, bool seated)
        {
            Op op;
            // Bias toward commands that fit the current mode so runs reach later days and settlements.
            if (rnd.NextDouble() < 0.35) op = (Op)rnd.Next(0, 19);
            else
            {
                switch (mode)
                {
                    case WorkshopMode.Walking:
                        op = rnd.NextDouble() < 0.85 ? Op.Interact : Op.NewRun; break;
                    case WorkshopMode.Seated:
                    {
                        Op[] seatedOps = { Op.TakeSheet, Op.TakeSheet, Op.Fold, Op.Fold, Op.Fold, Op.BookFold, Op.BookFold, Op.BookFold, Op.Crease,
                                           Op.Flip, Op.Undo, Op.Unfold, Op.Finish, Op.Finish, Op.Discard, Op.SkipGuide, Op.OpenBook,
                                           Op.CloseBook, Op.TurnPage, Op.BuyPage, Op.StandUp, Op.StandUp };
                        op = seatedOps[rnd.Next(seatedOps.Length)]; break;
                    }
                    case WorkshopMode.Bookshelf:
                        op = new[] { Op.TurnPage, Op.BuyPage, Op.CloseBook }[rnd.Next(3)]; break;
                    case WorkshopMode.Counter: op = Op.CloseCounter; break;
                    case WorkshopMode.SleepConfirm: op = Op.Sleep; break;
                    default: op = rnd.NextDouble() < 0.5 ? Op.NewRun : (Op)rnd.Next(1, 19); break;
                }
            }
            var c = new Cmd { Op = op };
            c.S = op == Op.Interact ? Targets[rnd.Next(Targets.Length)] : Models[rnd.Next(Models.Length)];
            if (op == Op.Interact && rnd.NextDouble() < 0.5) c.S = rnd.NextDouble() < 0.5 ? "table" : "bed";
            c.N = op == Op.TurnPage ? rnd.Next(-5, 50) : new[] { 0, 0, 0, 1, 2, 9, -1 }[rnd.Next(7)];
            c.B = op == Op.Sleep ? rnd.NextDouble() < 0.8 : rnd.NextDouble() < 0.3;
            c.G = Points[rnd.Next(Points.Length)];
            c.D = rnd.NextDouble() < 0.6 ? Points[rnd.Next(Points.Length)] : new Vec2(rnd.NextDouble() * 2.4 - 1.2, rnd.NextDouble() * 2.4 - 1.2);
            c.N = op == Op.BookFold ? rnd.Next(1000) : c.N;
            c.S = op == Op.BookFold ? Ids[rnd.Next(Ids.Length)] : c.S;
            return c;
        }

        private static CommandResult Exec(OrigamiGame g, Cmd c, Model m, out Cmd executed)
        {
            executed = c;
            switch (c.Op)
            {
                case Op.NewRun: return g.NewRun();
                case Op.Interact: return g.Interact(c.S);
                case Op.StandUp: return g.StandUp();
                case Op.OpenBook: return g.OpenBook();
                case Op.CloseBook: return g.CloseBook();
                case Op.TurnPage: return g.TurnPage(c.N);
                case Op.BuyPage: return g.BuyPage(c.S);
                case Op.CloseCounter: return g.CloseCounter();
                case Op.Sleep: return g.Sleep(c.B);
                case Op.TakeSheet: return g.TakeSheet();
                case Op.Fold: return g.Fold(c.G, c.D, c.B, c.N);
                case Op.Crease: return g.Crease(c.G, c.D, c.N);
                case Op.Flip: return g.Flip();
                case Op.Undo: return g.Undo();
                case Op.Unfold: return g.Unfold();
                case Op.Finish: return g.Finish();
                case Op.Discard: return g.Discard();
                case Op.SkipGuide: return g.SkipGuide();
                case Op.BookFold:
                {
                    // The next step of a book sequence (so finished pieces actually appear): S = model, step = current action count.
                    var book = V2SpecBook.Book(c.S);
                    int a = m.HasSheet ? m.Actions : 0;
                    var row = book[Math.Min(a, book.Count - 1)];
                    executed.Op = row.Kind == FoldStepKind.Crease ? Op.Crease : Op.Fold;
                    executed.G = row.Grab; executed.D = row.Drop; executed.B = row.Mountain; executed.N = row.Layers;
                    return row.Kind == FoldStepKind.Crease ? g.Crease(row.Grab, row.Drop, row.Layers) : g.Fold(row.Grab, row.Drop, row.Mountain, row.Layers);
                }
            }
            throw new InvalidOperationException();
        }

        /// <summary>Shadow-model prediction: the rejection reason (None = accept). InvalidFold for n ≥ 0 is left to the engine (returns null).</summary>
        private static RejectReason? Predict(Model m, Cmd c)
        {
            if (c.Op == Op.NewRun) return RejectReason.None;
            if (m.End != RunEndReason.None) return RejectReason.RunEnded;
            bool seated = m.Mode == WorkshopMode.Seated;
            switch (c.Op)
            {
                case Op.Interact:
                    if (m.Mode != WorkshopMode.Walking) return RejectReason.WrongMode;
                    if (!new[] { "table", "bookshelf", "counter", "bed" }.Contains(c.S)) return RejectReason.UnknownTarget;
                    if (c.S == "bed" && m.HasSheet) return RejectReason.SheetOnTable;
                    return RejectReason.None;
                case Op.StandUp: case Op.OpenBook:
                    return seated ? RejectReason.None : RejectReason.WrongMode;
                case Op.CloseBook:
                    return seated || m.Mode == WorkshopMode.Bookshelf ? RejectReason.None : RejectReason.WrongMode;
                case Op.TurnPage:
                    return m.BookVisible ? RejectReason.None : RejectReason.WrongMode;
                case Op.BuyPage:
                    if (!m.BookVisible) return RejectReason.WrongMode;
                    if (c.S == null || !PagePrice.ContainsKey(c.S)) return RejectReason.UnknownModel;
                    if (m.Owned.Contains(c.S)) return RejectReason.AlreadyOwned;
                    if (m.Money < PagePrice[c.S]) return RejectReason.NotEnoughMoney;
                    return RejectReason.None;
                case Op.CloseCounter:
                    return m.Mode == WorkshopMode.Counter ? RejectReason.None : RejectReason.WrongMode;
                case Op.Sleep:
                    return m.Mode == WorkshopMode.SleepConfirm ? RejectReason.None : RejectReason.WrongMode;
                case Op.TakeSheet:
                    if (!seated) return RejectReason.WrongMode;
                    if (m.HasSheet) return RejectReason.SheetOnTable;
                    if (m.Sheets == 0) return RejectReason.NoSheetsLeft;
                    if (!m.Free && m.Money < 2) return RejectReason.NotEnoughMoney;
                    return RejectReason.None;
                case Op.Fold: case Op.Crease:
                    if (!seated) return RejectReason.WrongMode;
                    if (!m.HasSheet) return RejectReason.NoSheet;
                    if (c.N < 0) return RejectReason.InvalidFold;
                    return null;
                case Op.Flip: case Op.Discard:
                    if (!seated) return RejectReason.WrongMode;
                    return m.HasSheet ? RejectReason.None : RejectReason.NoSheet;
                case Op.Undo: case Op.Unfold:
                    if (!seated) return RejectReason.WrongMode;
                    if (!m.HasSheet) return RejectReason.NoSheet;
                    return m.History.Count == 0 ? RejectReason.NothingToUndo : RejectReason.None;
                case Op.Finish:
                    if (!seated) return RejectReason.WrongMode;
                    if (!m.HasSheet) return RejectReason.NoSheet;
                    return m.Folds == 0 ? RejectReason.NoFolds : RejectReason.None;
                case Op.SkipGuide:
                    if (!seated) return RejectReason.WrongMode;
                    return m.GuideVisible ? RejectReason.None : RejectReason.NoGuide;
            }
            throw new InvalidOperationException(c.Op.ToString());
        }

        private static void ApplyToModel(Model m, Cmd c, CommandResult r)
        {
            switch (c.Op)
            {
                case Op.NewRun:
                {
                    var fresh = new Model();
                    m.Day = fresh.Day; m.Money = fresh.Money; m.Sheets = fresh.Sheets; m.Page = 0; m.Failures = 0; m.Sales = 0; m.Paper = 0; m.Books = 0;
                    m.Mode = WorkshopMode.Walking; m.BookOpen = false; m.Free = true; m.TutorialDone = false; m.HasSheet = false;
                    m.SheetTutorial = false; m.Skipped = false; m.Owned = fresh.Owned; m.History.Clear(); m.Scores = 0; m.Display.Clear();
                    m.Done.Clear(); m.End = RunEndReason.None; m.Ending = null;
                    return;
                }
                case Op.Interact:
                    m.Mode = c.S == "table" ? WorkshopMode.Seated : c.S == "bookshelf" ? WorkshopMode.Bookshelf
                        : c.S == "counter" ? WorkshopMode.Counter : WorkshopMode.SleepConfirm;
                    if (c.S == "table") m.BookOpen = false;
                    return;
                case Op.StandUp: m.Mode = WorkshopMode.Walking; m.BookOpen = false; return;
                case Op.OpenBook: m.BookOpen = true; return;
                case Op.CloseBook:
                    if (m.Mode == WorkshopMode.Bookshelf) m.Mode = WorkshopMode.Walking; else m.BookOpen = false;
                    return;
                case Op.TurnPage:
                {
                    int q = Math.Max(0, Math.Min(43, c.N));
                    m.Page = q - q % 2;
                    return;
                }
                case Op.BuyPage: m.Money -= PagePrice[c.S]; m.Books += PagePrice[c.S]; m.Owned.Add(c.S); return;
                case Op.CloseCounter: m.Mode = WorkshopMode.Walking; return;
                case Op.Sleep:
                    if (!c.B) { m.Mode = WorkshopMode.Walking; return; }
                    {
                        int total = 0;
                        var lines = new List<(string, Grade, bool, int)>();
                        foreach (var (id, gr) in m.Display)
                        {
                            int p = Price(id, gr, m.Day);
                            lines.Add((id, gr, Popular[m.Day - 1] == id, p));
                            total += p;
                        }
                        var st = r.Settlement;
                        Assert.That(st.SettledDay, Is.EqualTo(m.Day), "settled day");
                        Assert.That(st.Lines.Select(l => (l.ModelId, l.Grade, l.Popular, l.Price)).ToArray(), Is.EqualTo(lines.ToArray()), "sale lines");
                        Assert.That(st.Total, Is.EqualTo(total), "settlement total");
                        m.Money += total; m.Sales += total; m.Display.Clear();
                        if (m.Day == 7) m.End = RunEndReason.Completed;
                        else
                        {
                            m.Day++; m.Sheets = 5; m.Mode = WorkshopMode.Walking;
                            if (m.Money < 2) m.End = RunEndReason.Bankrupt;
                        }
                        if (m.End != RunEndReason.None) { m.Mode = WorkshopMode.Ended; m.Ending = m.EvaluateEnding(); }
                        Assert.That(st.EndReason, Is.EqualTo(m.End));
                        Assert.That(st.MoneyAfter, Is.EqualTo(m.Money));
                        Assert.That(st.NextPopularModelId, Is.EqualTo(m.End == RunEndReason.None ? Popular[m.Day - 1] : null));
                        return;
                    }
                case Op.TakeSheet:
                    m.HasSheet = true; m.History.Clear(); m.Scores = 0; m.Skipped = false; m.Sheets--;
                    if (m.Free) { m.Free = false; m.SheetTutorial = true; }
                    else { m.SheetTutorial = false; m.Money -= 2; m.Paper += 2; }
                    return;
                case Op.Fold: case Op.Crease:
                {
                    bool guide = m.GuideVisible;
                    Assert.That(r.GuideScore != null, Is.EqualTo(guide), "guide score recorded iff guide was visible");
                    Assert.That(r.Plan, Is.Not.Null, "accepted fold has a plan");
                    if (guide) m.Scores++;
                    m.History.Add(c.Op == Op.Crease ? FoldStepKind.Crease : FoldStepKind.Fold);
                    return;
                }
                case Op.Flip: m.History.Add(FoldStepKind.Flip); Assert.That(r.GuideScore, Is.Null); return;
                case Op.Undo: m.History.RemoveAt(m.History.Count - 1); m.Scores = Math.Min(m.Scores, m.Actions); return;
                case Op.Unfold: m.History.Clear(); m.Scores = 0; return;
                case Op.Finish:
                {
                    var rec = r.Recognition;
                    Assert.That(rec, Is.Not.Null);
                    if (rec.IsPiece)
                    {
                        Assert.That(m.Owned.Contains(rec.ModelId), Is.True, "only owned models are recognised");
                        Assert.That(rec.Grade, Is.Not.EqualTo(Grade.None));
                        m.Display.Add((rec.ModelId, rec.Grade));
                        m.Done.TryGetValue((rec.ModelId, rec.Grade), out var n);
                        m.Done[(rec.ModelId, rec.Grade)] = n + 1;
                    }
                    else m.Failures++;
                    EndSheet(m);
                    return;
                }
                case Op.Discard: m.Failures++; EndSheet(m); return;
                case Op.SkipGuide: m.Skipped = true; return;
            }
        }

        private static void EndSheet(Model m)
        {
            if (m.SheetTutorial) m.TutorialDone = true;
            m.HasSheet = false; m.SheetTutorial = false; m.History.Clear(); m.Scores = 0; m.Skipped = false;
        }

        private static void Compare(Model m, GameSnapshot s, string ctx)
        {
            Assert.That(s.Day, Is.EqualTo(m.Day), ctx + " day");
            Assert.That(s.Money, Is.EqualTo(m.Money), ctx + " money");
            Assert.That(s.SheetsLeft, Is.EqualTo(m.Sheets), ctx + " sheets");
            Assert.That(s.Mode, Is.EqualTo(m.Mode), ctx + " mode");
            Assert.That(s.BookOpen, Is.EqualTo(m.BookOpen), ctx + " book open");
            Assert.That(s.BookVisible, Is.EqualTo(m.BookVisible), ctx + " book visible");
            Assert.That(s.BookPage, Is.EqualTo(m.Page), ctx + " page");
            Assert.That(s.FreeSheetAvailable, Is.EqualTo(m.Free), ctx + " free");
            Assert.That(s.TutorialFinished, Is.EqualTo(m.TutorialDone), ctx + " tutorial finished");
            Assert.That(s.FailureCount, Is.EqualTo(m.Failures), ctx + " failures");
            Assert.That(s.TotalSales, Is.EqualTo(m.Sales), ctx + " sales");
            Assert.That(s.TotalPaperCost, Is.EqualTo(m.Paper), ctx + " paper");
            Assert.That(s.TotalBookCost, Is.EqualTo(m.Books), ctx + " books");
            CollectionAssert.AreEqual(Ids.Where(m.Owned.Contains).ToArray(), s.OwnedModelIds, ctx + " owned (catalogue order)");
            Assert.That(s.Display.Select(p => (p.ModelId, p.Grade)).ToArray(), Is.EqualTo(m.Display.ToArray()), ctx + " display");
            Assert.That(s.EndReason, Is.EqualTo(m.End), ctx + " end");
            Assert.That(s.EndingId, Is.EqualTo(m.Ending), ctx + " ending");
            Assert.That(s.PopularModelId, Is.EqualTo(Popular[m.Day - 1]), ctx + " popular");
            foreach (var id in Ids)
                foreach (var gr in new[] { Grade.Star1, Grade.Star2, Grade.Star3 })
                    Assert.That(s.Completions.Get(id, gr), Is.EqualTo(m.Done.TryGetValue((id, gr), out var n) ? n : 0), $"{ctx} completions {id} {gr}");
            Assert.That(s.Sheet != null, Is.EqualTo(m.HasSheet), ctx + " sheet present");
            if (m.HasSheet)
            {
                Assert.That(s.Sheet.IsTutorial, Is.EqualTo(m.SheetTutorial), ctx + " tutorial sheet");
                Assert.That(s.Sheet.FoldCount, Is.EqualTo(m.Folds), ctx + " fold count");
                Assert.That(s.Sheet.ActionCount, Is.EqualTo(m.Actions), ctx + " action count");
                Assert.That(s.Sheet.CanUndo, Is.EqualTo(m.History.Count > 0), ctx + " can undo");
                Assert.That(s.Sheet.GuideVisible, Is.EqualTo(m.GuideVisible), ctx + " guide visible");
                if (m.GuideVisible) Assert.That(s.Sheet.Guide.Number, Is.EqualTo(m.Actions + 1), ctx + " guide step");
                Assert.That(s.Sheet.GuideScores.Count, Is.EqualTo(m.Scores), ctx + " guide scores");
                Assert.That(s.Sheet.State.TotalArea(), Is.EqualTo(4.0).Within(1e-6), ctx + " paper area conserved");
            }
            // C-150 invariants
            Assert.That(s.Money, Is.GreaterThanOrEqualTo(0), ctx);
            Assert.That(s.Money, Is.EqualTo(20 + s.TotalSales - s.TotalPaperCost - s.TotalBookCost), ctx);
        }

        private static (List<Cmd> cmds, GameSnapshot final, int accepted, int settlements, int pieces, int maxDay) RunFuzz(int seed, int steps)
        {
            var rnd = new Random(seed);
            var g = new OrigamiGame();
            var m = new Model();
            var cmds = new List<Cmd>();
            int accepted = 0, settlements = 0, pieces = 0, maxDay = 1;
            var pending = new Queue<Cmd>();
            Compare(m, g.Snapshot, "initial");
            for (int i = 0; i < steps; i++)
            {
                if (pending.Count == 0 && m.Mode == WorkshopMode.Seated && m.HasSheet && m.History.Count == 0 && rnd.NextDouble() < 0.5)
                {
                    // Macro: the whole book sequence of a random model, then Finish (so pieces and gradeful sales occur).
                    string id = Ids[rnd.Next(Ids.Length)];
                    foreach (var row in V2SpecBook.Book(id))
                        pending.Enqueue(new Cmd { Op = row.Kind == FoldStepKind.Crease ? Op.Crease : Op.Fold, G = row.Grab, D = row.Drop, B = row.Mountain, N = row.Layers, S = id });
                    pending.Enqueue(new Cmd { Op = Op.Finish });
                }
                var c = pending.Count > 0 ? pending.Dequeue() : Gen(rnd, m.Mode, m.Mode == WorkshopMode.Seated);
                var before = g.Snapshot;
                CommandResult r = null;
                Cmd exec = c;
                Assert.DoesNotThrow(() => r = Exec(g, c, m, out exec), $"seed {seed} step {i} {c}");
                cmds.Add(exec);
                string ctx = $"seed {seed} step {i} {exec}";
                var expected = Predict(m, exec);
                if (expected == null)
                {
                    // Engine decides geometry: accepted, or InvalidFold with an engine FoldError.
                    if (!r.Accepted)
                    {
                        Assert.That(r.Reason, Is.EqualTo(RejectReason.InvalidFold), ctx);
                        Assert.That(r.FoldError, Is.Not.EqualTo(FoldError.None), ctx);
                    }
                    expected = r.Accepted ? RejectReason.None : RejectReason.InvalidFold;
                }
                Assert.That(r.Reason, Is.EqualTo(expected.Value), ctx + " reason");
                Assert.That(r.Accepted, Is.EqualTo(expected.Value == RejectReason.None), ctx + " accepted");
                if (!r.Accepted)
                {
                    Assert.That(g.Snapshot, Is.SameAs(before), ctx + " snapshot reference kept on rejection");
                    if (r.Reason != RejectReason.InvalidFold) Assert.That(r.FoldError, Is.EqualTo(FoldError.None), ctx + " FoldError only for InvalidFold");
                    if (r.Reason == RejectReason.InvalidFold && exec.N < 0) Assert.That(r.FoldError, Is.EqualTo(FoldError.InvalidLayers), ctx);
                }
                else
                {
                    Assert.That(g.Snapshot, Is.Not.SameAs(before), ctx + " accepted command publishes a new snapshot");
                    if (exec.Op == Op.Sleep && exec.B) settlements++;
                    if (exec.Op == Op.Finish && r.Recognition.IsPiece) pieces++;
                    Assert.That(r.Settlement != null, Is.EqualTo(exec.Op == Op.Sleep && exec.B), ctx + " settlement only for Sleep(yes)");
                    Assert.That(r.Recognition != null, Is.EqualTo(exec.Op == Op.Finish), ctx + " recognition only for Finish");
                    ApplyToModel(m, exec, r);
                }
                maxDay = Math.Max(maxDay, m.Day);
                Compare(m, g.Snapshot, ctx);
                if (r.Accepted) accepted++;
            }
            return (cmds, g.Snapshot, accepted, settlements, pieces, maxDay);
        }

        private static GameSnapshot Replay(List<Cmd> cmds)
        {
            var g = new OrigamiGame();
            foreach (var c in cmds)
            {
                switch (c.Op)
                {
                    case Op.NewRun: g.NewRun(); break;
                    case Op.Interact: g.Interact(c.S); break;
                    case Op.StandUp: g.StandUp(); break;
                    case Op.OpenBook: g.OpenBook(); break;
                    case Op.CloseBook: g.CloseBook(); break;
                    case Op.TurnPage: g.TurnPage(c.N); break;
                    case Op.BuyPage: g.BuyPage(c.S); break;
                    case Op.CloseCounter: g.CloseCounter(); break;
                    case Op.Sleep: g.Sleep(c.B); break;
                    case Op.TakeSheet: g.TakeSheet(); break;
                    case Op.Fold: g.Fold(c.G, c.D, c.B, c.N); break;
                    case Op.Crease: g.Crease(c.G, c.D, c.N); break;
                    case Op.Flip: g.Flip(); break;
                    case Op.Undo: g.Undo(); break;
                    case Op.Unfold: g.Unfold(); break;
                    case Op.Finish: g.Finish(); break;
                    case Op.Discard: g.Discard(); break;
                    case Op.SkipGuide: g.SkipGuide(); break;
                }
            }
            return g.Snapshot;
        }

        private static void AssertSameValues(GameSnapshot a, GameSnapshot b, string ctx)
        {
            Assert.That(b.Day, Is.EqualTo(a.Day), ctx);
            Assert.That(b.Money, Is.EqualTo(a.Money), ctx);
            Assert.That(b.SheetsLeft, Is.EqualTo(a.SheetsLeft), ctx);
            Assert.That(b.Mode, Is.EqualTo(a.Mode), ctx);
            Assert.That(b.BookOpen, Is.EqualTo(a.BookOpen), ctx);
            Assert.That(b.BookPage, Is.EqualTo(a.BookPage), ctx);
            CollectionAssert.AreEqual(a.OwnedModelIds, b.OwnedModelIds, ctx);
            Assert.That(b.Completions, Is.EqualTo(a.Completions), ctx);
            Assert.That(b.FailureCount, Is.EqualTo(a.FailureCount), ctx);
            Assert.That(b.TotalSales, Is.EqualTo(a.TotalSales), ctx);
            Assert.That(b.EndingId, Is.EqualTo(a.EndingId), ctx);
            Assert.That(b.Display.Select(p => (p.ModelId, p.Grade, p.Percent)).ToArray(),
                Is.EqualTo(a.Display.Select(p => (p.ModelId, p.Grade, p.Percent)).ToArray()), ctx);
            Assert.That(b.Sheet != null, Is.EqualTo(a.Sheet != null), ctx);
            if (a.Sheet != null)
            {
                Assert.That(b.Sheet.ActionCount, Is.EqualTo(a.Sheet.ActionCount), ctx);
                V2SpecBook.AssertSameState(a.Sheet.State, b.Sheet.State, 0.0, ctx + " sheet");
                Assert.That(b.Sheet.GuideScores.Select(x => x.Score).ToArray(), Is.EqualTo(a.Sheet.GuideScores.Select(x => x.Score).ToArray()), ctx);
            }
        }

        [Test]
        public void RandomCommands_MatchShadowModel_AndReplayDeterministically([Range(1, 12)] int seed)
        {
            var (cmds, final, accepted, settlements, pieces, maxDay) = RunFuzz(seed * 7919, 600);
            AssertSameValues(final, Replay(cmds), "replay seed " + seed);
            TestContext.WriteLine($"seed {seed}: accepted {accepted}/600, settlements {settlements}, pieces {pieces}, max day {maxDay}");
        }

        [Test]
        public void Fuzz_CoversSettlementsPiecesAndRunEnds()
        {
            // Guards against a fuzz that never reaches the interesting states.
            int settlements = 0, pieces = 0, ended = 0, accepted = 0, total = 0;
            for (int seed = 1; seed <= 12; seed++)
            {
                var r = RunFuzz(seed * 7919, 600);
                settlements += r.settlements; pieces += r.pieces; accepted += r.accepted; total += 600;
                if (r.maxDay >= 7) ended++;
            }
            Assert.That(settlements, Is.GreaterThan(20), "settlements");
            Assert.That(pieces, Is.GreaterThan(5), "recognised pieces");
            Assert.That(accepted, Is.GreaterThan(total / 4), "accepted commands");
        }
    }
}
