using System;
using Origami.Core.Folding;
using Origami.Core.Geometry;

namespace Origami.Core.Tutorial
{
    /// <summary>
    /// Tutorial step accuracy (C-140, R-008, F-006 AC-08): the Fold3DSpike <c>FoldScoring.Evaluate</c> formula.
    /// Player line = FoldLine.FromGrabDrop(grab, drop); ideal line = FromGrabDrop(target.Grab, target.Drop).
    /// </summary>
    public static class StepScoring
    {
        /// <summary>Crease offset (paper units) at which the position half reaches 0.</summary>
        public const double OffsetTolerance = 0.25;

        /// <summary>Angle error (degrees) at which the angle half reaches 0.</summary>
        public const double AngleTolerance = 20.0;

        /// <summary>
        /// angle = acos(min(1, |ideal.Direction · player.Direction|)) in degrees; offset = mean player-line distance of
        /// target.LineA / LineB; score = round(100 × (0.5·clamp01(1 − offset/0.25) + 0.5·clamp01(1 − angle/20))), halved when
        /// the target grab lies on the staying side (Side ≥ 0, not for EitherSide steps), clamped to 0..100.
        /// EitherSide steps take the mirrored grab/drop errors when their sum is smaller.
        /// <paramref name="kind"/> ≠ target.Kind → Score 0 and KindMismatch (the geometric fields are still filled).
        /// A zero-length drag (grab == drop) has no direction; the result is defined but meaningless — the game only scores accepted actions.
        /// </summary>
        /// <exception cref="ArgumentNullException">target is null.</exception>
        public static StepScore Evaluate(GuideStep target, FoldStepKind kind, Vec2 grab, Vec2 drop)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            var player = FoldLine.FromGrabDrop(grab, drop);
            var ideal = FoldLine.FromGrabDrop(target.Grab, target.Drop);
            double cos = Math.Abs(Vec2.Dot(ideal.Direction, player.Direction));
            if (cos > 1) cos = 1;
            double angle = Math.Acos(cos) * 180.0 / Math.PI;
            double offset = 0.5 * (player.Distance(target.LineA) + player.Distance(target.LineB));

            double grabErr = Vec2.Distance(grab, target.Grab);
            double dropErr = Vec2.Distance(drop, target.Drop);
            bool wrong = false;
            if (target.EitherSide)
            {
                // Mirrored (other half folded over): grab ~ target drop and drop ~ target grab.
                double g2 = Vec2.Distance(grab, target.Drop), d2 = Vec2.Distance(drop, target.Grab);
                if (g2 + d2 < grabErr + dropErr)
                {
                    grabErr = g2;
                    dropErr = d2;
                }
            }
            else
            {
                wrong = player.Side(target.Grab) >= 0;
            }

            double pos = Clamp01(1 - offset / OffsetTolerance);
            double ang = Clamp01(1 - angle / AngleTolerance);
            double score = 100.0 * (0.5 * pos + 0.5 * ang);
            if (wrong) score *= 0.5;
            int s = (int)Math.Round(score);
            if (s < 0) s = 0;
            if (s > 100) s = 100;

            bool mismatch = kind != target.Kind;
            if (mismatch) s = 0;
            return new StepScore(s, offset, angle, grabErr, dropErr, wrong, mismatch);
        }

        private static double Clamp01(double v) => v < 0 ? 0 : (v > 1 ? 1 : v);
    }
}
