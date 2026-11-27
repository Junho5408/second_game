using System.Collections.Generic;
using NUnit.Framework;
using Origami.Core.Catalog;
using Origami.Core.Economy;
using Origami.Core.Folding;
using Origami.Core.Game;
using Origami.Core.Geometry;

namespace Origami.Tests.Game
{
    /// <summary>
    /// FEATURE_SPECS "공통 표기" shorthands over <see cref="OrigamiGame"/>. Every helper asserts acceptance and the D-120 /
    /// R-020 invariants after each command (F-001 AC-09).
    /// </summary>
    internal static class GameScenarios
    {
        public static Vec2 V(double x, double y) => new Vec2(x, y);

        /// <summary>Invariants of C-150: money ≥ 0, money = 20 + sales − paper − book, 0 ≤ sheets ≤ 5, 1 ≤ day ≤ 7, even page.</summary>
        public static void CheckInvariants(OrigamiGame game)
        {
            var s = game.Snapshot;
            Assert.GreaterOrEqual(s.Money, 0, "money ≥ 0");
            Assert.AreEqual(RuleConstants.StartMoney + s.TotalSales - s.TotalPaperCost - s.TotalBookCost, s.Money, "R-020");
            Assert.That(s.SheetsLeft, Is.InRange(0, RuleConstants.DailySheets), "sheets");
            Assert.That(s.Day, Is.InRange(1, RuleConstants.TotalDays), "day");
            Assert.AreEqual(0, s.BookPage % 2, "even page");
            Assert.AreEqual(s.EndReason != RunEndReason.None, s.IsEnded);
            Assert.AreEqual(s.IsEnded, s.EndingId != null);
            Assert.AreEqual(s.IsEnded, s.Mode == WorkshopMode.Ended);
        }

        public static CommandResult Ok(OrigamiGame game, CommandResult r, string what = "")
        {
            Assert.IsTrue(r.Accepted, "expected accepted " + what + " but " + r.Reason + "/" + r.FoldError);
            Assert.AreEqual(RejectReason.None, r.Reason);
            CheckInvariants(game);
            return r;
        }

        /// <summary>Runs the command and asserts rejection with the reason and an unchanged snapshot reference.</summary>
        public static CommandResult Rejected(OrigamiGame game, System.Func<OrigamiGame, CommandResult> cmd, RejectReason reason, string what = "")
        {
            var before = game.Snapshot;
            var r = cmd(game);
            Assert.IsFalse(r.Accepted, "expected rejected " + what);
            Assert.AreEqual(reason, r.Reason, what);
            Assert.AreSame(before, game.Snapshot, "snapshot unchanged " + what);
            Assert.IsNull(r.Plan);
            Assert.IsNull(r.Recognition);
            Assert.IsNull(r.GuideScore);
            Assert.IsNull(r.Settlement);
            CheckInvariants(game);
            return r;
        }

        public static void Sit(OrigamiGame g) => Ok(g, g.Interact(InteractTargets.Table), "Sit");
        public static void Stand(OrigamiGame g) => Ok(g, g.StandUp(), "Stand");
        public static void Take(OrigamiGame g) => Ok(g, g.TakeSheet(), "TakeSheet");

        public static CommandResult Fold(OrigamiGame g, Vec2 grab, Vec2 drop, bool mountain = false, int layers = 0) =>
            Ok(g, g.Fold(grab, drop, mountain, layers), "Fold");

        public static CommandResult Crease(OrigamiGame g, Vec2 grab, Vec2 drop, int layers = 0) => Ok(g, g.Crease(grab, drop, layers), "Crease");

        /// <summary>Applies one reference step as a game command.</summary>
        public static CommandResult Apply(OrigamiGame g, FoldStep s)
        {
            switch (s.Kind)
            {
                case FoldStepKind.Fold: return Fold(g, s.Grab, s.Drop, s.Mountain, s.Layers);
                case FoldStepKind.Crease: return Crease(g, s.Grab, s.Drop, s.Layers);
                default: return Ok(g, g.Flip(), "Flip");
            }
        }

        /// <summary>Folds the CONTENT §2 sequence of the model (1-based steps first..last) on the current sheet.</summary>
        public static List<CommandResult> FoldSteps(OrigamiGame g, string modelId, int first = 1, int last = -1)
        {
            var steps = ModelCatalog.Find(modelId).Steps;
            if (last < 0) last = steps.Count;
            var results = new List<CommandResult>();
            for (int i = first - 1; i < last; i++) results.Add(Apply(g, steps[i]));
            return results;
        }

        public static CommandResult Finish(OrigamiGame g) => Ok(g, g.Finish(), "Finish");

        /// <summary>Ref(m) = TakeSheet → §2 sequence → Finish (seated).</summary>
        public static CommandResult Ref(OrigamiGame g, string modelId)
        {
            Take(g);
            FoldSteps(g, modelId);
            return Finish(g);
        }

        /// <summary>Var(dy): house roof by hand without creases (CONTENT §4 J-04~J-06).</summary>
        public static CommandResult Var(OrigamiGame g, double dy)
        {
            Take(g);
            Fold(g, V(-1, 1), V(-1, -1));
            Fold(g, V(-1, 0), V(-0.6, -0.8 + dy));
            Fold(g, V(1, 0), V(0.6, -0.8 + dy));
            return Finish(g);
        }

        /// <summary>Waste = TakeSheet → Discard.</summary>
        public static void Waste(OrigamiGame g)
        {
            Take(g);
            Ok(g, g.Discard(), "Discard");
        }

        /// <summary>Night = (Stand if seated) → Interact(bed) → Sleep(yes). Returns the Sleep result.</summary>
        public static CommandResult Night(OrigamiGame g)
        {
            if (g.Snapshot.Mode == WorkshopMode.Seated) Stand(g);
            Ok(g, g.Interact(InteractTargets.Bed), "bed");
            return Ok(g, g.Sleep(true), "Sleep(yes)");
        }

        public static void Times(int n, System.Action a)
        {
            for (int i = 0; i < n; i++) a();
        }
    }
}
