using System;
using NUnit.Framework;
using Origami.Core.Recognition;
using Origami.Tests.Folding;

namespace Origami.Tests.Recognition
{
    // C-110: pins the public surface (INTERFACE_CONTRACTS — a change needs a contract change request).
    public class RecognitionSurfaceTests
    {
        [Test]
        public void Namespaces_ContainOnlyContractTypes()
        {
            var asm = typeof(Origami.Core.Folding.FoldPlanner).Assembly;
            SurfaceAssert.NamespaceHasOnly(asm, "Origami.Core.Recognition", "D4", "Grade", "Grading", "Outline", "RecognitionCatalog", "RecognitionResult", "Recognizer", "Similarity");
        }

        [Test]
        public void D4_Surface()
        {
            var t = typeof(D4);
            Assert.IsTrue(t.IsSealed && t.IsPublic);
            SurfaceAssert.Is(t,
                "const Int32 Count",
                "static Vec2 Apply(Int32,Vec2)");
        }

        [Test]
        public void Grade_Values() => SurfaceAssert.EnumIs(typeof(Grade), "None=0", "Star1=1", "Star2=2", "Star3=3");

        [Test]
        public void Grading_Surface()
        {
            var t = typeof(Grading);
            Assert.IsTrue(t.IsSealed && t.IsPublic);
            SurfaceAssert.Is(t,
                "const Int32 Grade2Min",
                "const Int32 Grade3Min",
                "const Int32 MatchMin",
                "static Grade FromPercent(Int32)",
                "static Grade FromScore(Double)",
                "static Int32 Percent(Double)");
        }

        [Test]
        public void Outline_Surface()
        {
            var t = typeof(Outline);
            Assert.IsTrue(t.IsSealed && t.IsPublic);
            SurfaceAssert.Is(t,
                "Double Area {get;}",
                "Vec2 Centroid {get;}",
                "const Double RowStep",
                "static Double IntersectionArea(Outline,Outline)",
                "static Outline FromPolygons(IEnumerable`1)",
                "static Outline FromState(PaperState)");
        }

        [Test]
        public void RecognitionCatalog_Surface()
        {
            var t = typeof(RecognitionCatalog);
            Assert.IsTrue(t.IsSealed && t.IsPublic);
            SurfaceAssert.Is(t,
                ".ctor(IEnumerable`1)",
                "Double Score(PaperState,String)",
                "IReadOnlyList`1 ModelIds {get;}");
        }

        [Test]
        public void RecognitionResult_Surface()
        {
            var t = typeof(RecognitionResult);
            Assert.IsTrue(t.IsSealed && t.IsPublic);
            SurfaceAssert.Is(t,
                "Boolean IsPiece {get;}",
                "Double BestScore {get;}",
                "Double Score {get;}",
                "Grade Grade {get;}",
                "Int32 Percent {get;}",
                "String BestModelId {get;}",
                "String ModelId {get;}");
        }

        [Test]
        public void Recognizer_Surface()
        {
            var t = typeof(Recognizer);
            Assert.IsTrue(t.IsSealed && t.IsPublic);
            SurfaceAssert.Is(t,
                "static RecognitionResult Recognize(PaperState,IEnumerable`1,RecognitionCatalog)");
        }

        [Test]
        public void Similarity_Surface()
        {
            var t = typeof(Similarity);
            Assert.IsTrue(t.IsSealed && t.IsPublic);
            SurfaceAssert.Is(t,
                "static Double Centered(PaperState,PaperState)");
        }

    }
}
