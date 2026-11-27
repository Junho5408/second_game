using System;
using NUnit.Framework;
using Origami.Core.Screens;
using Origami.Tests.Folding;

namespace Origami.Tests.Screens
{
    // C-171: pins the public surface of Origami.Core.Screens.
    public class ScreensSurfaceTests
    {
        [Test]
        public void Namespace_ContainsOnlyContractTypes()
        {
            SurfaceAssert.NamespaceHasOnly(typeof(ScreenFlow).Assembly, "Origami.Core.Screens",
                "FlowMessage", "GameKeys", "MessageKind", "Overlay", "ScreenFlow", "SheetChange", "SheetChangeKind");
        }

        [Test]
        public void GameKeys_FlagValues()
        {
            Assert.IsTrue(typeof(GameKeys).IsDefined(typeof(FlagsAttribute), false));
            SurfaceAssert.EnumIs(typeof(GameKeys),
                "None=0", "Escape=1", "Enter=2", "Interact=4", "TakeSheet=8", "Discard=16", "Undo=32", "Unfold=64", "Flip=128",
                "Book=256", "SkipGuide=512", "StandUp=1024", "PageLeft=2048", "PageRight=4096");
        }

        [Test]
        public void Overlay_Values() => SurfaceAssert.EnumIs(typeof(Overlay),
            "None=0", "PauseMenu=1", "ConfirmNewRun=2", "Settlement=3", "Result=4");

        [Test]
        public void MessageKind_Values() => SurfaceAssert.EnumIs(typeof(MessageKind), "None=0", "Reject=1", "Recognition=2", "Info=3");

        [Test]
        public void SheetChangeKind_Values() => SurfaceAssert.EnumIs(typeof(SheetChangeKind),
            "None=0", "Taken=1", "Folded=2", "Creased=3", "Flipped=4", "Undone=5", "Unfolded=6", "Removed=7", "Reset=8");

        [Test]
        public void FlowMessage_Surface()
        {
            Assert.IsTrue(typeof(FlowMessage).IsValueType);
            SurfaceAssert.Is(typeof(FlowMessage), "Int32 Serial {get;}", "MessageKind Kind {get;}", "String Text {get;}");
        }

        [Test]
        public void SheetChange_Surface()
        {
            Assert.IsTrue(typeof(SheetChange).IsValueType);
            SurfaceAssert.Is(typeof(SheetChange),
                "Int32 Serial {get;}", "SheetChangeKind Kind {get;}", "FoldPlan Plan {get;}", "PaperState Previous {get;}");
        }

        [Test]
        public void ScreenFlow_Surface()
        {
            var t = typeof(ScreenFlow);
            Assert.IsTrue(t.IsSealed && !t.IsAbstract);
            SurfaceAssert.Is(t,
                ".ctor()",
                "GameSnapshot Snapshot {get;}",
                "Overlay Overlay {get;}",
                "SettlementResult PendingSettlement {get;}",
                "FlowMessage Message {get;}",
                "SheetChange LastSheetChange {get;}",
                "Boolean WalkHintVisible {get;}",
                "Boolean TakeSheetHintVisible {get;}",
                "Boolean PostTutorialHintVisible {get;}",
                "Boolean ProcessKeys(GameKeys,String)",
                "CommandResult Interact(String)",
                "CommandResult StandUp()",
                "CommandResult ToggleBook()",
                "CommandResult OpenBook()",
                "CommandResult CloseBook()",
                "CommandResult TurnPage(Int32)",
                "CommandResult TurnPageBy(Int32)",
                "CommandResult BuyPage(String)",
                "CommandResult CloseCounter()",
                "CommandResult Sleep(Boolean)",
                "CommandResult TakeSheet()",
                "CommandResult Fold(Vec2,Vec2,Boolean,Int32)",
                "CommandResult Crease(Vec2,Vec2,Int32)",
                "CommandResult Flip()",
                "CommandResult Undo()",
                "CommandResult Unfold()",
                "CommandResult Finish()",
                "CommandResult Discard()",
                "CommandResult SkipGuide()",
                "Void OpenPause()",
                "Void ClosePause()",
                "Void RequestNewRun()",
                "Void ConfirmNewRun()",
                "Void AcknowledgeSettlement()");
        }
    }
}
