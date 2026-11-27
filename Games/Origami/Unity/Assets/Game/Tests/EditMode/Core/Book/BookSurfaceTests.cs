using NUnit.Framework;
using Origami.Core.Book;
using Origami.Tests.Folding;

namespace Origami.Tests.Book
{
    // C-160: pins the public surface of Origami.Core.Book.
    public class BookSurfaceTests
    {
        [Test]
        public void Namespace_ContainsOnlyContractTypes()
        {
            SurfaceAssert.NamespaceHasOnly(typeof(BookPages).Assembly, "Origami.Core.Book",
                "BookPage", "BookPages", "ContentsEntry", "CoverInfo", "StepDiagram");
        }

        [Test]
        public void BookPages_Surface()
        {
            var t = typeof(BookPages);
            Assert.IsTrue(t.IsAbstract && t.IsSealed, "static class");
            SurfaceAssert.Is(t, "static BookPage Get(Int32,IEnumerable`1)");
        }

        [Test]
        public void BookPage_Surface()
        {
            var t = typeof(BookPage);
            Assert.IsTrue(t.IsSealed);
            SurfaceAssert.Is(t,
                "Int32 Index {get;}", "BookPageKind Kind {get;}", "String ModelId {get;}", "Boolean Sealed {get;}",
                "Int32 StepNumber {get;}", "Int32 StepTotal {get;}", "String StepLabel {get;}", "StepDiagram Diagram {get;}",
                "CoverInfo Cover {get;}", "PaperState CompleteShape {get;}", "IReadOnlyList`1 Contents {get;}");
        }

        [Test]
        public void StepDiagram_Surface()
        {
            var t = typeof(StepDiagram);
            Assert.IsTrue(t.IsSealed);
            SurfaceAssert.Is(t,
                "FoldStep Step {get;}", "PaperState Before {get;}", "PaperState After {get;}", "Int32 BeforeFolds {get;}",
                "Int32 BeforeCreases {get;}", "Vec2 LineA {get;}", "Vec2 LineB {get;}", "SnapKind Magnet {get;}",
                "IReadOnlyList`1 Anchors {get;}", "String Text {get;}");
        }

        [Test]
        public void CoverInfo_Surface()
        {
            var t = typeof(CoverInfo);
            Assert.IsTrue(t.IsSealed);
            SurfaceAssert.Is(t,
                "String ModelId {get;}", "String Name {get;}", "PaperState Silhouette {get;}", "Double RotationDeg {get;}",
                "Int32 Difficulty {get;}", "Int32 ActionCount {get;}", "String Techniques {get;}", "Int32 BasePrice {get;}",
                "Int32 PagePrice {get;}", "Boolean Owned {get;}", "Boolean Buyable {get;}");
        }

        [Test]
        public void ContentsEntry_Surface()
        {
            var t = typeof(ContentsEntry);
            Assert.IsTrue(t.IsSealed);
            SurfaceAssert.Is(t,
                "String ModelId {get;}", "String Name {get;}", "Boolean Owned {get;}", "Int32 PagePrice {get;}", "Int32 CoverPage {get;}");
        }
    }
}
