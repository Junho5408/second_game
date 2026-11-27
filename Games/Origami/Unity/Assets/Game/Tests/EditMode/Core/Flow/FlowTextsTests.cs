using System;
using NUnit.Framework;
using Origami.Core.Content;
using Origami.Core.Flow;
using Origami.Core.Rules;
using Origami.Core.Run;

namespace Origami.Tests.Flow
{
    // ORIGAMI-004: C-021 fixed string formats.
    public class FlowTextsTests
    {
        [Test]
        public void Numbers_Formats()
        {
            Assert.AreEqual("1/7", FlowTexts.DayText(1));
            Assert.AreEqual("7/7", FlowTexts.DayText(7));
            Assert.AreEqual("5/5", FlowTexts.AttemptsText(5));
            Assert.AreEqual("0/5", FlowTexts.AttemptsText(0));
            Assert.AreEqual("20원", FlowTexts.MoneyText(20));
            Assert.AreEqual("0원", FlowTexts.MoneyText(0));
            Assert.AreEqual("12345원", FlowTexts.MoneyText(12345), "no thousands separator");
        }

        [Test]
        public void Stars_AllGrades_InvalidEmpty()
        {
            Assert.AreEqual("★★★", FlowTexts.Stars(Grade.Star3));
            Assert.AreEqual("★★☆", FlowTexts.Stars(Grade.Star2));
            Assert.AreEqual("★☆☆", FlowTexts.Stars(Grade.Star1));
            Assert.AreEqual("", FlowTexts.Stars((Grade)0));
            Assert.AreEqual("", FlowTexts.Stars((Grade)4));
        }

        [Test]
        public void ActionGlyph_AndName()
        {
            Assert.AreEqual("↑", FlowTexts.ActionGlyph(FoldAction.U));
            Assert.AreEqual("↓", FlowTexts.ActionGlyph(FoldAction.D));
            Assert.AreEqual("←", FlowTexts.ActionGlyph(FoldAction.L));
            Assert.AreEqual("→", FlowTexts.ActionGlyph(FoldAction.R));
            Assert.AreEqual("F", FlowTexts.ActionGlyph(FoldAction.F));
            Assert.AreEqual("?", FlowTexts.ActionGlyph((FoldAction)0));
            Assert.AreEqual("?", FlowTexts.ActionGlyph((FoldAction)99));
            Assert.AreEqual("위로 접기", FlowTexts.ActionName(FoldAction.U));
            Assert.AreEqual("아래로 접기", FlowTexts.ActionName(FoldAction.D));
            Assert.AreEqual("왼쪽으로 접기", FlowTexts.ActionName(FoldAction.L));
            Assert.AreEqual("오른쪽으로 접기", FlowTexts.ActionName(FoldAction.R));
            Assert.AreEqual("뒤집기", FlowTexts.ActionName(FoldAction.F));
            Assert.AreEqual("", FlowTexts.ActionName((FoldAction)0));
            Assert.AreEqual("", FlowTexts.ActionName((FoldAction)(-1)));
        }

        [Test]
        public void UnlockCondition_Formats()
        {
            var frog = OrigamiCatalog.FindModel(ModelIds.Frog);
            var crane = OrigamiCatalog.FindModel(ModelIds.Crane);
            var dragon = OrigamiCatalog.FindModel(ModelIds.Dragon);
            var empty = CompletionCounts.Empty;
            Assert.AreEqual("종이배 3개 완성 시 (0/3)", FlowTexts.UnlockCondition(frog, empty));
            Assert.AreEqual("개구리 3개 완성 시 (0/3)", FlowTexts.UnlockCondition(crane, empty));
            Assert.AreEqual("학 3개 완성 시 (0/3)", FlowTexts.UnlockCondition(dragon, empty));
            var one = empty.With(ModelIds.Boat, Grade.Star1, 1);
            Assert.AreEqual("종이배 3개 완성 시 (1/3)", FlowTexts.UnlockCondition(frog, one));
            var mixed = empty.With(ModelIds.Boat, Grade.Star3, 1).With(ModelIds.Boat, Grade.Star2, 1);
            Assert.AreEqual("종이배 3개 완성 시 (2/3)", FlowTexts.UnlockCondition(frog, mixed), "grade-independent");
            var many = empty.With(ModelIds.Boat, Grade.Star3, 7);
            Assert.AreEqual("종이배 3개 완성 시 (3/3)", FlowTexts.UnlockCondition(frog, many), "progress capped at 3");
        }

        [Test]
        public void UnlockCondition_InitiallyUnlockedOrNullModel_Empty()
        {
            Assert.AreEqual("", FlowTexts.UnlockCondition(OrigamiCatalog.FindModel(ModelIds.Plane), CompletionCounts.Empty));
            Assert.AreEqual("", FlowTexts.UnlockCondition(OrigamiCatalog.FindModel(ModelIds.Boat), CompletionCounts.Empty));
            Assert.AreEqual("", FlowTexts.UnlockCondition(null, CompletionCounts.Empty));
            Assert.AreEqual("", FlowTexts.UnlockCondition(null, null));
            Assert.AreEqual("", FlowTexts.UnlockCondition(OrigamiCatalog.FindModel(ModelIds.Plane), null),
                "no counts needed for initially unlocked models");
        }

        [Test]
        public void UnlockCondition_LockedModelWithNullCounts_Throws()
        {
            // Decision (ORIGAMI-004): same programmer-error policy as Unlocking.Progress.
            Assert.Throws<ArgumentNullException>(() =>
                FlowTexts.UnlockCondition(OrigamiCatalog.FindModel(ModelIds.Frog), null));
        }

        [Test]
        public void Rejection_AllReasons()
        {
            var frog = OrigamiCatalog.FindModel(ModelIds.Frog);
            var c = CompletionCounts.Empty.With(ModelIds.Boat, Grade.Star2, 2);
            Assert.AreEqual("", FlowTexts.Rejection(RejectReason.None, null, c));
            Assert.AreEqual("런이 끝났습니다", FlowTexts.Rejection(RejectReason.RunEnded, null, c));
            Assert.AreEqual("접는 중에는 할 수 없습니다", FlowTexts.Rejection(RejectReason.FoldInProgress, null, c));
            Assert.AreEqual("없는 작품입니다", FlowTexts.Rejection(RejectReason.UnknownModel, null, c));
            Assert.AreEqual("종이배 3개 완성 시 (2/3)", FlowTexts.Rejection(RejectReason.Locked, frog, c));
            Assert.AreEqual("", FlowTexts.Rejection(RejectReason.Locked, null, c));
            Assert.AreEqual("오늘은 더 접을 수 없습니다", FlowTexts.Rejection(RejectReason.NoAttemptsLeft, null, c));
            Assert.AreEqual("돈이 부족합니다", FlowTexts.Rejection(RejectReason.NotEnoughMoney, null, c));
            Assert.AreEqual("접는 중이 아닙니다", FlowTexts.Rejection(RejectReason.NoFold, null, c));
            Assert.AreEqual("알 수 없는 동작입니다", FlowTexts.Rejection(RejectReason.InvalidAction, null, c));
            Assert.AreEqual("", FlowTexts.Rejection((RejectReason)99, frog, c));
        }

        [Test]
        public void Rejection_NonLockedReasonsIgnoreModelAndCounts()
        {
            var frog = OrigamiCatalog.FindModel(ModelIds.Frog);
            Assert.AreEqual("돈이 부족합니다", FlowTexts.Rejection(RejectReason.NotEnoughMoney, frog, null));
        }

        [Test]
        public void ScreenLines_Null_Throws()
        {
            // Decision (ORIGAMI-004): a null flow is a programmer error (C-020 exception policy).
            Assert.Throws<ArgumentNullException>(() => FlowTexts.ScreenLines(null));
        }

        [Test]
        public void ScreenLines_ReturnsFreshReadOnlyList()
        {
            var flow = new OrigamiFlow();
            var a = FlowTexts.ScreenLines(flow);
            var b = FlowTexts.ScreenLines(flow);
            Assert.AreNotSame(a, b);
            CollectionAssert.AreEqual(a, b);
            Assert.IsTrue(a is System.Collections.Generic.ICollection<string> c && c.IsReadOnly, "read-only list");
        }
    }
}
