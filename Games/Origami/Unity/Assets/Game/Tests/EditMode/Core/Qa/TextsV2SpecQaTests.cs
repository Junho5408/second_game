using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Origami.Core.Economy;
using Origami.Core.Folding;
using Origami.Core.Game;
using Origami.Core.Recognition;
using Origami.Core.Texts;
using Origami.Core.Tutorial;
using static Origami.Tests.Qa.WsDriver;

namespace Origami.Tests.Qa
{
    /// <summary>
    /// QA ORIGAMI-011: black-box checks of C-170 <see cref="GameTexts"/> / <see cref="HudData"/> against F-013, F-006, R-012,
    /// F-010/F-011/F-012 and CONTENT §5·§6·§8. Fixed sentences are typed from the specs. Line formats that C-170 leaves to 011 are
    /// checked against the formats documented in the GameTexts remarks. J-case / dog-without-chin percents are never asserted:
    /// expected strings use <see cref="RecognitionResult.Percent"/>.
    /// </summary>
    public class TextsV2SpecQaTests
    {
        // ------------------------------------------------------------------ F-013 AC-01 reject table (spec wording)

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
        public void F013_AC01_RejectText(RejectReason reason, string expected)
        {
            // The fold error argument must not matter for non-InvalidFold reasons.
            foreach (FoldError e in Enum.GetValues(typeof(FoldError)))
                Assert.That(GameTexts.RejectMessage(reason, e), Is.EqualTo(expected), reason + " / " + e);
        }

        [Test]
        public void F013_AC01_InvalidFold_UsesTrialTextsPlusNothingGrabbed()
        {
            Assert.That(GameTexts.RejectMessage(RejectReason.InvalidFold, FoldError.NothingGrabbed), Is.EqualTo("잡은 곳에 종이가 없어요"));
            foreach (FoldError e in Enum.GetValues(typeof(FoldError)))
                Assert.That(GameTexts.RejectMessage(RejectReason.InvalidFold, e), Is.EqualTo(FoldTexts.ErrorMessage(e)), e.ToString());
            Assert.That(GameTexts.RejectMessage(RejectReason.InvalidFold, FoldError.DragTooShort), Is.Not.Empty);
        }

        [Test]
        public void F013_AC01_EveryReasonIsDefinedAndNeverNull()
        {
            foreach (RejectReason r in Enum.GetValues(typeof(RejectReason)))
                Assert.That(GameTexts.RejectMessage(r, FoldError.None), Is.Not.Null, r.ToString());
            // undefined enum value → treated as silent
            Assert.That(GameTexts.RejectMessage((RejectReason)999, FoldError.None), Is.EqualTo(""));
        }

        [Test]
        public void F013_RealRejectionsFromTheGame_MapToSpecTexts()
        {
            var g = new OrigamiGame();
            Sit(g);
            Ok(g, x => x.TakeSheet());
            var r = Rej(g, x => x.TakeSheet(), RejectReason.SheetOnTable);
            Assert.That(GameTexts.RejectMessage(r.Reason, r.FoldError), Is.EqualTo("테이블 위 종이를 먼저 완성하거나 버리세요"));
            r = Rej(g, x => x.Finish(), RejectReason.NoFolds);
            Assert.That(GameTexts.RejectMessage(r.Reason, r.FoldError), Is.EqualTo("아직 접지 않았어요"));
            r = Rej(g, x => x.Undo(), RejectReason.NothingToUndo);
            Assert.That(GameTexts.RejectMessage(r.Reason, r.FoldError), Is.EqualTo("되돌릴 것이 없어요"));
            r = g.Fold(V(-1, 1), V(-1, 1.0000001), false, 0);
            Assert.That(r.Reason, Is.EqualTo(RejectReason.InvalidFold));
            Assert.That(GameTexts.RejectMessage(r.Reason, r.FoldError), Is.EqualTo(FoldTexts.ErrorMessage(r.FoldError)).And.Not.Empty);
            Ok(g, x => x.OpenBook());
            r = Rej(g, x => x.BuyPage("house"), RejectReason.AlreadyOwned);
            Assert.That(GameTexts.RejectMessage(r.Reason, r.FoldError), Is.EqualTo("이미 가진 페이지예요"));
            r = Rej(g, x => x.BuyPage("cicada"), RejectReason.NotEnoughMoney);
            Assert.That(GameTexts.RejectMessage(r.Reason, r.FoldError), Is.EqualTo("돈이 모자라요"));
        }

        // ------------------------------------------------------------------ F-013 fixed hints / prompts (spec + CONTENT §6)

        [Test]
        public void F013_HintsAndPrompts_ExactWording()
        {
            Assert.That(GameTexts.WalkHint, Is.EqualTo("WASD/방향키로 걸어 작업 테이블로 가세요"));
            Assert.That(GameTexts.TakeSheetHint, Is.EqualTo("Space: 종이 꺼내기"));
            Assert.That(GameTexts.PostTutorialHint, Is.EqualTo("이제부터 접는 법은 책에서만 볼 수 있어요. 앉아서 B 또는 책장에서 E"));
            Assert.That(GameTexts.InteractPrompt("table"), Is.EqualTo("E: 앉기"));
            Assert.That(GameTexts.InteractPrompt("bookshelf"), Is.EqualTo("E: 책 읽기"));
            Assert.That(GameTexts.InteractPrompt("counter"), Is.EqualTo("E: 판매대 보기"));
            Assert.That(GameTexts.InteractPrompt("bed"), Is.EqualTo("E: 잠자기"));
            foreach (var other in new[] { null, "", "Table", "door", " bed" })
                Assert.That(GameTexts.InteractPrompt(other), Is.EqualTo(""), "prompt for " + (other ?? "null"));
        }

        [Test]
        public void ModelNames_MatchContentSection1()
        {
            Assert.That(GameTexts.ModelName("plane"), Is.EqualTo("종이비행기"));
            Assert.That(GameTexts.ModelName("house"), Is.EqualTo("집"));
            Assert.That(GameTexts.ModelName("tulip"), Is.EqualTo("튤립"));
            Assert.That(GameTexts.ModelName("dog"), Is.EqualTo("강아지 얼굴"));
            Assert.That(GameTexts.ModelName("cicada"), Is.EqualTo("매미"));
            Assert.That(GameTexts.ModelName(null), Is.EqualTo(""));
            Assert.That(GameTexts.ModelName("unicorn"), Is.EqualTo(""));
            Assert.That(GameTexts.ModelName("Plane"), Is.EqualTo(""));
        }

        // ------------------------------------------------------------------ F-006 guide titles

        [Test]
        public void F006_GuideTitles_ForAllSixSteps()
        {
            var steps = PlaneGuide.Steps;
            Assert.That(steps.Count, Is.EqualTo(6));
            Assert.That(GameTexts.GuideTitle(steps[0]), Is.EqualTo("종이비행기 1 / 6 — Ctrl을 누른 채 놓으세요"));
            for (int n = 2; n <= 6; n++)
                Assert.That(GameTexts.GuideTitle(steps[n - 1]), Is.EqualTo("종이비행기 " + n + " / 6"), "step " + n);
        }

        [Test]
        public void F006_GuideTitle_FollowsTheLiveGuide()
        {
            var g = new OrigamiGame();
            Sit(g);
            Ok(g, x => x.TakeSheet());
            Assert.That(GameTexts.GuideTitle(g.Snapshot.Sheet.Guide), Is.EqualTo("종이비행기 1 / 6 — Ctrl을 누른 채 놓으세요"));
            int i = 0;
            foreach (var row in V2SpecBook.Book("plane").Take(5))
            {
                Apply(g, row, "plane " + (++i));
                Assert.That(GameTexts.GuideTitle(g.Snapshot.Sheet.Guide), Is.EqualTo("종이비행기 " + (i + 1) + " / 6"));
            }
        }

        // ------------------------------------------------------------------ R-012 / F-010 recognition line

        [Test]
        public void R012_RecognitionLine_PieceAndCrumpled()
        {
            var g = new OrigamiGame();
            Sit(g);
            var house = Ref(g, "house");
            Assert.That(GameTexts.RecognitionLine(house), Is.EqualTo("집 ★3 (일치도 100%)"));
            var plane = Ref(g, "plane");
            Assert.That(GameTexts.RecognitionLine(plane), Is.EqualTo("종이비행기 ★3 (일치도 100%)"));

            // F-010 AC-03 one fold → crumpled, best model house (percent taken from the result, not asserted).
            Ok(g, x => x.TakeSheet());
            Ok(g, x => x.Fold(V(-1, 1), V(-1, -1), false, 0));
            var crumpled = Ok(g, x => x.Finish()).Recognition;
            Assert.That(crumpled.IsPiece, Is.False);
            Assert.That(crumpled.BestModelId, Is.EqualTo("house"));
            Assert.That(GameTexts.RecognitionLine(crumpled), Is.EqualTo("구겨진 종이 (가장 비슷한 작품: 집 " + crumpled.Percent + "%)"));

            // Var(0.06) / Var(0.15): house ★2 / ★1 (grades from F-011 AC-03, percent from the result).
            var v2 = Var(g, 0.06);
            Assert.That(v2.Grade, Is.EqualTo(Grade.Star2));
            Assert.That(GameTexts.RecognitionLine(v2), Is.EqualTo("집 ★2 (일치도 " + v2.Percent + "%)"));
            var v1 = Var(g, 0.15);
            Assert.That(v1.Grade, Is.EqualTo(Grade.Star1));
            Assert.That(GameTexts.RecognitionLine(v1), Is.EqualTo("집 ★1 (일치도 " + v1.Percent + "%)"));
        }

        // ------------------------------------------------------------------ F-013 AC-02 HUD

        [Test]
        public void F013_AC02_HudAfterF011AC01()
        {
            var g = new OrigamiGame();
            Sit(g);
            Times(5, () => Ref3(g, "plane"));
            Night(g);
            var hud = GameTexts.Hud(g.Snapshot);
            Assert.That((hud.Day, hud.Money, hud.SheetsLeft, hud.PopularName), Is.EqualTo((2, 77, 5, "집")));
            Assert.That(hud.Lines, Is.EqualTo(new[] { "2일차 / 7", "돈 77원", "오늘 남은 종이 5 / 5", "오늘의 인기: 집" }));
        }

        [Test]
        public void F013_Hud_SeatedSheetLine_OnlyWhenSeatedWithSheet()
        {
            var g = new OrigamiGame();
            var hud = GameTexts.Hud(g.Snapshot);
            Assert.That(hud.Lines, Is.EqualTo(new[] { "1일차 / 7", "돈 20원", "오늘 남은 종이 5 / 5", "오늘의 인기: 종이비행기" }));
            Sit(g);
            Assert.That(GameTexts.Hud(g.Snapshot).Lines.Count, Is.EqualTo(4), "seated without sheet");
            Ok(g, x => x.TakeSheet());
            Assert.That(GameTexts.Hud(g.Snapshot).Lines.Last(), Is.EqualTo("접은 횟수 0 · 자국 0"));
            var rows = V2SpecBook.Book("house");
            Apply(g, rows[0], "h1");
            Apply(g, rows[1], "h2");
            Apply(g, rows[2], "h3");
            Apply(g, rows[3], "h4");
            Assert.That(GameTexts.Hud(g.Snapshot).Lines.Last(), Is.EqualTo("접은 횟수 2 · 자국 2"));
            Ok(g, x => x.OpenBook());
            Assert.That(GameTexts.Hud(g.Snapshot).Lines.Last(), Is.EqualTo("접은 횟수 2 · 자국 2"), "book open is still seated");
            Ok(g, x => x.Undo());
            Assert.That(GameTexts.Hud(g.Snapshot).Lines.Last(), Is.EqualTo("접은 횟수 1 · 자국 2"));
            Stand(g);
            var walking = GameTexts.Hud(g.Snapshot);
            Assert.That(walking.Lines.Count, Is.EqualTo(4), "walking with a sheet left on the table: no fold line");
            Assert.That(walking.Lines[2], Is.EqualTo("오늘 남은 종이 4 / 5"));
        }

        [Test]
        public void F013_Hud_PopularNameForEachDay_ContentSection5()
        {
            var expected = new[] { "종이비행기", "집", "튤립", "집", "강아지 얼굴", "튤립", "매미" };
            var g = new OrigamiGame();
            for (int day = 1; day <= 7; day++)
            {
                var hud = GameTexts.Hud(g.Snapshot);
                Assert.That(hud.Day, Is.EqualTo(day));
                Assert.That(hud.PopularName, Is.EqualTo(expected[day - 1]), "day " + day);
                Assert.That(hud.Lines[0], Is.EqualTo(day + "일차 / 7"));
                Assert.That(hud.Lines[3], Is.EqualTo("오늘의 인기: " + expected[day - 1]));
                if (day < 7) Night(g);
            }
        }

        // ------------------------------------------------------------------ settlement / counter / result lines (F-011, F-012)

        [Test]
        public void F011_AC03_SettlementLines()
        {
            var g = new OrigamiGame();
            Sit(g);
            Var(g, 0.06);
            Var(g, 0.15);
            Ref3(g, "house");
            Assert.That(g.Snapshot.Money, Is.EqualTo(16));

            // Counter preview before the night (F-011 AC-07 data): day 1, popular plane, house normal prices 8 / 4 / 12.
            var counter = GameTexts.CounterLines(g.Snapshot);
            var disp = g.Snapshot.Display;
            Assert.That(counter, Is.EqualTo(new[]
            {
                "1일차 판매대", "오늘의 인기: 종이비행기",
                "집 ★2 일치도 " + disp[0].Percent + "% · 오늘 팔면 8원",
                "집 ★1 일치도 " + disp[1].Percent + "% · 오늘 팔면 4원",
                "집 ★3 일치도 100% · 오늘 팔면 12원",
                "합계 예상 24원",
            }));

            var s = Night(g);
            Assert.That(s.Lines.Count, Is.EqualTo(3));
            Assert.That(s.Total, Is.EqualTo(24));
            Assert.That(s.MoneyAfter, Is.EqualTo(40));
            Assert.That(GameTexts.SettlementLines(s), Is.EqualTo(new[]
            {
                "1일차 정산", "집 ★2 8원", "집 ★1 4원", "집 ★3 12원", "합계 24원", "정산 후 돈 40원", "내일의 인기: 집",
            }));
        }

        [Test]
        public void F011_AC01_SettlementLines_PopularMarked()
        {
            var g = new OrigamiGame();
            Sit(g);
            Times(5, () => Ref3(g, "plane"));
            var counter = GameTexts.CounterLines(g.Snapshot);
            Assert.That(counter.Count, Is.EqualTo(2 + 5 + 1));
            Assert.That(counter[2], Is.EqualTo("종이비행기 ★3 일치도 100% · 오늘 팔면 13원"));
            Assert.That(counter.Last(), Is.EqualTo("합계 예상 65원"));
            var s = Night(g);
            var lines = GameTexts.SettlementLines(s);
            Assert.That(lines.Count, Is.EqualTo(1 + 5 + 3));
            for (int i = 1; i <= 5; i++) Assert.That(lines[i], Is.EqualTo("종이비행기 ★3 13원 (인기)"));
            Assert.That(lines[6], Is.EqualTo("합계 65원"));
            Assert.That(lines[7], Is.EqualTo("정산 후 돈 77원"));
            Assert.That(lines[8], Is.EqualTo("내일의 인기: 집"));
        }

        [Test]
        public void F011_AC05_EmptyNight_AndEmptyCounter()
        {
            var g = new OrigamiGame();
            Assert.That(GameTexts.CounterLines(g.Snapshot), Is.EqualTo(new[] { "1일차 판매대", "오늘의 인기: 종이비행기", "진열된 작품이 없어요", "합계 예상 0원" }));
            var s = Night(g);
            Assert.That(GameTexts.SettlementLines(s), Is.EqualTo(new[] { "1일차 정산", "팔린 작품이 없어요", "합계 0원", "정산 후 돈 20원", "내일의 인기: 집" }));
        }

        [Test]
        public void F012_AC08_ResultLines_E01_AfterSevenEmptyNights()
        {
            var g = new OrigamiGame();
            SettlementResult last = null;
            for (int i = 0; i < 7; i++) last = Night(g);
            Assert.That(g.Snapshot.IsEnded, Is.True);
            Assert.That(last.EndReason, Is.EqualTo(RunEndReason.Completed));
            Assert.That(last.NextPopularModelId, Is.Null);
            Assert.That(GameTexts.SettlementLines(last).Last(), Is.EqualTo("7일이 모두 지났어요"));
            Assert.That(GameTexts.SettlementLines(last)[0], Is.EqualTo("7일차 정산"));
            Assert.That(GameTexts.ResultLines(g.Snapshot), Is.EqualTo(new[]
            {
                "빈손", "종이는 다 떨어졌지만, 종이접기는 취미로 남았다.", "최종 돈 20원",
                "종이비행기: ★3 0 · ★2 0 · ★1 0", "집: ★3 0 · ★2 0 · ★1 0", "튤립: ★3 0 · ★2 0 · ★1 0",
                "강아지 얼굴: ★3 0 · ★2 0 · ★1 0", "매미: ★3 0 · ★2 0 · ★1 0",
                "실패 0", "총 판매액 0원", "총 종이값 0원", "총 책값 0원",
            }));
        }

        [Test]
        public void F012_ResultLines_E05()
        {
            var g = new OrigamiGame();
            Sit(g);
            Ref3(g, "house");   // free sheet → day 1 sale 12 (house normal ★3)
            Var(g, 0.06);       // −2, house ★2 → 8
            Waste(g);           // −2, failure 1
            Night(g);           // 20 − 4 + 20 = 36
            for (int i = 0; i < 6; i++) Night(g);
            var s = g.Snapshot;
            Assert.That(s.IsEnded, Is.True);
            Assert.That(s.Money, Is.EqualTo(36));
            Assert.That(s.EndingId, Is.EqualTo("E-05"));
            Assert.That(GameTexts.ResultLines(s), Is.EqualTo(new[]
            {
                "동네 종이접기 공방", "소박하지만 단골이 생긴 동네 공방. 다음 주도 문을 연다.", "최종 돈 36원",
                "종이비행기: ★3 0 · ★2 0 · ★1 0", "집: ★3 1 · ★2 1 · ★1 0", "튤립: ★3 0 · ★2 0 · ★1 0",
                "강아지 얼굴: ★3 0 · ★2 0 · ★1 0", "매미: ★3 0 · ★2 0 · ★1 0",
                "실패 1", "총 판매액 20원", "총 종이값 4원", "총 책값 0원",
            }));
        }

        [Test]
        public void F012_Bankrupt_SettlementLastLine_AndResultE01()
        {
            // F-001 AC-05 path
            var g = new OrigamiGame();
            Sit(g);
            Times(5, () => Waste(g));
            Night(g);
            Sit(g);
            Times(5, () => Waste(g));
            Night(g);
            Sit(g);
            Waste(g);
            var last = Night(g);
            Assert.That(last.EndReason, Is.EqualTo(RunEndReason.Bankrupt));
            var lines = GameTexts.SettlementLines(last);
            Assert.That(lines[0], Is.EqualTo("3일차 정산"));
            Assert.That(lines.Last(), Is.EqualTo("돈이 모자라 공방 문을 닫았어요"));
            var result = GameTexts.ResultLines(g.Snapshot);
            Assert.That(result[0], Is.EqualTo("빈손"));
            Assert.That(result[2], Is.EqualTo("최종 돈 0원"));
            Assert.That(result[8], Is.EqualTo("실패 11"));
            Assert.That(result[10], Is.EqualTo("총 종이값 20원"));
        }

        [Test]
        public void ResultLines_DuringRun_HasNoEndingLines()
        {
            var g = new OrigamiGame();
            var lines = GameTexts.ResultLines(g.Snapshot);
            Assert.That(lines[0], Is.EqualTo("최종 돈 20원"));
            Assert.That(lines.Count, Is.EqualTo(10));
        }

        [Test]
        public void NullArguments_Throw()
        {
            Assert.Throws<ArgumentNullException>(() => GameTexts.Hud(null));
            Assert.Throws<ArgumentNullException>(() => GameTexts.RecognitionLine(null));
            Assert.Throws<ArgumentNullException>(() => GameTexts.GuideTitle(null));
            Assert.Throws<ArgumentNullException>(() => GameTexts.SettlementLines(null));
            Assert.Throws<ArgumentNullException>(() => GameTexts.CounterLines(null));
            Assert.Throws<ArgumentNullException>(() => GameTexts.ResultLines(null));
        }

        [Test]
        public void TextsAreCultureInvariant()
        {
            var saved = System.Globalization.CultureInfo.CurrentCulture;
            try
            {
                var g = new OrigamiGame();
                Sit(g);
                Times(5, () => Ref3(g, "plane"));
                var s = Night(g);
                var expected = GameTexts.SettlementLines(s).ToArray();
                foreach (var name in new[] { "ar-SA", "fa-IR", "de-DE", "hi-IN" })
                {
                    System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo(name);
                    Assert.That(GameTexts.SettlementLines(s), Is.EqualTo(expected), name);
                    Assert.That(GameTexts.Hud(g.Snapshot).Lines[1], Is.EqualTo("돈 77원"), name);
                }
            }
            finally
            {
                System.Globalization.CultureInfo.CurrentCulture = saved;
            }
        }
    }
}
