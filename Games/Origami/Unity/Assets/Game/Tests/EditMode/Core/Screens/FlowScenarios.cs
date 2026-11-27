using NUnit.Framework;
using Origami.Core.Catalog;
using Origami.Core.Folding;
using Origami.Core.Game;
using Origami.Core.Geometry;
using Origami.Core.Screens;

namespace Origami.Tests.Screens
{
    /// <summary>FEATURE_SPECS "공통 표기" shorthands over <see cref="ScreenFlow"/> (each asserts acceptance).</summary>
    internal static class FlowScenarios
    {
        public static Vec2 V(double x, double y) => new Vec2(x, y);

        public static CommandResult Ok(CommandResult r, string what = "")
        {
            Assert.IsTrue(r.Accepted, what + " rejected: " + r.Reason + "/" + r.FoldError);
            return r;
        }

        public static void Sit(ScreenFlow f) => Ok(f.Interact(InteractTargets.Table), "Sit");
        public static void Stand(ScreenFlow f) => Ok(f.StandUp(), "Stand");
        public static void Take(ScreenFlow f) => Ok(f.TakeSheet(), "TakeSheet");

        public static CommandResult Apply(ScreenFlow f, FoldStep s)
        {
            if (s.Kind == FoldStepKind.Fold) return Ok(f.Fold(s.Grab, s.Drop, s.Mountain, s.Layers), "Fold");
            if (s.Kind == FoldStepKind.Crease) return Ok(f.Crease(s.Grab, s.Drop, s.Layers), "Crease");
            return Ok(f.Flip(), "Flip");
        }

        /// <summary>Ref(m) = TakeSheet → CONTENT §2 sequence → Finish.</summary>
        public static CommandResult Ref(ScreenFlow f, string modelId)
        {
            Take(f);
            foreach (var s in ModelCatalog.Find(modelId).Steps) Apply(f, s);
            return Ok(f.Finish(), "Finish");
        }

        /// <summary>Night = (Stand if seated) → Interact(bed) → Sleep(yes) → AcknowledgeSettlement.</summary>
        public static CommandResult Night(ScreenFlow f)
        {
            if (f.Snapshot.Mode == WorkshopMode.Seated) Stand(f);
            Ok(f.Interact(InteractTargets.Bed), "bed");
            var r = Ok(f.Sleep(true), "Sleep(yes)");
            f.AcknowledgeSettlement();
            return r;
        }
    }
}
