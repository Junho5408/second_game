using System;
using System.Collections.Generic;
using NUnit.Framework;
using Origami.Core.Catalog;
using Origami.Core.Folding;
using Origami.Core.Game;
using Origami.Core.Texts;
using Origami.Core.Tutorial;
using static Origami.Tests.Texts.TextScenarios;

namespace Origami.Tests.Texts
{
    // C-170 GameTexts — F-013 AC-01/AC-02, F-010 result texts (R-012), F-006 guide titles, support for F-011 AC-07 / F-012 AC-09.
    // Expected strings: FEATURE_SPECS F-013 / GAME_RULES R-012 / CONTENT §6·§8 literally; line formats fixed by ORIGAMI-011 (C-170).
    public class GameTextsTests
    {
        // ------------------------------------------------------------------------------------------------ F-013 AC-01

        [TestCase(RejectReason.SheetOnTable, "테이블 위 종이를 먼저 완성하거나 버리세요")]
        [TestCase(RejectReason.NoSheetsLeft, "오늘은 종이를 다 썼어요. 침대에서 쉬세요")]
        [TestCase(RejectReason.NotEnoughMoney, "돈이 모자라요")]
        [TestCase(RejectReason.NoFolds, "아직 접지 않았어요")]
        [TestCase(RejectReason.NothingToUndo, "되돌릴 것이 없어요")]
        [TestCase(RejectReason.AlreadyOwned, "이미 가진 페이지예요")]
        [TestCase(RejectReason.NoGuide, "")]
        [TestCase(RejectReason.WrongMode, "")]
        [TestCase(RejectReason.UnknownTarget, "")]
        [TestCase(RejectReason.UnknownModel, "")]
        [TestCase(RejectReason.RunEnded, "")]
        [TestCase(RejectReason.None, "")]
        [TestCase(RejectReason.NoSheet, "")]
        public void F013_AC01_RejectMessageTable(RejectReason reason, string expected)
        {
            Assert.AreEqual(expected, GameTexts.RejectMessage(reason, FoldError.None));
            // the fold error only matters for InvalidFold
            Assert.AreEqual(expected, GameTexts.RejectMessage(reason, FoldError.NothingGrabbed));
        }

        [TestCase(FoldError.DragTooShort, "조금 더 끌어 주세요")]
        [TestCase(FoldError.NothingGrabbed, "잡은 곳에 종이가 없어요")]
        [TestCase(FoldError.NothingToFold, "접히는 부분이 없어요")]
        [TestCase(FoldError.LineOutsidePaper, "접는 선이 종이 밖이에요")]
        [TestCase(FoldError.InvalidLayers, "")]
        [TestCase(FoldError.None, "")]
        public void F013_AC01_InvalidFoldUsesFoldErrorText(FoldError error, string expected)
        {
            Assert.AreEqual(expected, GameTexts.RejectMessage(RejectReason.InvalidFold, error));
        }

        [Test]
        public void F013_AC01_EveryReasonHasADefinedText()
        {
            foreach (RejectReason r in Enum.GetValues(typeof(RejectReason)))
                foreach (FoldError e in Enum.GetValues(typeof(FoldError)))
                    Assert.IsNotNull(GameTexts.RejectMessage(r, e), r + "/" + e);
            Assert.AreEqual("", GameTexts.RejectMessage((RejectReason)999, FoldError.None));
        }

        [Test]
        public void F013_AC01_RealRejectionsMapToText()
        {
            var g = new OrigamiGame();
            Sit(g);
            Assert.AreEqual("", Text(g.Finish()), "no sheet → NoSheet is silent");
            Take(g);
            Assert.AreEqual("아직 접지 않았어요", Text(g.Finish()));
            Assert.AreEqual("되돌릴 것이 없어요", Text(g.Undo()));
            Assert.AreEqual("테이블 위 종이를 먼저 완성하거나 버리세요", Text(g.TakeSheet()));
            Assert.AreEqual("조금 더 끌어 주세요", Text(g.Fold(V(0, 0), V(0, 0), false, 0)));
            Assert.AreEqual("", Text(g.Interact(InteractTargets.Bookshelf)), "WrongMode is silent");
        }

        private static string Text(CommandResult r) => GameTexts.RejectMessage(r.Reason, r.FoldError);

        // ------------------------------------------------------------------------------------------------ F-013 AC-02, HUD

        [Test]
        public void F013_AC02_HudAfterF011AC01()
        {
            var g = new OrigamiGame();
            Sit(g);
            for (int i = 0; i < 5; i++) Ref(g, ModelIds.Plane);
            Night(g);
            var hud = GameTexts.Hud(g.Snapshot);
            Assert.AreEqual(2, hud.Day);
            Assert.AreEqual(77, hud.Money);
            Assert.AreEqual(5, hud.SheetsLeft);
            Assert.AreEqual("집", hud.PopularName);
            CollectionAssert.AreEqual(new[] { "2일차 / 7", "돈 77원", "오늘 남은 종이 5 / 5", "오늘의 인기: 집" }, hud.Lines);
        }

        [Test]
        public void F013_HudNewRunAndSeatedSheetLine()
        {
            var g = new OrigamiGame();
            var start = new[] { "1일차 / 7", "돈 20원", "오늘 남은 종이 5 / 5", "오늘의 인기: 종이비행기" };
            CollectionAssert.AreEqual(start, GameTexts.Hud(g.Snapshot).Lines);
            Assert.AreEqual("종이비행기", GameTexts.Hud(g.Snapshot).PopularName);

            Sit(g);
            CollectionAssert.AreEqual(start, GameTexts.Hud(g.Snapshot).Lines, "seated without a sheet: no count line");
            Take(g);
            CollectionAssert.AreEqual(new[] { "1일차 / 7", "돈 20원", "오늘 남은 종이 4 / 5", "오늘의 인기: 종이비행기", "접은 횟수 0 · 자국 0" },
                GameTexts.Hud(g.Snapshot).Lines);
            var plane = ModelCatalog.Find(ModelIds.Plane).Steps;
            Apply(g, plane[0]);
            Apply(g, plane[1]);
            Apply(g, plane[2]);
            Assert.AreEqual("접은 횟수 2 · 자국 1", GameTexts.Hud(g.Snapshot).Lines[4]);
            Stand(g);
            Assert.AreEqual(4, GameTexts.Hud(g.Snapshot).Lines.Count, "walking: the sheet line is a seated line");
        }

        [Test]
        public void Hud_NullThrowsAndLinesAreReadOnly()
        {
            Assert.Throws<ArgumentNullException>(() => GameTexts.Hud(null));
            var lines = GameTexts.Hud(new OrigamiGame().Snapshot).Lines;
            Assert.Throws<NotSupportedException>(() => ((IList<string>)lines).Add("x"));
        }

        // ------------------------------------------------------------------------------------------------ F-013 prompts / hints

        [TestCase("table", "E: 앉기")]
        [TestCase("bookshelf", "E: 책 읽기")]
        [TestCase("counter", "E: 판매대 보기")]
        [TestCase("bed", "E: 잠자기")]
        [TestCase(null, "")]
        [TestCase("", "")]
        [TestCase("Table", "")]
        [TestCase("trash", "")]
        public void F013_InteractPrompt(string target, string expected)
        {
            Assert.AreEqual(expected, GameTexts.InteractPrompt(target));
        }

        [Test]
        public void F013_HintTexts()
        {
            Assert.AreEqual("WASD/방향키로 걸어 작업 테이블로 가세요", GameTexts.WalkHint);
            Assert.AreEqual("Space: 종이 꺼내기", GameTexts.TakeSheetHint);
            Assert.AreEqual("이제부터 접는 법은 책에서만 볼 수 있어요. 앉아서 B 또는 책장에서 E", GameTexts.PostTutorialHint);
        }

        [Test]
        public void ModelName_CatalogueNames()
        {
            Assert.AreEqual("종이비행기", GameTexts.ModelName("plane"));
            Assert.AreEqual("집", GameTexts.ModelName("house"));
            Assert.AreEqual("튤립", GameTexts.ModelName("tulip"));
            Assert.AreEqual("강아지 얼굴", GameTexts.ModelName("dog"));
            Assert.AreEqual("매미", GameTexts.ModelName("cicada"));
            Assert.AreEqual("", GameTexts.ModelName(null));
            Assert.AreEqual("", GameTexts.ModelName(""));
            Assert.AreEqual("", GameTexts.ModelName("Plane"));
            Assert.AreEqual("", GameTexts.ModelName("unicorn"));
        }

        // ------------------------------------------------------------------------------------------------ F-010 / R-012

        [Test]
        public void F010_AC01_PieceLine()
        {
            var g = new OrigamiGame();
            Sit(g);
            var r = Ref(g, ModelIds.House);
            Assert.AreEqual("집 ★3 (일치도 100%)", GameTexts.RecognitionLine(r.Recognition));
            Assert.AreEqual("종이비행기 ★3 (일치도 100%)", GameTexts.RecognitionLine(Ref(g, ModelIds.Plane).Recognition));
        }

        [Test]
        public void F010_AC03_CrumpledLineNamesTheMostSimilarModel()
        {
            var g = new OrigamiGame();
            Sit(g);
            Take(g);
            Ok(g.Fold(V(-1, 1), V(-1, -1), false, 0), "Fold");
            var r = Ok(g.Finish(), "Finish").Recognition;
            Assert.IsFalse(r.IsPiece);
            Assert.AreEqual("house", r.BestModelId);
            // the percent value itself is not asserted (J values, review W-6); only the format around it
            Assert.AreEqual("구겨진 종이 (가장 비슷한 작품: 집 " + r.Percent.ToString(System.Globalization.CultureInfo.InvariantCulture) + "%)",
                GameTexts.RecognitionLine(r));
        }

        [Test]
        public void RecognitionLine_NullThrows()
        {
            Assert.Throws<ArgumentNullException>(() => GameTexts.RecognitionLine(null));
        }

        // ------------------------------------------------------------------------------------------------ F-006 guide titles

        [Test]
        public void F006_GuideTitles()
        {
            var steps = PlaneGuide.Steps;
            Assert.AreEqual("종이비행기 1 / 6 — Ctrl을 누른 채 놓으세요", GameTexts.GuideTitle(steps[0]));
            for (int n = 2; n <= 6; n++) Assert.AreEqual("종이비행기 " + n + " / 6", GameTexts.GuideTitle(steps[n - 1]));
            Assert.Throws<ArgumentNullException>(() => GameTexts.GuideTitle(null));

            var g = new OrigamiGame();
            Sit(g);
            Take(g);
            Assert.AreEqual("종이비행기 1 / 6 — Ctrl을 누른 채 놓으세요", GameTexts.GuideTitle(g.Snapshot.Sheet.Guide));
            Apply(g, ModelCatalog.Find(ModelIds.Plane).Steps[0]);
            Assert.AreEqual("종이비행기 2 / 6", GameTexts.GuideTitle(g.Snapshot.Sheet.Guide));
        }

        // ------------------------------------------------------------------------------------------------ settlement (F-011)

        [Test]
        public void F011_AC01_SettlementLines()
        {
            var g = new OrigamiGame();
            Sit(g);
            for (int i = 0; i < 5; i++) Ref(g, ModelIds.Plane);
            var lines = GameTexts.SettlementLines(Night(g).Settlement);
            var line = "종이비행기 ★3 13원 (인기)";
            CollectionAssert.AreEqual(new[] { "1일차 정산", line, line, line, line, line, "합계 65원", "정산 후 돈 77원", "내일의 인기: 집" }, lines);
        }

        [Test]
        public void F011_AC03_SettlementLines()
        {
            var g = new OrigamiGame();
            Sit(g);
            Var(g, 0.06);
            Var(g, 0.15);
            Ref(g, ModelIds.House);
            var lines = GameTexts.SettlementLines(Night(g).Settlement);
            CollectionAssert.AreEqual(
                new[] { "1일차 정산", "집 ★2 8원", "집 ★1 4원", "집 ★3 12원", "합계 24원", "정산 후 돈 40원", "내일의 인기: 집" }, lines);
        }

        [Test]
        public void F011_AC05_EmptySettlementAndRunEnds()
        {
            var g = new OrigamiGame();
            CollectionAssert.AreEqual(new[] { "1일차 정산", "팔린 작품이 없어요", "합계 0원", "정산 후 돈 20원", "내일의 인기: 집" },
                GameTexts.SettlementLines(Night(g).Settlement));
            for (int d = 2; d < 7; d++) Night(g);
            CollectionAssert.AreEqual(new[] { "7일차 정산", "팔린 작품이 없어요", "합계 0원", "정산 후 돈 20원", "7일이 모두 지났어요" },
                GameTexts.SettlementLines(Night(g).Settlement));
            Assert.Throws<ArgumentNullException>(() => GameTexts.SettlementLines(null));
        }

        [Test]
        public void F001_AC05_BankruptSettlementLine()
        {
            var g = BankruptRun(out var last);
            Assert.IsTrue(g.Snapshot.IsEnded);
            CollectionAssert.AreEqual(new[] { "3일차 정산", "팔린 작품이 없어요", "합계 0원", "정산 후 돈 0원", "돈이 모자라 공방 문을 닫았어요" },
                GameTexts.SettlementLines(last.Settlement));
        }

        // ------------------------------------------------------------------------------------------------ counter (F-011 AC-07 data)

        [Test]
        public void F011_AC07_CounterLines()
        {
            var g = new OrigamiGame();
            CollectionAssert.AreEqual(new[] { "1일차 판매대", "오늘의 인기: 종이비행기", "진열된 작품이 없어요", "합계 예상 0원" },
                GameTexts.CounterLines(g.Snapshot));
            Sit(g);
            Ref(g, ModelIds.House);
            Ref(g, ModelIds.Plane);
            CollectionAssert.AreEqual(new[]
            {
                "1일차 판매대", "오늘의 인기: 종이비행기", "집 ★3 일치도 100% · 오늘 팔면 12원", "종이비행기 ★3 일치도 100% · 오늘 팔면 13원",
                "합계 예상 25원",
            }, GameTexts.CounterLines(g.Snapshot));
            Assert.Throws<ArgumentNullException>(() => GameTexts.CounterLines(null));
        }

        // ------------------------------------------------------------------------------------------------ result (F-012 AC-09 data)

        [Test]
        public void F012_AC08_ResultLinesOfF001AC03Run()
        {
            var g = new OrigamiGame();
            for (int d = 0; d < 7; d++) Night(g);
            CollectionAssert.AreEqual(new[]
            {
                "빈손", "종이는 다 떨어졌지만, 종이접기는 취미로 남았다.", "최종 돈 20원",
                "종이비행기: ★3 0 · ★2 0 · ★1 0", "집: ★3 0 · ★2 0 · ★1 0", "튤립: ★3 0 · ★2 0 · ★1 0", "강아지 얼굴: ★3 0 · ★2 0 · ★1 0",
                "매미: ★3 0 · ★2 0 · ★1 0", "실패 0", "총 판매액 0원", "총 종이값 0원", "총 책값 0원",
            }, GameTexts.ResultLines(g.Snapshot));
        }

        [Test]
        public void F012_ResultLinesWithCompletions()
        {
            var g = new OrigamiGame();
            Sit(g);
            Ref(g, ModelIds.House);      // free tutorial sheet, house ★3
            Ref(g, ModelIds.Plane);      // 2원, plane ★3
            for (int d = 0; d < 7; d++) Night(g);   // day 1: 12 + 13 = 25 → 43 → E-05
            CollectionAssert.AreEqual(new[]
            {
                "동네 종이접기 공방", "소박하지만 단골이 생긴 동네 공방. 다음 주도 문을 연다.", "최종 돈 43원",
                "종이비행기: ★3 1 · ★2 0 · ★1 0", "집: ★3 1 · ★2 0 · ★1 0", "튤립: ★3 0 · ★2 0 · ★1 0", "강아지 얼굴: ★3 0 · ★2 0 · ★1 0",
                "매미: ★3 0 · ★2 0 · ★1 0", "실패 0", "총 판매액 25원", "총 종이값 2원", "총 책값 0원",
            }, GameTexts.ResultLines(g.Snapshot));
        }

        [Test]
        public void F012_ResultLinesOfBankruptRun()
        {
            var g = BankruptRun(out _);
            var lines = GameTexts.ResultLines(g.Snapshot);
            Assert.AreEqual("빈손", lines[0]);
            Assert.AreEqual("최종 돈 0원", lines[2]);
            Assert.AreEqual("실패 11", lines[8]);
            Assert.AreEqual("총 종이값 20원", lines[10]);
        }

        [Test]
        public void ResultLines_BeforeTheEndHaveNoEndingLines()
        {
            var lines = GameTexts.ResultLines(new OrigamiGame().Snapshot);
            Assert.AreEqual("최종 돈 20원", lines[0]);
            Assert.AreEqual(10, lines.Count);
            Assert.Throws<ArgumentNullException>(() => GameTexts.ResultLines(null));
        }

        /// <summary>F-001 AC-05 (bankruptcy): returns the game and the last Sleep result.</summary>
        private static OrigamiGame BankruptRun(out CommandResult last)
        {
            var g = new OrigamiGame();
            Sit(g);
            for (int i = 0; i < 5; i++) Waste(g);
            Night(g);
            Sit(g);
            for (int i = 0; i < 5; i++) Waste(g);
            Night(g);
            Sit(g);
            Waste(g);
            last = Night(g);
            Assert.AreEqual(0, g.Snapshot.Money);
            Assert.AreEqual(11, g.Snapshot.FailureCount);
            return g;
        }
    }
}
