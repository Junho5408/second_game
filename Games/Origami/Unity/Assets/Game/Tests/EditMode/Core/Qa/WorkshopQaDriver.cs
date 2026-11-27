using System;
using System.Linq;
using NUnit.Framework;
using Origami.Core.Folding;
using Origami.Core.Game;
using Origami.Core.Geometry;
using Origami.Core.Recognition;

namespace Origami.Tests.Qa
{
    /// <summary>
    /// QA ORIGAMI-010: black-box command driver for <see cref="OrigamiGame"/> written by QA from FEATURE_SPECS "공통 표기"
    /// (Sit, Stand, Ref, Var, Waste, Night). Folding sequences come from the QA transcription of CONTENT §2 (<see cref="V2SpecBook"/>),
    /// not from ModelCatalog or the developer's GameScenarios. Every command is followed by an invariant check (C-150 / R-020),
    /// and every rejection is checked to leave the snapshot reference unchanged (R-019).
    /// </summary>
    internal static class WsDriver
    {
        public const string Table = "table", Bookshelf = "bookshelf", Counter = "counter", Bed = "bed";

        public static Vec2 V(double x, double y) => new Vec2(x, y);

        /// <summary>C-150 invariants: money ≥ 0, R-020 money identity, 0 ≤ sheets ≤ 5, 1 ≤ day ≤ 7, even book page.</summary>
        public static void Inv(OrigamiGame g, string ctx = "")
        {
            var s = g.Snapshot;
            Assert.That(s.Money, Is.GreaterThanOrEqualTo(0), ctx + " money >= 0");
            Assert.That(s.Money, Is.EqualTo(20 + s.TotalSales - s.TotalPaperCost - s.TotalBookCost), ctx + " R-020 identity");
            Assert.That(s.SheetsLeft, Is.InRange(0, 5), ctx + " sheets");
            Assert.That(s.Day, Is.InRange(1, 7), ctx + " day");
            Assert.That(s.BookPage % 2, Is.EqualTo(0), ctx + " even page");
            Assert.That(s.BookPage, Is.InRange(0, 42), ctx + " page range");
            Assert.That(s.IsEnded, Is.EqualTo(s.Mode == WorkshopMode.Ended), ctx + " ended <=> mode Ended");
            Assert.That(s.EndingId == null, Is.EqualTo(!s.IsEnded), ctx + " ending only when ended");
        }

        public static CommandResult Ok(OrigamiGame g, Func<OrigamiGame, CommandResult> cmd, string ctx = "")
        {
            var r = cmd(g);
            Assert.That(r.Accepted, Is.True, $"{ctx} expected accepted, got {r.Reason} / {r.FoldError}");
            Assert.That(r.Reason, Is.EqualTo(RejectReason.None), ctx);
            Inv(g, ctx);
            return r;
        }

        public static CommandResult Rej(OrigamiGame g, Func<OrigamiGame, CommandResult> cmd, RejectReason reason, string ctx = "")
        {
            var before = g.Snapshot;
            var r = cmd(g);
            Assert.That(r.Accepted, Is.False, ctx + " expected rejection " + reason);
            Assert.That(r.Reason, Is.EqualTo(reason), ctx);
            Assert.That(g.Snapshot, Is.SameAs(before), ctx + " rejection must not replace the snapshot (C-150)");
            Assert.That(r.Settlement, Is.Null, ctx);
            Assert.That(r.Recognition, Is.Null, ctx);
            Assert.That(r.Plan, Is.Null, ctx);
            Assert.That(r.GuideScore, Is.Null, ctx);
            if (reason != RejectReason.InvalidFold) Assert.That(r.FoldError, Is.EqualTo(FoldError.None), ctx);
            return r;
        }

        public static void Sit(OrigamiGame g) => Ok(g, x => x.Interact(Table), "Sit");

        public static void Stand(OrigamiGame g) => Ok(g, x => x.StandUp(), "Stand");

        public static CommandResult Apply(OrigamiGame g, V2SpecBook.Row row, string ctx)
        {
            return row.Kind == FoldStepKind.Crease
                ? Ok(g, x => x.Crease(row.Grab, row.Drop, row.Layers), ctx)
                : Ok(g, x => x.Fold(row.Grab, row.Drop, row.Mountain, row.Layers), ctx);
        }

        /// <summary>Ref(m): TakeSheet → CONTENT §2 sequence of m → Finish. Returns the Finish recognition.</summary>
        public static RecognitionResult Ref(OrigamiGame g, string id)
        {
            Ok(g, x => x.TakeSheet(), "Ref TakeSheet " + id);
            int i = 0;
            foreach (var row in V2SpecBook.Book(id)) Apply(g, row, $"Ref {id} step {++i}");
            return Ok(g, x => x.Finish(), "Ref Finish " + id).Recognition;
        }

        /// <summary>Ref(m) that must give m ★3 100% (m owned).</summary>
        public static void Ref3(OrigamiGame g, string id)
        {
            var rec = Ref(g, id);
            Assert.That(rec.ModelId, Is.EqualTo(id), "Ref " + id);
            Assert.That(rec.Grade, Is.EqualTo(Grade.Star3), "Ref " + id);
            Assert.That(rec.Percent, Is.EqualTo(100), "Ref " + id);
        }

        /// <summary>Var(dy) of FEATURE_SPECS 공통 표기 (house roof folded by hand without creases).</summary>
        public static RecognitionResult Var(OrigamiGame g, double dy)
        {
            Ok(g, x => x.TakeSheet(), "Var take");
            Ok(g, x => x.Fold(V(-1, 1), V(-1, -1), false, 0), "Var f1");
            Ok(g, x => x.Fold(V(-1, 0), V(-0.6, -0.8 + dy), false, 0), "Var f2");
            Ok(g, x => x.Fold(V(1, 0), V(0.6, -0.8 + dy), false, 0), "Var f3");
            return Ok(g, x => x.Finish(), "Var finish").Recognition;
        }

        public static void Waste(OrigamiGame g)
        {
            Ok(g, x => x.TakeSheet(), "Waste take");
            Ok(g, x => x.Discard(), "Waste discard");
        }

        /// <summary>Night: (Stand if seated) → Interact(bed) → Sleep(yes). Returns the settlement.</summary>
        public static SettlementResult Night(OrigamiGame g)
        {
            if (g.Snapshot.Mode == WorkshopMode.Seated) Stand(g);
            Ok(g, x => x.Interact(Bed), "Night bed");
            var r = Ok(g, x => x.Sleep(true), "Night sleep");
            Assert.That(r.Settlement, Is.Not.Null, "settlement");
            Assert.That(r.Settlement.Total, Is.EqualTo(r.Settlement.Lines.Sum(l => l.Price)), "settlement total = sum of lines");
            Assert.That(r.Settlement.MoneyAfter, Is.EqualTo(g.Snapshot.Money), "settlement money after");
            return r.Settlement;
        }

        public static void Buy(OrigamiGame g, string id)
        {
            if (g.Snapshot.Mode == WorkshopMode.Seated && !g.Snapshot.BookOpen) Ok(g, x => x.OpenBook(), "open book");
            Ok(g, x => x.BuyPage(id), "buy " + id);
        }

        public static void Times(int n, Action a)
        {
            for (int i = 0; i < n; i++) a();
        }
    }
}
