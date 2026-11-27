using NUnit.Framework;
using Origami.Core.Tutorial;
using Origami.Tests.Folding;

namespace Origami.Tests.Tutorial
{
    // C-140: pins the public surface.
    public class TutorialSurfaceTests
    {
        [Test]
        public void Namespace_ContainsOnlyContractTypes()
        {
            SurfaceAssert.NamespaceHasOnly(typeof(PlaneGuide).Assembly, "Origami.Core.Tutorial",
                "GuideStep", "PlaneGuide", "StepScore", "StepScoring");
        }

        [Test]
        public void GuideStep_Surface()
        {
            var t = typeof(GuideStep);
            Assert.IsTrue(t.IsSealed && !t.IsAbstract);
            SurfaceAssert.Is(t,
                "Int32 Number {get;}", "FoldStepKind Kind {get;}", "Vec2 Grab {get;}", "Vec2 Drop {get;}",
                "Vec2 LineA {get;}", "Vec2 LineB {get;}", "Boolean EitherSide {get;}", "String Text {get;}");
        }

        [Test]
        public void PlaneGuide_Surface()
        {
            var t = typeof(PlaneGuide);
            Assert.IsTrue(t.IsSealed && t.IsAbstract, "static");
            SurfaceAssert.Is(t, "const Int32 StepCount", "static IReadOnlyList`1 Steps {get;}");
        }

        [Test]
        public void StepScore_Surface()
        {
            var t = typeof(StepScore);
            Assert.IsTrue(t.IsSealed && !t.IsAbstract);
            SurfaceAssert.Is(t,
                "Int32 Score {get;}", "Double CreaseOffset {get;}", "Double AngleErrorDeg {get;}",
                "Double GrabError {get;}", "Double DropError {get;}", "Boolean WrongSide {get;}", "Boolean KindMismatch {get;}");
        }

        [Test]
        public void StepScoring_Surface()
        {
            var t = typeof(StepScoring);
            Assert.IsTrue(t.IsSealed && t.IsAbstract, "static");
            SurfaceAssert.Is(t,
                "const Double OffsetTolerance", "const Double AngleTolerance",
                "static StepScore Evaluate(GuideStep,FoldStepKind,Vec2,Vec2)");
        }
    }
}
