using NUnit.Framework;
using Origami.Core.Catalog;
using Origami.Core.Folding;
using Origami.Core.Game;
using Origami.Core.Geometry;

namespace Origami.Tests.Texts
{
    /// <summary>FEATURE_SPECS "공통 표기" shorthands over <see cref="OrigamiGame"/> for the text tests (each asserts acceptance).</summary>
    internal static class TextScenarios
    {
        public static Vec2 V(double x, double y) => new Vec2(x, y);

        public static CommandResult Ok(CommandResult r, string what)
        {
            Assert.IsTrue(r.Accepted, what + " rejected: " + r.Reason + "/" + r.FoldError);
            return r;
        }

        public static void Sit(OrigamiGame g) => Ok(g.Interact(InteractTargets.Table), "Sit");
        public static void Stand(OrigamiGame g) => Ok(g.StandUp(), "Stand");
        public static void Take(OrigamiGame g) => Ok(g.TakeSheet(), "TakeSheet");

        public static void Apply(OrigamiGame g, FoldStep s)
        {
            if (s.Kind == FoldStepKind.Fold) Ok(g.Fold(s.Grab, s.Drop, s.Mountain, s.Layers), "Fold");
            else if (s.Kind == FoldStepKind.Crease) Ok(g.Crease(s.Grab, s.Drop, s.Layers), "Crease");
            else Ok(g.Flip(), "Flip");
        }

        /// <summary>Ref(m) = TakeSheet → CONTENT §2 sequence → Finish.</summary>
        public static CommandResult Ref(OrigamiGame g, string modelId)
        {
            Take(g);
            foreach (var s in ModelCatalog.Find(modelId).Steps) Apply(g, s);
            return Ok(g.Finish(), "Finish");
        }

        /// <summary>Var(dy): house roof by hand (CONTENT §4 J-04~J-06).</summary>
        public static CommandResult Var(OrigamiGame g, double dy)
        {
            Take(g);
            Ok(g.Fold(V(-1, 1), V(-1, -1), false, 0), "Fold");
            Ok(g.Fold(V(-1, 0), V(-0.6, -0.8 + dy), false, 0), "Fold");
            Ok(g.Fold(V(1, 0), V(0.6, -0.8 + dy), false, 0), "Fold");
            return Ok(g.Finish(), "Finish");
        }

        public static void Waste(OrigamiGame g)
        {
            Take(g);
            Ok(g.Discard(), "Discard");
        }

        /// <summary>Night = (Stand if seated) → Interact(bed) → Sleep(yes).</summary>
        public static CommandResult Night(OrigamiGame g)
        {
            if (g.Snapshot.Mode == WorkshopMode.Seated) Stand(g);
            Ok(g.Interact(InteractTargets.Bed), "bed");
            return Ok(g.Sleep(true), "Sleep(yes)");
        }
    }
}
