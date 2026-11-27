using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Origami.Core.Catalog;
using Origami.Core.Folding;

namespace Origami.Core.Tutorial
{
    /// <summary>The 6-step paper plane tutorial guide (C-140, D-115), generated once from the plane reference sequence.</summary>
    public static class PlaneGuide
    {
        public const int StepCount = 6;

        private static readonly Lazy<IReadOnlyList<GuideStep>> StepsLazy = new Lazy<IReadOnlyList<GuideStep>>(Build);

        /// <summary>Steps 1..6 (shared read-only list, same instances on every call).</summary>
        public static IReadOnlyList<GuideStep> Steps => StepsLazy.Value;

        private static IReadOnlyList<GuideStep> Build()
        {
            var plane = ModelCatalog.Find(ModelIds.Plane);
            if (plane == null || plane.ActionCount != StepCount)
                throw new InvalidOperationException("plane catalogue entry must have " + StepCount + " steps");
            var steps = new GuideStep[StepCount];
            for (int i = 0; i < StepCount; i++)
            {
                int n = i + 1;
                var s = plane.Steps[i];
                var before = ModelCatalog.StateBeforeStep(ModelIds.Plane, n);
                if (!LineOnPaper.TryGet(before, s.Grab, s.Drop, out var a, out var b))
                    throw new InvalidOperationException("plane step " + n + " has no line on the paper");
                steps[i] = new GuideStep(n, s.Kind, s.Grab, s.Drop, a, b, n == 1 || n == StepCount, plane.StepTexts[i]);
            }
            return new ReadOnlyCollection<GuideStep>(steps);
        }
    }
}
