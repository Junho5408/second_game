using System;
using NUnit.Framework;
using Origami.Core.Folding;
using Origami.Core.Geometry;

namespace Origami.Tests.Folding
{
    // C-101..C-108: pins the public surface (INTERFACE_CONTRACTS — a change needs a contract change request).
    public class FoldingSurfaceTests
    {
        [Test]
        public void Namespaces_ContainOnlyContractTypes()
        {
            var asm = typeof(Origami.Core.Folding.FoldPlanner).Assembly;
            SurfaceAssert.NamespaceHasOnly(asm, "Origami.Core.Geometry", "Vec2");
            SurfaceAssert.NamespaceHasOnly(asm, "Origami.Core.Folding", "CreaseSegment", "DropSnap", "Facet", "FoldError", "FoldGesture", "FoldLine", "FoldPlan", "FoldPlanner", "FoldSession", "FoldStep", "FoldStepKind", "FoldTexts", "LineOnPaper", "MarkPiece", "PaperState", "SnapKind", "SnapResult");
        }

        [Test]
        public void CreaseSegment_Surface()
        {
            var t = typeof(CreaseSegment);
            Assert.IsTrue(t.IsValueType && t.IsPublic);
            SurfaceAssert.Is(t,
                ".ctor(Vec2,Vec2)",
                "Boolean Equals(CreaseSegment)",
                "Boolean Equals(Object)",
                "Double Length {get;}",
                "Int32 GetHashCode()",
                "Vec2 A {get;}",
                "Vec2 B {get;}");
        }

        [Test]
        public void DropSnap_Surface()
        {
            var t = typeof(DropSnap);
            Assert.IsTrue(t.IsSealed && t.IsPublic);
            SurfaceAssert.Is(t,
                "const Double GuideSnap",
                "const Double LinePairMin",
                "const Double SnapLine",
                "const Double SnapMark",
                "const Double SnapPoint",
                "static Boolean IsVisible(PaperState,Vec2,Int32)",
                "static Double Key(Double)",
                "static IReadOnlyList`1 Landmarks(PaperState)",
                "static IReadOnlyList`1 MarkPieces(PaperState)",
                "static SnapResult Snap(PaperState,Vec2,Vec2)",
                "static SnapResult Snap(PaperState,Vec2,Vec2,Vec2)");
        }

        [Test]
        public void Facet_Surface()
        {
            var t = typeof(Facet);
            Assert.IsTrue(t.IsSealed && t.IsPublic);
            SurfaceAssert.Is(t,
                ".ctor(IEnumerable`1,IEnumerable`1,Int32,Boolean)",
                "Boolean Contains(Vec2,Double)",
                "Boolean FrontUp {get;}",
                "Boolean StrictlyContains(Vec2,Double)",
                "Double Area {get;}",
                "Double SignedArea()",
                "IReadOnlyList`1 Paper {get;}",
                "IReadOnlyList`1 World {get;}",
                "Int32 Layer {get;}",
                "Vec2 PaperToWorld(Vec2)");
        }

        [Test]
        public void FoldError_Values() => SurfaceAssert.EnumIs(typeof(FoldError), "None=0", "DragTooShort=1", "NothingGrabbed=2", "NothingToFold=3", "LineOutsidePaper=4", "InvalidLayers=5");

        [Test]
        public void FoldGesture_Surface()
        {
            var t = typeof(FoldGesture);
            Assert.IsTrue(t.IsSealed && t.IsPublic);
            SurfaceAssert.Is(t,
                "Boolean GrabbedCorner {get;}",
                "FoldPlan Preview(PaperState,Boolean,Boolean)",
                "Int32 Layers {get;}",
                "Int32 StackCount {get;}",
                "SnapResult Drop {get;}",
                "SnapResult MoveTo(PaperState,Vec2)",
                "SnapResult MoveTo(PaperState,Vec2,Vec2)",
                "Vec2 Grab {get;}",
                "Void CycleLayers(Int32)",
                "const Double GrabCornerRadius",
                "const Double GrabEdgeRadius",
                "static FoldGesture TryBegin(PaperState,Vec2)");
        }

        [Test]
        public void FoldLine_Surface()
        {
            var t = typeof(FoldLine);
            Assert.IsTrue(t.IsValueType && t.IsPublic);
            SurfaceAssert.Is(t,
                ".ctor(Vec2,Vec2)",
                "Double Distance(Vec2)",
                "Double RawSide(Vec2)",
                "Double Side(Vec2)",
                "Vec2 Direction {get;}",
                "Vec2 Normal {get;}",
                "Vec2 Point {get;}",
                "Vec2 Reflect(Vec2)",
                "static FoldLine FromGrabDrop(Vec2,Vec2)");
        }

        [Test]
        public void FoldPlan_Surface()
        {
            var t = typeof(FoldPlan);
            Assert.IsTrue(t.IsSealed && t.IsPublic);
            SurfaceAssert.Is(t,
                "Boolean IsCrease {get;}",
                "Boolean Mountain {get;}",
                "Boolean Partial {get;}",
                "Boolean Valid {get;}",
                "FoldError Error {get;}",
                "FoldLine Line {get;}",
                "IReadOnlyList`1 Moved {get;}",
                "IReadOnlyList`1 Moving {get;}",
                "IReadOnlyList`1 NewCreases {get;}",
                "IReadOnlyList`1 Static {get;}",
                "Int32 Layers {get;}",
                "PaperState Result {get;}",
                "Vec2 Drop {get;}",
                "Vec2 Grab {get;}");
        }

        [Test]
        public void FoldPlanner_Surface()
        {
            var t = typeof(FoldPlanner);
            Assert.IsTrue(t.IsSealed && t.IsPublic);
            SurfaceAssert.Is(t,
                "const Double ContainsEps",
                "const Double HingeMin",
                "const Double MinArea",
                "const Double MinDrag",
                "const Double SideEps",
                "static FoldPlan PlanCrease(PaperState,Vec2,Vec2,Int32)",
                "static FoldPlan PlanFold(PaperState,Vec2,Vec2,Boolean,Int32)",
                "static PaperState Apply(PaperState,FoldStep)",
                "static PaperState ApplyAll(IEnumerable`1,PaperState)",
                "static PaperState Flip(PaperState)");
        }

        [Test]
        public void FoldSession_Surface()
        {
            var t = typeof(FoldSession);
            Assert.IsTrue(t.IsSealed && t.IsPublic);
            SurfaceAssert.Is(t,
                ".ctor()",
                "Boolean CanUndo {get;}",
                "Boolean Undo()",
                "Boolean Unfold()",
                "FoldPlan Crease(Vec2,Vec2,Int32)",
                "FoldPlan Fold(Vec2,Vec2,Boolean,Int32)",
                "IReadOnlyList`1 AppliedSteps {get;}",
                "Int32 ActionCount {get;}",
                "Int32 CreaseCount {get;}",
                "Int32 FoldCount {get;}",
                "PaperState State {get;}",
                "Void Flip()");
        }

        [Test]
        public void FoldStep_Surface()
        {
            var t = typeof(FoldStep);
            Assert.IsTrue(t.IsSealed && t.IsPublic);
            SurfaceAssert.Is(t,
                "Boolean Mountain {get;}",
                "FoldStepKind Kind {get;}",
                "Int32 Layers {get;}",
                "Vec2 Drop {get;}",
                "Vec2 Grab {get;}",
                "static FoldStep Crease(Vec2,Vec2,Int32)",
                "static FoldStep Flip()",
                "static FoldStep Fold(Vec2,Vec2,Boolean,Int32)");
        }

        [Test]
        public void FoldStepKind_Values() => SurfaceAssert.EnumIs(typeof(FoldStepKind), "Fold=1", "Crease=2", "Flip=3");

        [Test]
        public void FoldTexts_Surface()
        {
            var t = typeof(FoldTexts);
            Assert.IsTrue(t.IsSealed && t.IsPublic);
            SurfaceAssert.Is(t,
                "const String CreaseLabel",
                "static String ErrorMessage(FoldError)",
                "static String LayerLabel(Int32,Int32)");
        }

        [Test]
        public void LineOnPaper_Surface()
        {
            var t = typeof(LineOnPaper);
            Assert.IsTrue(t.IsSealed && t.IsPublic);
            SurfaceAssert.Is(t,
                "static Boolean TryGet(PaperState,Vec2,Vec2,Vec2&,Vec2&)");
        }

        [Test]
        public void MarkPiece_Surface()
        {
            var t = typeof(MarkPiece);
            Assert.IsTrue(t.IsValueType && t.IsPublic);
            SurfaceAssert.Is(t,
                "Int32 FacetIndex {get;}",
                "Vec2 A {get;}",
                "Vec2 B {get;}");
        }

        [Test]
        public void PaperState_Surface()
        {
            var t = typeof(PaperState);
            Assert.IsTrue(t.IsSealed && t.IsPublic);
            SurfaceAssert.Is(t,
                ".ctor(IEnumerable`1,IEnumerable`1)",
                "Boolean TrySnapGrab(Vec2,Double,Double,Vec2&,Boolean&)",
                "Double FlipAxisX()",
                "Double TotalArea()",
                "IReadOnlyList`1 Creases {get;}",
                "IReadOnlyList`1 Facets {get;}",
                "Int32 MaxLayer()",
                "Int32 StackCountAt(Vec2)",
                "Int32 TopFacetAt(Vec2)",
                "static PaperState Initial {get;}");
        }

        [Test]
        public void SnapKind_Values() => SurfaceAssert.EnumIs(typeof(SnapKind), "None=0", "Guide=1", "Point=2", "Line=3", "Mark=4");

        [Test]
        public void SnapResult_Surface()
        {
            var t = typeof(SnapResult);
            Assert.IsTrue(t.IsValueType && t.IsPublic);
            SurfaceAssert.Is(t,
                "Int32 AnchorCount {get;}",
                "SnapKind Kind {get;}",
                "Vec2 AnchorA {get;}",
                "Vec2 AnchorB {get;}",
                "Vec2 Point {get;}");
        }

        [Test]
        public void Vec2_Surface()
        {
            var t = typeof(Vec2);
            Assert.IsTrue(t.IsValueType && t.IsPublic);
            SurfaceAssert.Is(t,
                ".ctor(Double,Double)",
                "Boolean Equals(Object)",
                "Boolean Equals(Vec2)",
                "Double Length {get;}",
                "Double LengthSq {get;}",
                "Int32 GetHashCode()",
                "String ToString()",
                "Vec2 Normalized {get;}",
                "Vec2 Perp {get;}",
                "readonly Double X",
                "readonly Double Y",
                "static Double Cross(Vec2,Vec2)",
                "static Double Distance(Vec2,Vec2)",
                "static Double Dot(Vec2,Vec2)",
                "static Vec2 ClosestOnSegment(Vec2,Vec2,Vec2)",
                "static Vec2 Lerp(Vec2,Vec2,Double)",
                "static Vec2 op_Addition(Vec2,Vec2)",
                "static Vec2 op_Multiply(Double,Vec2)",
                "static Vec2 op_Multiply(Vec2,Double)",
                "static Vec2 op_Subtraction(Vec2,Vec2)",
                "static Vec2 op_UnaryNegation(Vec2)");
        }

    }
}
