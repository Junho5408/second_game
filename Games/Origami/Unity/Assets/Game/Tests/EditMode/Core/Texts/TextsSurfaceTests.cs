using NUnit.Framework;
using Origami.Core.Texts;
using Origami.Tests.Folding;

namespace Origami.Tests.Texts
{
    // C-170: pins the public surface of Origami.Core.Texts.
    public class TextsSurfaceTests
    {
        [Test]
        public void Namespace_ContainsOnlyContractTypes()
        {
            SurfaceAssert.NamespaceHasOnly(typeof(GameTexts).Assembly, "Origami.Core.Texts", "GameTexts", "HudData");
        }

        [Test]
        public void GameTexts_Surface()
        {
            var t = typeof(GameTexts);
            Assert.IsTrue(t.IsAbstract && t.IsSealed, "static class");
            SurfaceAssert.Is(t,
                "static String ModelName(String)",
                "static String RejectMessage(RejectReason,FoldError)",
                "static HudData Hud(GameSnapshot)",
                "static String InteractPrompt(String)",
                "const String WalkHint",
                "const String TakeSheetHint",
                "const String PostTutorialHint",
                "static String RecognitionLine(RecognitionResult)",
                "static String GuideTitle(GuideStep)",
                "static IReadOnlyList`1 SettlementLines(SettlementResult)",
                "static IReadOnlyList`1 CounterLines(GameSnapshot)",
                "static IReadOnlyList`1 ResultLines(GameSnapshot)");
        }

        [Test]
        public void HudData_Surface()
        {
            var t = typeof(HudData);
            Assert.IsTrue(t.IsSealed);
            SurfaceAssert.Is(t,
                "Int32 Day {get;}", "Int32 Money {get;}", "Int32 SheetsLeft {get;}", "String PopularName {get;}", "IReadOnlyList`1 Lines {get;}");
        }
    }
}
