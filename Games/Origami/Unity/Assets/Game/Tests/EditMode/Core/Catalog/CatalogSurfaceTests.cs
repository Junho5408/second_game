using System;
using NUnit.Framework;
using Origami.Core.Catalog;
using Origami.Tests.Folding;

namespace Origami.Tests.Catalog
{
    // C-120: pins the public surface (INTERFACE_CONTRACTS — a change needs a contract change request).
    public class CatalogSurfaceTests
    {
        [Test]
        public void Namespaces_ContainOnlyContractTypes()
        {
            var asm = typeof(Origami.Core.Folding.FoldPlanner).Assembly;
            SurfaceAssert.NamespaceHasOnly(asm, "Origami.Core.Catalog", "BookLayout", "BookPageKind", "EndingCatalog", "EndingDefinition", "EndingIds", "InteractTargets", "ModelCatalog", "ModelDefinition", "ModelIds");
        }

        [Test]
        public void BookLayout_Surface()
        {
            var t = typeof(BookLayout);
            Assert.IsTrue(t.IsSealed && t.IsPublic);
            SurfaceAssert.Is(t,
                "const Int32 ContentsPage",
                "const Int32 SymbolsPage",
                "static BookPageKind KindOf(Int32)",
                "static Int32 ClampToSpread(Int32)",
                "static Int32 CoverPage(String)",
                "static Int32 PageCount {get;}",
                "static Int32 StepAt(Int32)",
                "static String ModelAt(Int32)");
        }

        [Test]
        public void BookPageKind_Values() => SurfaceAssert.EnumIs(typeof(BookPageKind), "Contents=0", "Symbols=1", "Cover=2", "Step=3", "Complete=4");

        [Test]
        public void EndingCatalog_Surface()
        {
            var t = typeof(EndingCatalog);
            Assert.IsTrue(t.IsSealed && t.IsPublic);
            SurfaceAssert.Is(t,
                "static EndingDefinition Find(String)",
                "static IReadOnlyList`1 Endings {get;}");
        }

        [Test]
        public void EndingDefinition_Surface()
        {
            var t = typeof(EndingDefinition);
            Assert.IsTrue(t.IsSealed && t.IsPublic);
            SurfaceAssert.Is(t,
                "Int32 Priority {get;}",
                "String DisplayName {get;}",
                "String Id {get;}",
                "String Text {get;}");
        }

        [Test]
        public void EndingIds_Surface()
        {
            var t = typeof(EndingIds);
            Assert.IsTrue(t.IsSealed && t.IsPublic);
            SurfaceAssert.Is(t,
                "const String EmptyHanded",
                "const String LocalWorkshop",
                "const String Master",
                "const String TulipGarden",
                "const String Tycoon");
        }

        [Test]
        public void InteractTargets_Surface()
        {
            var t = typeof(InteractTargets);
            Assert.IsTrue(t.IsSealed && t.IsPublic);
            SurfaceAssert.Is(t,
                "const String Bed",
                "const String Bookshelf",
                "const String Counter",
                "const String Table",
                "static Boolean IsValid(String)",
                "static IReadOnlyList`1 All {get;}");
        }

        [Test]
        public void ModelCatalog_Surface()
        {
            var t = typeof(ModelCatalog);
            Assert.IsTrue(t.IsSealed && t.IsPublic);
            SurfaceAssert.Is(t,
                "static IReadOnlyList`1 Models {get;}",
                "static IReadOnlyList`1 PopularByDay {get;}",
                "static Int32 IndexOf(String)",
                "static ModelDefinition Find(String)",
                "static PaperState ReferenceState(String)",
                "static PaperState StateBeforeStep(String,Int32)",
                "static RecognitionCatalog Recognition {get;}",
                "static String PopularModelId(Int32)");
        }

        [Test]
        public void ModelDefinition_Surface()
        {
            var t = typeof(ModelDefinition);
            Assert.IsTrue(t.IsSealed && t.IsPublic);
            SurfaceAssert.Is(t,
                "Boolean InitiallyOwned {get;}",
                "Double CoverRotationDeg {get;}",
                "IReadOnlyList`1 StepTexts {get;}",
                "IReadOnlyList`1 Steps {get;}",
                "Int32 ActionCount {get;}",
                "Int32 BasePrice {get;}",
                "Int32 Difficulty {get;}",
                "Int32 FoldCount {get;}",
                "Int32 Order {get;}",
                "Int32 PagePrice {get;}",
                "String DisplayName {get;}",
                "String Id {get;}",
                "String Techniques {get;}");
        }

        [Test]
        public void ModelIds_Surface()
        {
            var t = typeof(ModelIds);
            Assert.IsTrue(t.IsSealed && t.IsPublic);
            SurfaceAssert.Is(t,
                "const String Cicada",
                "const String Dog",
                "const String House",
                "const String Plane",
                "const String Tulip");
        }

    }
}
