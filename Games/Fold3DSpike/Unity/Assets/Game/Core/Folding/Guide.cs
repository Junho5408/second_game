using System;

namespace Fold3DSpike.Core.Folding
{
    /// <summary>One guided step: grab <see cref="Grab"/>, drop it on <see cref="Drop"/>; ideal crease A-B.</summary>
    public sealed class StepTarget
    {
        public string Title;
        public string Hint;
        public Vec2 Grab;
        public Vec2 Drop;
        public Vec2 CreaseA;
        public Vec2 CreaseB;
        /// <summary>Symmetric step: folding the other half onto this one is equally correct.</summary>
        public bool EitherSide;
    }

    /// <summary>Accuracy of one guided fold.</summary>
    public sealed class StepScore
    {
        /// <summary>0..100.</summary>
        public int Score;
        /// <summary>Mean distance of the ideal crease end points from the player's crease (paper units).</summary>
        public double CreaseOffset;
        /// <summary>Angle between player's and ideal crease, 0..90 degrees.</summary>
        public double AngleErrorDeg;
        /// <summary>Distance grabbed point to target grab point (paper units).</summary>
        public double GrabError;
        /// <summary>Distance drop point to target drop point (paper units).</summary>
        public double DropError;
        /// <summary>The wrong part of the paper was folded over (score halved).</summary>
        public bool WrongSide;
    }

    /// <summary>Paper plane on a 2x2 square (half = 1, centred on the origin, +Y = far edge).</summary>
    public static class PaperPlaneGuide
    {
        public const double Half = 1.0;

        public static readonly StepTarget[] Steps = Build();

        private static StepTarget[] Build()
        {
            double k = 1.0 - Math.Sqrt(2.0);            // centre-line point the slanted edge's end lands on
            double x = 2.0 * Math.Tan(Math.PI / 8.0);    // where the 22.5° crease meets the bottom edge
            return new[]
            {
                new StepTarget
                {
                    Title = "왼쪽 위 모서리를 가운데로",
                    Hint = "왼쪽 위 모서리를 잡아 종이 한가운데 점까지 끌어 놓으세요",
                    Grab = new Vec2(-1, 1), Drop = new Vec2(0, 0),
                    CreaseA = new Vec2(-1, 0), CreaseB = new Vec2(0, 1),
                },
                new StepTarget
                {
                    Title = "오른쪽 위 모서리를 가운데로",
                    Hint = "오른쪽 위 모서리도 한가운데 점으로 접으세요",
                    Grab = new Vec2(1, 1), Drop = new Vec2(0, 0),
                    CreaseA = new Vec2(1, 0), CreaseB = new Vec2(0, 1),
                },
                new StepTarget
                {
                    Title = "왼쪽 비스듬한 변을 가운데 선으로",
                    Hint = "왼쪽 뾰족한 끝(왼쪽 가장자리 가운데)을 잡아 가운데 선 위 표시까지",
                    Grab = new Vec2(-1, 0), Drop = new Vec2(0, k),
                    CreaseA = new Vec2(0, 1), CreaseB = new Vec2(-x, -1),
                },
                new StepTarget
                {
                    Title = "오른쪽 비스듬한 변을 가운데 선으로",
                    Hint = "오른쪽도 똑같이 가운데 선으로 접으세요",
                    Grab = new Vec2(1, 0), Drop = new Vec2(0, k),
                    CreaseA = new Vec2(0, 1), CreaseB = new Vec2(x, -1),
                },
                new StepTarget
                {
                    Title = "반으로 접기",
                    Hint = "왼쪽 아래 모서리를 오른쪽 아래 모서리에 맞춰 반으로 접으세요 (Shift = 산접기)",
                    Grab = new Vec2(-x, -1), Drop = new Vec2(x, -1),
                    CreaseA = new Vec2(0, 1), CreaseB = new Vec2(0, -1),
                    EitherSide = true,
                },
            };
        }
    }

    public static class FoldScoring
    {
        /// <summary>Crease offset (paper units) at which the position half of the score reaches 0.</summary>
        public const double OffsetTolerance = 0.25;
        /// <summary>Angle error (degrees) at which the angle half of the score reaches 0.</summary>
        public const double AngleTolerance = 20.0;

        public static StepScore Evaluate(StepTarget target, FoldLine player, Vec2 grab, Vec2 drop)
        {
            var ideal = FoldLine.FromGrabDrop(target.Grab, target.Drop);
            double cos = Math.Abs(Vec2.Dot(ideal.Direction, player.Direction));
            if (cos > 1) cos = 1;
            double angle = Math.Acos(cos) * 180.0 / Math.PI;
            double offset = 0.5 * (player.Distance(target.CreaseA) + player.Distance(target.CreaseB));

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
            return new StepScore
            {
                Score = s, CreaseOffset = offset, AngleErrorDeg = angle,
                GrabError = grabErr, DropError = dropErr, WrongSide = wrong,
            };
        }

        private static double Clamp01(double v) => v < 0 ? 0 : (v > 1 ? 1 : v);
    }
}
