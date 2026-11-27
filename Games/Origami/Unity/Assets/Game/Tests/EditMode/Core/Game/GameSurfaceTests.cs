using NUnit.Framework;
using Origami.Core.Game;
using Origami.Tests.Folding;

namespace Origami.Tests.Game
{
    // C-150 / C-151: pins the public surface.
    public class GameSurfaceTests
    {
        [Test]
        public void Namespace_ContainsOnlyContractTypes()
        {
            SurfaceAssert.NamespaceHasOnly(typeof(OrigamiGame).Assembly, "Origami.Core.Game",
                "CommandResult", "DisplayedPiece", "GameSnapshot", "OrigamiGame", "RejectReason", "SaleLine", "SettlementResult",
                "SheetSnapshot", "WorkshopMode");
        }

        [Test]
        public void OrigamiGame_Surface()
        {
            var t = typeof(OrigamiGame);
            Assert.IsTrue(t.IsSealed && !t.IsAbstract);
            SurfaceAssert.Is(t,
                ".ctor()",
                "GameSnapshot Snapshot {get;}",
                "CommandResult NewRun()",
                "CommandResult Interact(String)",
                "CommandResult StandUp()",
                "CommandResult OpenBook()",
                "CommandResult CloseBook()",
                "CommandResult TurnPage(Int32)",
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
                "CommandResult SkipGuide()");
        }

        [Test]
        public void WorkshopMode_Values() => SurfaceAssert.EnumIs(typeof(WorkshopMode),
            "Walking=1", "Seated=2", "Bookshelf=3", "Counter=4", "SleepConfirm=5", "Ended=6");

        [Test]
        public void RejectReason_Values() => SurfaceAssert.EnumIs(typeof(RejectReason),
            "None=0", "RunEnded=1", "WrongMode=2", "UnknownTarget=3", "SheetOnTable=4", "NoSheetsLeft=5", "NotEnoughMoney=6",
            "NoSheet=7", "InvalidFold=8", "NothingToUndo=9", "NoFolds=10", "NoGuide=11", "UnknownModel=12", "AlreadyOwned=13");

        [Test]
        public void CommandResult_Surface()
        {
            var t = typeof(CommandResult);
            Assert.IsTrue(t.IsSealed);
            SurfaceAssert.Is(t,
                "Boolean Accepted {get;}", "RejectReason Reason {get;}", "FoldError FoldError {get;}", "FoldPlan Plan {get;}",
                "RecognitionResult Recognition {get;}", "StepScore GuideScore {get;}", "SettlementResult Settlement {get;}");
        }

        [Test]
        public void SaleLine_Surface()
        {
            var t = typeof(SaleLine);
            Assert.IsTrue(t.IsSealed);
            SurfaceAssert.Is(t, "String ModelId {get;}", "Grade Grade {get;}", "Boolean Popular {get;}", "Int32 Price {get;}");
        }

        [Test]
        public void SettlementResult_Surface()
        {
            var t = typeof(SettlementResult);
            Assert.IsTrue(t.IsSealed);
            SurfaceAssert.Is(t,
                "Int32 SettledDay {get;}", "IReadOnlyList`1 Lines {get;}", "Int32 Total {get;}", "Int32 MoneyAfter {get;}",
                "RunEndReason EndReason {get;}", "String NextPopularModelId {get;}");
        }

        [Test]
        public void DisplayedPiece_Surface()
        {
            var t = typeof(DisplayedPiece);
            Assert.IsTrue(t.IsSealed);
            SurfaceAssert.Is(t,
                "String ModelId {get;}", "Grade Grade {get;}", "Double Score {get;}", "Int32 Percent {get;}", "PaperState Shape {get;}");
        }

        [Test]
        public void SheetSnapshot_Surface()
        {
            var t = typeof(SheetSnapshot);
            Assert.IsTrue(t.IsSealed);
            SurfaceAssert.Is(t,
                "PaperState State {get;}", "Int32 FoldCount {get;}", "Int32 CreaseCount {get;}", "Int32 ActionCount {get;}",
                "Boolean CanUndo {get;}", "Boolean IsTutorial {get;}", "Boolean GuideVisible {get;}", "GuideStep Guide {get;}",
                "IReadOnlyList`1 GuideScores {get;}");
        }

        [Test]
        public void GameSnapshot_Surface()
        {
            var t = typeof(GameSnapshot);
            Assert.IsTrue(t.IsSealed);
            SurfaceAssert.Is(t,
                "Int32 Day {get;}", "Int32 Money {get;}", "Int32 SheetsLeft {get;}",
                "WorkshopMode Mode {get;}", "Boolean BookOpen {get;}", "Boolean BookVisible {get;}", "Int32 BookPage {get;}",
                "IReadOnlyList`1 OwnedModelIds {get;}", "Boolean FreeSheetAvailable {get;}", "Boolean TutorialFinished {get;}",
                "Boolean HasSat {get;}", "SheetSnapshot Sheet {get;}", "IReadOnlyList`1 Display {get;}",
                "CompletionCounts Completions {get;}", "Int32 FailureCount {get;}", "Int32 TotalSales {get;}",
                "Int32 TotalPaperCost {get;}", "Int32 TotalBookCost {get;}", "String PopularModelId {get;}",
                "RunEndReason EndReason {get;}", "String EndingId {get;}", "Boolean IsEnded {get;}");
        }

        [Test]
        public void ResultTypes_HaveNoPublicConstructors()
        {
            foreach (var t in new[] { typeof(CommandResult), typeof(SaleLine), typeof(SettlementResult), typeof(DisplayedPiece), typeof(SheetSnapshot), typeof(GameSnapshot) })
                Assert.AreEqual(0, t.GetConstructors().Length, t.Name);
        }
    }
}
