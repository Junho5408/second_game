using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Origami.Core.Economy;
using Origami.Core.Folding;
using Origami.Core.Game;
using Origami.Core.Geometry;
using Origami.Core.Recognition;
using Origami.Core.Screens;
using Origami.Core.Texts;

namespace Origami.Tests.Qa
{
    /// <summary>
    /// QA ORIGAMI-011: black-box checks of C-171 <see cref="ScreenFlow"/>: a full 7-day run driven only by key presses
    /// (plus the fold/crease mouse commands), the F-003 key table and priority combinations re-derived by QA from C-171/F-003,
    /// message rules (F-013), sheet changes and the one-time hints lifecycle (F-013, F-006).
    /// </summary>
    public class ScreenFlowV2SpecQaTests
    {
        private const string Table = "table", Bookshelf = "bookshelf", Counter = "counter", Bed = "bed";

        private static Vec2 V(double x, double y) => new Vec2(x, y);

        private static bool Press(ScreenFlow f, GameKeys k, string candidate = null) => f.ProcessKeys(k, candidate);

        /// <summary>Key press that must do something.</summary>
        private static void Do(ScreenFlow f, GameKeys k, string candidate = null, string ctx = "")
        {
            Assert.That(Press(f, k, candidate), Is.True, $"{ctx} {k} should act");
        }

        /// <summary>Key press that must be a "—" cell: false and nothing changes.</summary>
        private static void Nothing(ScreenFlow f, GameKeys k, string candidate = null, string ctx = "")
        {
            var snap = f.Snapshot;
            var overlay = f.Overlay;
            int msg = f.Message.Serial;
            int chg = f.LastSheetChange.Serial;
            var pending = f.PendingSettlement;
            bool w = f.WalkHintVisible, t = f.TakeSheetHintVisible, p = f.PostTutorialHintVisible;
            Assert.That(Press(f, k, candidate), Is.False, $"{ctx} {k} should mean nothing");
            Assert.That(f.Snapshot, Is.SameAs(snap), ctx + " snapshot");
            Assert.That(f.Overlay, Is.EqualTo(overlay), ctx + " overlay");
            Assert.That(f.Message.Serial, Is.EqualTo(msg), ctx + " message");
            Assert.That(f.LastSheetChange.Serial, Is.EqualTo(chg), ctx + " sheet change");
            Assert.That(f.PendingSettlement, Is.SameAs(pending), ctx + " pending");
            Assert.That((f.WalkHintVisible, f.TakeSheetHintVisible, f.PostTutorialHintVisible), Is.EqualTo((w, t, p)), ctx + " hints");
        }

        private static void Apply(ScreenFlow f, V2SpecBook.Row row, string ctx)
        {
            var before = f.Snapshot.Sheet.State;
            int serial = f.LastSheetChange.Serial;
            var r = row.Kind == FoldStepKind.Crease ? f.Crease(row.Grab, row.Drop, row.Layers) : f.Fold(row.Grab, row.Drop, row.Mountain, row.Layers);
            Assert.That(r.Accepted, Is.True, ctx + " " + r.Reason);
            var c = f.LastSheetChange;
            Assert.That(c.Serial, Is.EqualTo(serial + 1), ctx + " change serial");
            Assert.That(c.Kind, Is.EqualTo(row.Kind == FoldStepKind.Crease ? SheetChangeKind.Creased : SheetChangeKind.Folded), ctx);
            Assert.That(c.Plan, Is.Not.Null.And.SameAs(r.Plan), ctx + " plan");
            Assert.That(c.Previous, Is.SameAs(before), ctx + " previous");
        }

        private static void FoldModel(ScreenFlow f, string id)
        {
            int i = 0;
            foreach (var row in V2SpecBook.Book(id)) Apply(f, row, id + " step " + (++i));
        }

        /// <summary>Space, the model's book sequence, Enter; checks the recognition toast.</summary>
        private static void MakeByKeys(ScreenFlow f, string id, string expectedLine)
        {
            Do(f, GameKeys.TakeSheet, null, "take " + id);
            Assert.That(f.LastSheetChange.Kind, Is.EqualTo(SheetChangeKind.Taken));
            FoldModel(f, id);
            int msg = f.Message.Serial;
            var sheet = f.Snapshot.Sheet.State;
            Do(f, GameKeys.Enter, null, "finish " + id);
            Assert.That(f.Message.Serial, Is.EqualTo(msg + 1), "recognition toast");
            Assert.That(f.Message.Kind, Is.EqualTo(MessageKind.Recognition));
            Assert.That(f.Message.Text, Is.EqualTo(expectedLine));
            Assert.That(f.LastSheetChange.Kind, Is.EqualTo(SheetChangeKind.Removed));
            Assert.That(f.LastSheetChange.Previous, Is.SameAs(sheet));
            Assert.That(f.Snapshot.Sheet, Is.Null);
        }

        private static void AssertNewRunState(ScreenFlow f)
        {
            var s = f.Snapshot;
            Assert.That(f.Overlay, Is.EqualTo(Overlay.None));
            Assert.That(f.PendingSettlement, Is.Null);
            Assert.That((s.Day, s.Money, s.SheetsLeft, s.Mode), Is.EqualTo((1, 20, 5, WorkshopMode.Walking)));
            Assert.That(s.OwnedModelIds, Is.EqualTo(new[] { "plane", "house" }));
            Assert.That(s.FreeSheetAvailable, Is.True);
            Assert.That(s.Sheet, Is.Null);
            Assert.That(s.BookOpen, Is.False);
            Assert.That(s.BookPage, Is.EqualTo(0));
            Assert.That(s.Display, Is.Empty);
            Assert.That(s.FailureCount, Is.EqualTo(0));
            Assert.That((s.TotalSales, s.TotalPaperCost, s.TotalBookCost), Is.EqualTo((0, 0, 0)));
            Assert.That(s.IsEnded, Is.False);
            Assert.That(s.HasSat, Is.False);
            Assert.That(f.WalkHintVisible, Is.True);
            Assert.That(f.TakeSheetHintVisible, Is.False);
            Assert.That(f.PostTutorialHintVisible, Is.False);
        }

        // ================================================================================== full scripted 7-day run

        [Test]
        public void FullSevenDayRun_ByKeysOnly_ThroughSettlementResultAndNewRun()
        {
            var f = new ScreenFlow();
            AssertNewRunState(f);
            Assert.That(f.Message.Serial, Is.EqualTo(0));
            Assert.That(f.Message.Kind, Is.EqualTo(MessageKind.None));
            Assert.That(f.Message.Text, Is.EqualTo(""));
            Assert.That(f.LastSheetChange.Kind, Is.EqualTo(SheetChangeKind.None));

            // ---------- day 1: walking hints, sit, tutorial sheet
            Nothing(f, GameKeys.Interact, null, "walking E without candidate");
            Nothing(f, GameKeys.Enter, Table, "walking Enter");
            Nothing(f, GameKeys.TakeSheet, Table, "walking Space");
            Do(f, GameKeys.Interact, Table, "sit");
            Assert.That(f.Snapshot.Mode, Is.EqualTo(WorkshopMode.Seated));
            Assert.That(f.WalkHintVisible, Is.False, "walk hint gone after first sit");
            Assert.That(f.TakeSheetHintVisible, Is.True, "Space hint before first sheet");
            Nothing(f, GameKeys.Escape, null, "seated Esc");
            Nothing(f, GameKeys.Interact, Bookshelf, "seated E");
            Nothing(f, GameKeys.PageRight, null, "seated → with book closed");

            Do(f, GameKeys.TakeSheet);
            Assert.That(f.TakeSheetHintVisible, Is.False);
            Assert.That(f.LastSheetChange.Kind, Is.EqualTo(SheetChangeKind.Taken));
            Assert.That(f.LastSheetChange.Previous, Is.Null);
            Assert.That(f.Snapshot.Sheet.IsTutorial, Is.True);
            Assert.That(f.Snapshot.Sheet.GuideVisible, Is.True);
            Assert.That(f.Snapshot.Money, Is.EqualTo(20), "first sheet free");
            Assert.That(GameTexts.GuideTitle(f.Snapshot.Sheet.Guide), Is.EqualTo("종이비행기 1 / 6 — Ctrl을 누른 채 놓으세요"));

            // Space with a sheet on the table → SheetOnTable toast
            Do(f, GameKeys.TakeSheet);
            Assert.That(f.Message.Kind, Is.EqualTo(MessageKind.Reject));
            Assert.That(f.Message.Text, Is.EqualTo("테이블 위 종이를 먼저 완성하거나 버리세요"));
            int serial = f.Message.Serial;
            Do(f, GameKeys.Enter, null, "finish without folds");
            Assert.That(f.Message.Serial, Is.EqualTo(serial + 1), "a repeated toast gets a new serial");
            Assert.That(f.Message.Text, Is.EqualTo("아직 접지 않았어요"));
            Do(f, GameKeys.Undo);
            Assert.That(f.Message.Text, Is.EqualTo("되돌릴 것이 없어요"));

            FoldModel(f, "plane");
            Assert.That(f.Snapshot.Sheet.GuideVisible, Is.False, "guide gone after 6 steps");
            Assert.That(f.Snapshot.Sheet.GuideScores.Select(x => x.Score), Is.All.EqualTo(100));
            Do(f, GameKeys.Enter, null, "finish tutorial");
            Assert.That(f.Message.Kind, Is.EqualTo(MessageKind.Recognition));
            Assert.That(f.Message.Text, Is.EqualTo("종이비행기 ★3 (일치도 100%)"));
            Assert.That(f.PostTutorialHintVisible, Is.True, "one-time hint after tutorial sheet");
            Assert.That(f.TakeSheetHintVisible, Is.False, "first sheet used");

            // no sheet: Enter/X/Z/R/F are rejected silently (NoSheet) but count as "did something"
            serial = f.Message.Serial;
            int chg = f.LastSheetChange.Serial;
            foreach (var k in new[] { GameKeys.Enter, GameKeys.Discard, GameKeys.Undo, GameKeys.Unfold, GameKeys.Flip, GameKeys.SkipGuide })
            {
                var snap = f.Snapshot;
                Do(f, k, null, "no-sheet " + k);
                Assert.That(f.Snapshot, Is.SameAs(snap), k.ToString());
            }
            Assert.That(f.Message.Serial, Is.EqualTo(serial), "NoSheet / NoGuide are silent");
            Assert.That(f.LastSheetChange.Serial, Is.EqualTo(chg));
            Assert.That(f.PostTutorialHintVisible, Is.True, "rejected commands do not clear the hint");

            // B opens the book (clears the one-time hint); ←/→ turn spreads; B closes
            Do(f, GameKeys.Book);
            Assert.That(f.Snapshot.BookOpen, Is.True);
            Assert.That(f.PostTutorialHintVisible, Is.False, "cleared by OpenBook");
            Do(f, GameKeys.PageLeft);
            Assert.That(f.Snapshot.BookPage, Is.EqualTo(0), "← on page 0 stays 0");
            Do(f, GameKeys.PageRight);
            Do(f, GameKeys.PageRight);
            Assert.That(f.Snapshot.BookPage, Is.EqualTo(4));
            Do(f, GameKeys.Book);
            Assert.That(f.Snapshot.BookOpen, Is.False);
            Nothing(f, GameKeys.PageLeft, null, "← with closed book");

            // paid sheet: house
            MakeByKeys(f, "house", "집 ★3 (일치도 100%)");
            Assert.That(f.Snapshot.Money, Is.EqualTo(18));
            Assert.That(f.PostTutorialHintVisible, Is.False, "not shown again for later sheets");
            Do(f, GameKeys.StandUp);
            Assert.That(f.Snapshot.Mode, Is.EqualTo(WorkshopMode.Walking));
            Assert.That(f.WalkHintVisible, Is.False, "walk hint only before the first sit");

            // counter: E opens, preview lines, E closes
            Do(f, GameKeys.Interact, Counter);
            Assert.That(f.Snapshot.Mode, Is.EqualTo(WorkshopMode.Counter));
            Assert.That(GameTexts.CounterLines(f.Snapshot), Is.EqualTo(new[]
            {
                "1일차 판매대", "오늘의 인기: 종이비행기",
                "종이비행기 ★3 일치도 100% · 오늘 팔면 13원", "집 ★3 일치도 100% · 오늘 팔면 12원", "합계 예상 25원",
            }));
            Nothing(f, GameKeys.Enter, null, "counter Enter");
            Nothing(f, GameKeys.PageRight, null, "counter →");
            Do(f, GameKeys.Interact, null, "counter E closes");
            Assert.That(f.Snapshot.Mode, Is.EqualTo(WorkshopMode.Walking));

            // bookshelf: page keeps the last spread (4), → twice, Enter nothing, Esc closes
            Do(f, GameKeys.Interact, Bookshelf);
            Assert.That(f.Snapshot.Mode, Is.EqualTo(WorkshopMode.Bookshelf));
            Assert.That(f.Snapshot.BookPage, Is.EqualTo(4));
            Do(f, GameKeys.PageRight);
            Do(f, GameKeys.PageRight);
            Assert.That(f.Snapshot.BookPage, Is.EqualTo(8));
            Nothing(f, GameKeys.Enter, null, "bookshelf Enter");
            Nothing(f, GameKeys.TakeSheet, null, "bookshelf Space");
            Do(f, GameKeys.Escape);
            Assert.That(f.Snapshot.Mode, Is.EqualTo(WorkshopMode.Walking));

            // bed: Esc = no, then Enter = yes → settlement overlay
            Do(f, GameKeys.Interact, Bed);
            Assert.That(f.Snapshot.Mode, Is.EqualTo(WorkshopMode.SleepConfirm));
            Nothing(f, GameKeys.Interact, Bed, "sleep confirm E");
            Do(f, GameKeys.Escape);
            Assert.That(f.Snapshot.Mode, Is.EqualTo(WorkshopMode.Walking));
            Assert.That(f.Snapshot.Day, Is.EqualTo(1));
            Do(f, GameKeys.Interact, Bed);
            Do(f, GameKeys.Enter);
            Assert.That(f.Overlay, Is.EqualTo(Overlay.Settlement));
            Assert.That(f.PendingSettlement, Is.Not.Null);
            Assert.That(GameTexts.SettlementLines(f.PendingSettlement), Is.EqualTo(new[]
            {
                "1일차 정산", "종이비행기 ★3 13원 (인기)", "집 ★3 12원", "합계 25원", "정산 후 돈 43원", "내일의 인기: 집",
            }));
            Assert.That(f.Snapshot.Day, Is.EqualTo(2));
            Assert.That(f.Snapshot.Mode, Is.EqualTo(WorkshopMode.Walking));
            // overlay blocks everything except Enter
            foreach (var k in new[] { GameKeys.Escape, GameKeys.Interact, GameKeys.TakeSheet, GameKeys.Book, GameKeys.PageRight })
                Nothing(f, k, Table, "settlement " + k);
            Nothing(f, GameKeys.Escape | GameKeys.Enter, null, "settlement Esc+Enter: Esc is highest and means nothing");
            var blocked = f.Interact(Table);
            Assert.That(blocked.Accepted, Is.False);
            Assert.That(blocked.Reason, Is.EqualTo(RejectReason.WrongMode), "A2-11");
            Assert.That(f.Snapshot.Mode, Is.EqualTo(WorkshopMode.Walking));
            Do(f, GameKeys.Enter, null, "acknowledge settlement");
            Assert.That(f.Overlay, Is.EqualTo(Overlay.None));
            Assert.That(f.PendingSettlement, Is.Null);

            // ---------- days 2..7: one house per day (CONTENT §5: house ★3 normal 12, popular 18; −2 paper)
            int money = 43;
            var popular = new[] { null, "plane", "house", "tulip", "house", "dog", "tulip", "cicada" };
            for (int day = 2; day <= 7; day++)
            {
                Assert.That(f.Snapshot.Day, Is.EqualTo(day));
                Do(f, GameKeys.Interact, Table, "sit day " + day);
                Assert.That(f.TakeSheetHintVisible, Is.False);
                MakeByKeys(f, "house", "집 ★3 (일치도 100%)");
                money -= 2;
                Do(f, GameKeys.StandUp);
                Do(f, GameKeys.Interact, Bed);
                Do(f, GameKeys.Enter);
                int price = popular[day] == "house" ? 18 : 12;
                money += price;
                Assert.That(f.Overlay, Is.EqualTo(Overlay.Settlement), "day " + day);
                var lines = GameTexts.SettlementLines(f.PendingSettlement);
                Assert.That(lines[0], Is.EqualTo(day + "일차 정산"));
                Assert.That(lines[1], Is.EqualTo("집 ★3 " + price + "원" + (price == 18 ? " (인기)" : "")));
                Assert.That(lines[2], Is.EqualTo("합계 " + price + "원"));
                Assert.That(lines[3], Is.EqualTo("정산 후 돈 " + money + "원"));
                Assert.That(f.Snapshot.Money, Is.EqualTo(money));
                if (day < 7)
                {
                    Assert.That(lines[4], Is.EqualTo("내일의 인기: " + GameTexts.ModelName(popular[day + 1])));
                    Do(f, GameKeys.Enter);
                    Assert.That(f.Overlay, Is.EqualTo(Overlay.None));
                }
                else
                {
                    Assert.That(lines[4], Is.EqualTo("7일이 모두 지났어요"));
                    Assert.That(f.Snapshot.IsEnded, Is.True);
                    Assert.That(f.Snapshot.Mode, Is.EqualTo(WorkshopMode.Ended));
                    // Enter on the last settlement acknowledges (Settlement row wins over Mode Ended) → Result, not a new run
                    Do(f, GameKeys.Enter);
                    Assert.That(f.Overlay, Is.EqualTo(Overlay.Result));
                    Assert.That(f.Snapshot.IsEnded, Is.True, "still the ended run");
                }
            }
            Assert.That(money, Is.EqualTo(115));

            // ---------- result screen
            var result = GameTexts.ResultLines(f.Snapshot);
            Assert.That(result.Take(3), Is.EqualTo(new[] { "동네 종이접기 공방", "소박하지만 단골이 생긴 동네 공방. 다음 주도 문을 연다.", "최종 돈 115원" }));
            Assert.That(result[3], Is.EqualTo("종이비행기: ★3 1 · ★2 0 · ★1 0"));
            Assert.That(result[4], Is.EqualTo("집: ★3 7 · ★2 0 · ★1 0"));
            Assert.That(result.Skip(8), Is.EqualTo(new[] { "실패 0", "총 판매액 109원", "총 종이값 14원", "총 책값 0원" }));
            foreach (var k in new[] { GameKeys.Escape, GameKeys.Interact, GameKeys.TakeSheet, GameKeys.StandUp })
                Nothing(f, k, Table, "result " + k);
            Assert.That(f.TakeSheet().Reason, Is.EqualTo(RejectReason.WrongMode), "overlay blocks commands");

            int resetSerial = f.LastSheetChange.Serial;
            Do(f, GameKeys.Enter, null, "new run from result");
            AssertNewRunState(f);
            Assert.That(f.LastSheetChange.Kind, Is.EqualTo(SheetChangeKind.Reset));
            Assert.That(f.LastSheetChange.Serial, Is.EqualTo(resetSerial + 1));
            Assert.That(f.LastSheetChange.Previous, Is.Null);

            // the new run is fully playable: tutorial again
            Do(f, GameKeys.Interact, Table);
            Assert.That(f.TakeSheetHintVisible, Is.True);
            Do(f, GameKeys.TakeSheet);
            Assert.That(f.Snapshot.Sheet.IsTutorial, Is.True);
            Assert.That(f.Snapshot.Money, Is.EqualTo(20));
        }

        // ================================================================================== pause / new run

        [Test]
        public void PauseMenu_ContinueAndConfirmNewRun_FromMidRun()
        {
            var f = new ScreenFlow();
            Do(f, GameKeys.Interact, Table);
            Do(f, GameKeys.TakeSheet);
            Apply(f, V2SpecBook.Book("house")[0], "h1");
            var sheet = f.Snapshot.Sheet.State;
            f.OpenPause();
            Assert.That(f.Overlay, Is.EqualTo(Overlay.None), "OpenPause only while walking");
            Do(f, GameKeys.StandUp);

            Do(f, GameKeys.Escape);
            Assert.That(f.Overlay, Is.EqualTo(Overlay.PauseMenu));
            Nothing(f, GameKeys.Interact, Table, "pause E");
            Nothing(f, GameKeys.TakeSheet, Table, "pause Space");
            Assert.That(f.Interact(Table).Reason, Is.EqualTo(RejectReason.WrongMode));
            Assert.That(f.Snapshot.Mode, Is.EqualTo(WorkshopMode.Walking));
            Do(f, GameKeys.Escape, null, "continue");
            Assert.That(f.Overlay, Is.EqualTo(Overlay.None));

            Do(f, GameKeys.Escape);
            Do(f, GameKeys.Enter, null, "new run → confirm");
            Assert.That(f.Overlay, Is.EqualTo(Overlay.ConfirmNewRun));
            Nothing(f, GameKeys.Interact, Bed, "confirm E");
            Do(f, GameKeys.Escape, null, "cancel confirm");
            Assert.That(f.Overlay, Is.EqualTo(Overlay.None));
            Assert.That(f.Snapshot.Sheet.State, Is.SameAs(sheet), "cancel keeps the run");

            Do(f, GameKeys.Escape);
            Do(f, GameKeys.Enter);
            Do(f, GameKeys.Enter, null, "confirm new run");
            AssertNewRunState(f);
            Assert.That(f.LastSheetChange.Kind, Is.EqualTo(SheetChangeKind.Reset));
            Assert.That(f.LastSheetChange.Previous, Is.SameAs(sheet), "Reset.Previous = the discarded sheet");
        }

        [Test]
        public void OverlayMethods_OutsideTheirOverlay_DoNothing()
        {
            var f = new ScreenFlow();
            var snap = f.Snapshot;
            f.ClosePause();
            f.RequestNewRun();
            f.ConfirmNewRun();
            f.AcknowledgeSettlement();
            Assert.That(f.Overlay, Is.EqualTo(Overlay.None));
            Assert.That(f.Snapshot, Is.SameAs(snap), "ConfirmNewRun without confirm overlay keeps the run");
            Assert.That(f.LastSheetChange.Serial, Is.EqualTo(0));

            f.OpenPause();
            f.AcknowledgeSettlement();
            Assert.That(f.Overlay, Is.EqualTo(Overlay.PauseMenu));
            f.ConfirmNewRun();
            Assert.That(f.Snapshot, Is.SameAs(snap), "ConfirmNewRun from PauseMenu (no confirmation) does nothing");
            Assert.That(f.Overlay, Is.EqualTo(Overlay.PauseMenu));
            f.OpenPause();
            Assert.That(f.Overlay, Is.EqualTo(Overlay.PauseMenu));
            f.RequestNewRun();
            f.RequestNewRun();
            Assert.That(f.Overlay, Is.EqualTo(Overlay.ConfirmNewRun));
            f.OpenPause();
            Assert.That(f.Overlay, Is.EqualTo(Overlay.ConfirmNewRun), "OpenPause only with no overlay");
            f.ClosePause();
            Assert.That(f.Overlay, Is.EqualTo(Overlay.None));
        }

        [Test]
        public void EveryCommand_IsBlockedByEveryOverlay()
        {
            var commands = new List<Func<ScreenFlow, CommandResult>>
            {
                x => x.Interact(Table), x => x.StandUp(), x => x.ToggleBook(), x => x.OpenBook(), x => x.CloseBook(),
                x => x.TurnPage(4), x => x.TurnPageBy(2), x => x.BuyPage("tulip"), x => x.CloseCounter(), x => x.Sleep(true),
                x => x.Sleep(false), x => x.TakeSheet(), x => x.Fold(V(-1, 1), V(-1, -1), false, 0), x => x.Crease(V(-1, 1), V(1, 1), 0),
                x => x.Flip(), x => x.Undo(), x => x.Unfold(), x => x.Finish(), x => x.Discard(), x => x.SkipGuide(),
            };
            foreach (var overlay in new[] { Overlay.PauseMenu, Overlay.ConfirmNewRun, Overlay.Settlement, Overlay.Result })
            {
                var f = new ScreenFlow();
                switch (overlay)
                {
                    case Overlay.PauseMenu: f.OpenPause(); break;
                    case Overlay.ConfirmNewRun: f.OpenPause(); f.RequestNewRun(); break;
                    case Overlay.Settlement: f.Interact(Bed); f.Sleep(true); break;
                    case Overlay.Result:
                        for (int d = 0; d < 7; d++) { f.Interact(Bed); f.Sleep(true); f.AcknowledgeSettlement(); }
                        break;
                }
                Assert.That(f.Overlay, Is.EqualTo(overlay));
                foreach (var cmd in commands)
                {
                    var snap = f.Snapshot;
                    int msg = f.Message.Serial, chg = f.LastSheetChange.Serial;
                    var r = cmd(f);
                    Assert.That(r.Accepted, Is.False, overlay.ToString());
                    Assert.That(r.Reason, Is.EqualTo(RejectReason.WrongMode), overlay.ToString());
                    Assert.That(f.Snapshot, Is.SameAs(snap));
                    Assert.That(f.Overlay, Is.EqualTo(overlay));
                    Assert.That(f.Message.Serial, Is.EqualTo(msg));
                    Assert.That(f.LastSheetChange.Serial, Is.EqualTo(chg));
                }
            }
        }

        // ================================================================================== key table and priority

        [Test]
        public void KeyPriority_Combinations_PerC171()
        {
            // Walking
            var f = new ScreenFlow();
            Do(f, GameKeys.Escape | GameKeys.Interact, Table, "Esc beats E");
            Assert.That(f.Overlay, Is.EqualTo(Overlay.PauseMenu));
            f.ClosePause();
            Nothing(f, GameKeys.Enter | GameKeys.Interact, Table, "walking Enter+E: Enter is highest and means nothing");
            Assert.That(f.Snapshot.Mode, Is.EqualTo(WorkshopMode.Walking));
            Nothing(f, GameKeys.None, Table, "no key");
            Nothing(f, (GameKeys)(1 << 20), Table, "unknown bit");
            Do(f, (GameKeys)(-1), Table, "all bits → Escape");
            Assert.That(f.Overlay, Is.EqualTo(Overlay.PauseMenu));
            f.ClosePause();
            Do(f, GameKeys.Interact | GameKeys.TakeSheet | GameKeys.PageRight, Table);
            Assert.That(f.Snapshot.Mode, Is.EqualTo(WorkshopMode.Seated));

            // Seated, tutorial sheet with one fold
            Do(f, GameKeys.TakeSheet);
            Apply(f, V2SpecBook.Book("plane")[0], "p1");
            Nothing(f, GameKeys.Escape | GameKeys.Enter, null, "seated Esc+Enter: Esc means nothing");
            Nothing(f, GameKeys.Interact | GameKeys.Undo, Table, "seated E+Z: E means nothing");
            Assert.That(f.Snapshot.Sheet.ActionCount, Is.EqualTo(1));
            Do(f, GameKeys.Undo | GameKeys.Unfold | GameKeys.Flip);
            Assert.That(f.LastSheetChange.Kind, Is.EqualTo(SheetChangeKind.Undone), "Z beats R and F");
            Apply(f, V2SpecBook.Book("plane")[0], "p1 again");
            Apply(f, V2SpecBook.Book("plane")[1], "p2");
            Do(f, GameKeys.Unfold | GameKeys.Flip | GameKeys.Book);
            Assert.That(f.LastSheetChange.Kind, Is.EqualTo(SheetChangeKind.Unfolded), "R beats F and B");
            Assert.That(f.Snapshot.Sheet.ActionCount, Is.EqualTo(0));
            var before = f.Snapshot.Sheet.State;
            Do(f, GameKeys.Flip | GameKeys.Book | GameKeys.StandUp);
            Assert.That(f.LastSheetChange.Kind, Is.EqualTo(SheetChangeKind.Flipped), "F beats B and Q");
            Assert.That(f.LastSheetChange.Previous, Is.SameAs(before));
            Assert.That(f.LastSheetChange.Plan, Is.Null, "Flip carries no plan");
            Assert.That(f.Snapshot.BookOpen, Is.False);
            Do(f, GameKeys.Book | GameKeys.SkipGuide);
            Assert.That(f.Snapshot.BookOpen, Is.True, "B beats G");
            Assert.That(f.Snapshot.Sheet.GuideVisible, Is.True);
            Do(f, GameKeys.SkipGuide | GameKeys.StandUp | GameKeys.PageRight);
            Assert.That(f.Snapshot.Sheet.GuideVisible, Is.False, "G beats Q and →");
            Assert.That(f.Snapshot.Mode, Is.EqualTo(WorkshopMode.Seated));
            Do(f, GameKeys.PageLeft | GameKeys.PageRight);
            Assert.That(f.Snapshot.BookPage, Is.EqualTo(0), "← beats →");
            Do(f, GameKeys.PageRight);
            Assert.That(f.Snapshot.BookPage, Is.EqualTo(2));
            Do(f, GameKeys.StandUp | GameKeys.PageRight);
            Assert.That(f.Snapshot.Mode, Is.EqualTo(WorkshopMode.Walking), "Q beats →");
            Assert.That(f.Snapshot.BookPage, Is.EqualTo(2));

            // sheet stays on the table; back to seated: Discard beats Undo; Enter beats Space
            Do(f, GameKeys.Interact, Table);
            Assert.That(f.Snapshot.BookOpen, Is.False, "sitting again closes the book (F-003 AC-04)");
            Do(f, GameKeys.Discard | GameKeys.Undo);
            Assert.That(f.LastSheetChange.Kind, Is.EqualTo(SheetChangeKind.Removed), "X beats Z");
            Assert.That(f.Snapshot.FailureCount, Is.EqualTo(1));
            Assert.That(f.PostTutorialHintVisible, Is.True, "tutorial sheet discarded");
            Do(f, GameKeys.TakeSheet | GameKeys.Discard);
            Assert.That(f.LastSheetChange.Kind, Is.EqualTo(SheetChangeKind.Taken), "Space beats X");
            Assert.That(f.PostTutorialHintVisible, Is.False, "cleared by TakeSheet");
            FoldModel(f, "house");
            Do(f, GameKeys.Enter | GameKeys.TakeSheet);
            Assert.That(f.Message.Text, Is.EqualTo("집 ★3 (일치도 100%)"), "Enter beats Space");
            Assert.That(f.Snapshot.Sheet, Is.Null, "Space not processed in the same frame");

            // Bookshelf
            Do(f, GameKeys.StandUp);
            Do(f, GameKeys.Interact, Bookshelf);
            Assert.That(f.Snapshot.BookPage, Is.EqualTo(2), "page kept while closed");
            Do(f, GameKeys.PageLeft | GameKeys.PageRight);
            Assert.That(f.Snapshot.BookPage, Is.EqualTo(0));
            Nothing(f, GameKeys.Enter | GameKeys.PageRight, null, "bookshelf Enter+→: Enter means nothing");
            Do(f, GameKeys.Book | GameKeys.PageRight);
            Assert.That(f.Snapshot.Mode, Is.EqualTo(WorkshopMode.Walking), "B closes the bookshelf");
            Do(f, GameKeys.Interact, Bookshelf);
            Do(f, GameKeys.Interact | GameKeys.PageRight, null);
            Assert.That(f.Snapshot.Mode, Is.EqualTo(WorkshopMode.Walking), "E closes the bookshelf");

        }

        [Test]
        public void KeyPriority_CounterAndSleepConfirm()
        {
            var f = new ScreenFlow();
            Do(f, GameKeys.Interact, Counter);
            Do(f, GameKeys.Escape | GameKeys.Enter);
            Assert.That(f.Snapshot.Mode, Is.EqualTo(WorkshopMode.Walking), "counter: Esc (highest) closes");
            Do(f, GameKeys.Interact, Counter);
            Nothing(f, GameKeys.Enter | GameKeys.Book, null, "counter Enter means nothing");
            Do(f, GameKeys.Interact | GameKeys.Book);
            Assert.That(f.Snapshot.Mode, Is.EqualTo(WorkshopMode.Walking));

            Do(f, GameKeys.Interact, Bed);
            Do(f, GameKeys.Escape | GameKeys.Enter);
            Assert.That(f.Snapshot.Mode, Is.EqualTo(WorkshopMode.Walking), "sleep confirm: Esc beats Enter → no");
            Assert.That(f.Overlay, Is.EqualTo(Overlay.None));
            Do(f, GameKeys.Interact, Bed);
            Do(f, GameKeys.Enter | GameKeys.Interact);
            Assert.That(f.Overlay, Is.EqualTo(Overlay.Settlement), "Enter beats E → yes");
            Assert.That(f.Snapshot.Day, Is.EqualTo(2));
        }

        [Test]
        public void KeyTable_CellsSampledFromF003()
        {
            // Bed with a sheet left on the table → SheetOnTable toast via E (F-001 AC-04)
            var f = new ScreenFlow();
            Do(f, GameKeys.Interact, Table);
            Do(f, GameKeys.TakeSheet);
            Do(f, GameKeys.StandUp);
            Do(f, GameKeys.Interact, Bed);
            Assert.That(f.Snapshot.Mode, Is.EqualTo(WorkshopMode.Walking));
            Assert.That(f.Message.Kind, Is.EqualTo(MessageKind.Reject));
            Assert.That(f.Message.Text, Is.EqualTo("테이블 위 종이를 먼저 완성하거나 버리세요"));
            // unknown candidate → UnknownTarget, silent, still true
            int serial = f.Message.Serial;
            Do(f, GameKeys.Interact, "door");
            Assert.That(f.Message.Serial, Is.EqualTo(serial));

            // bookshelf: → to the end clamps at 42; ← from 0 stays 0
            Do(f, GameKeys.Interact, Bookshelf);
            for (int i = 0; i < 30; i++) Do(f, GameKeys.PageRight);
            Assert.That(f.Snapshot.BookPage, Is.EqualTo(42));
            Assert.That(f.TurnPageBy(int.MaxValue).Accepted, Is.True);
            Assert.That(f.Snapshot.BookPage, Is.EqualTo(42));
            Assert.That(f.TurnPageBy(int.MinValue).Accepted, Is.True);
            Assert.That(f.Snapshot.BookPage, Is.EqualTo(0));
            // buying through the flow: AlreadyOwned / NotEnoughMoney toasts, UnknownModel silent
            Assert.That(f.BuyPage("plane").Reason, Is.EqualTo(RejectReason.AlreadyOwned));
            Assert.That(f.Message.Text, Is.EqualTo("이미 가진 페이지예요"));
            Assert.That(f.BuyPage("dog").Reason, Is.EqualTo(RejectReason.NotEnoughMoney));
            Assert.That(f.Message.Text, Is.EqualTo("돈이 모자라요"));
            serial = f.Message.Serial;
            Assert.That(f.BuyPage("unicorn").Reason, Is.EqualTo(RejectReason.UnknownModel));
            Assert.That(f.Message.Serial, Is.EqualTo(serial));
            Assert.That(f.BuyPage("tulip").Accepted, Is.True);
            Assert.That(f.Snapshot.Money, Is.EqualTo(20 - 20), "first sheet free, tulip 20");
            Do(f, GameKeys.Escape);

            // seated with the book open: Q stands up; book visibility rules
            Do(f, GameKeys.Interact, Table);
            Do(f, GameKeys.Book);
            Assert.That(f.Snapshot.BookVisible, Is.True);
            Do(f, GameKeys.Book);
            Assert.That(f.Snapshot.BookVisible, Is.False);
            Assert.That(f.Snapshot.Mode, Is.EqualTo(WorkshopMode.Seated), "B in seated closes the book only");
        }

        [Test]
        public void SeatedCommands_NoSheetsLeftAndNotEnoughMoney_Toasts()
        {
            var f = new ScreenFlow();
            Do(f, GameKeys.Interact, Table);
            for (int i = 0; i < 5; i++)
            {
                Do(f, GameKeys.TakeSheet);
                Do(f, GameKeys.Discard);
            }
            Assert.That(f.Snapshot.Money, Is.EqualTo(12));
            Do(f, GameKeys.TakeSheet);
            Assert.That(f.Message.Text, Is.EqualTo("오늘은 종이를 다 썼어요. 침대에서 쉬세요"));
            Do(f, GameKeys.StandUp);
            Do(f, GameKeys.Interact, Bed);
            Do(f, GameKeys.Enter);
            Do(f, GameKeys.Enter);
            Do(f, GameKeys.Interact, Table);
            for (int i = 0; i < 5; i++)
            {
                Do(f, GameKeys.TakeSheet);
                Do(f, GameKeys.Discard);
            }
            Do(f, GameKeys.StandUp);
            Do(f, GameKeys.Interact, Bed);
            Do(f, GameKeys.Enter);
            Assert.That(f.PendingSettlement.EndReason, Is.EqualTo(RunEndReason.None), "money exactly 2 is not bankrupt");
            Do(f, GameKeys.Enter);
            Do(f, GameKeys.Interact, Table);
            Do(f, GameKeys.TakeSheet);
            Do(f, GameKeys.Discard);
            Assert.That(f.Snapshot.Money, Is.EqualTo(0));
            Do(f, GameKeys.TakeSheet);
            Assert.That(f.Message.Text, Is.EqualTo("돈이 모자라요"));
            Do(f, GameKeys.StandUp);
            Do(f, GameKeys.Interact, Bed);
            Do(f, GameKeys.Enter);
            Assert.That(f.Overlay, Is.EqualTo(Overlay.Settlement));
            Assert.That(f.PendingSettlement.EndReason, Is.EqualTo(RunEndReason.Bankrupt));
            Assert.That(GameTexts.SettlementLines(f.PendingSettlement).Last(), Is.EqualTo("돈이 모자라 공방 문을 닫았어요"));
            Do(f, GameKeys.Enter);
            Assert.That(f.Overlay, Is.EqualTo(Overlay.Result));
            Assert.That(GameTexts.ResultLines(f.Snapshot)[0], Is.EqualTo("빈손"));
            Do(f, GameKeys.Enter);
            AssertNewRunState(f);
        }

        // ================================================================================== sheet changes

        [Test]
        public void SheetChanges_KindsPreviousAndSerials()
        {
            var f = new ScreenFlow();
            Do(f, GameKeys.Interact, Table);
            Do(f, GameKeys.TakeSheet);
            Assert.That(f.LastSheetChange.Serial, Is.EqualTo(1));
            var s0 = f.Snapshot.Sheet.State;
            Apply(f, V2SpecBook.Book("house")[0], "h1");
            var s1 = f.Snapshot.Sheet.State;
            Do(f, GameKeys.Undo);
            Assert.That(f.LastSheetChange.Kind, Is.EqualTo(SheetChangeKind.Undone));
            Assert.That(f.LastSheetChange.Previous, Is.SameAs(s1));
            Assert.That(f.LastSheetChange.Serial, Is.EqualTo(3));
            Assert.That(f.Snapshot.Sheet.FoldCount, Is.EqualTo(0));
            // a rejected sheet command does not create a change
            Do(f, GameKeys.Undo);
            Assert.That(f.LastSheetChange.Serial, Is.EqualTo(3));
            var bad = f.Fold(V(-1, 1), V(-1, 1), false, 0);
            Assert.That(bad.Reason, Is.EqualTo(RejectReason.InvalidFold));
            Assert.That(f.LastSheetChange.Serial, Is.EqualTo(3));
            Assert.That(f.Message.Text, Is.EqualTo(FoldTexts.ErrorMessage(bad.FoldError)).And.Not.Empty);
            Apply(f, V2SpecBook.Book("house")[0], "h1 again");
            Apply(f, V2SpecBook.Book("house")[1], "h2");
            var s2 = f.Snapshot.Sheet.State;
            Do(f, GameKeys.Unfold);
            Assert.That(f.LastSheetChange.Kind, Is.EqualTo(SheetChangeKind.Unfolded));
            Assert.That(f.LastSheetChange.Previous, Is.SameAs(s2));
            Assert.That(f.LastSheetChange.Plan, Is.Null);
            Assert.That(f.Snapshot.Sheet.CreaseCount, Is.EqualTo(0));
            Assert.That(s0, Is.Not.Null);
        }

        // ================================================================================== one-time hints lifecycle

        [Test]
        public void PostTutorialHint_PersistsAcrossNight_ClearedByBookshelf()
        {
            var f = new ScreenFlow();
            Do(f, GameKeys.Interact, Table);
            Do(f, GameKeys.TakeSheet);
            Do(f, GameKeys.Discard);
            Assert.That(f.PostTutorialHintVisible, Is.True);
            // rejected Interact(bookshelf) from seated (WrongMode) does not clear it
            Assert.That(f.Interact(Bookshelf).Reason, Is.EqualTo(RejectReason.WrongMode));
            Assert.That(f.PostTutorialHintVisible, Is.True);
            Do(f, GameKeys.StandUp);
            Do(f, GameKeys.Interact, Counter);
            Do(f, GameKeys.Escape);
            Do(f, GameKeys.Interact, Bed);
            Do(f, GameKeys.Enter);
            Assert.That(f.PostTutorialHintVisible, Is.True, "behind settlement");
            Do(f, GameKeys.Enter);
            Assert.That(f.Snapshot.Day, Is.EqualTo(2));
            Assert.That(f.PostTutorialHintVisible, Is.True, "until TakeSheet/OpenBook/Interact(bookshelf)");
            Do(f, GameKeys.Interact, Bookshelf);
            Assert.That(f.PostTutorialHintVisible, Is.False, "cleared by Interact(bookshelf)");
            Do(f, GameKeys.Escape);
            Do(f, GameKeys.Interact, Table);
            Do(f, GameKeys.TakeSheet);
            Do(f, GameKeys.Discard);
            Assert.That(f.PostTutorialHintVisible, Is.False, "only once per run");
        }

        [Test]
        public void PostTutorialHint_AfterSkippedGuideFinish_AndClearedByNewRun()
        {
            var f = new ScreenFlow();
            Do(f, GameKeys.Interact, Table);
            Do(f, GameKeys.TakeSheet);
            Do(f, GameKeys.SkipGuide);
            Assert.That(f.Snapshot.Sheet.GuideVisible, Is.False);
            Do(f, GameKeys.Enter);
            Assert.That(f.PostTutorialHintVisible, Is.False, "rejected Finish (NoFolds) does not end the tutorial");
            Apply(f, V2SpecBook.Book("house")[0], "one fold");
            Do(f, GameKeys.Enter);
            Assert.That(f.Message.Kind, Is.EqualTo(MessageKind.Recognition));
            Assert.That(f.Message.Text, Does.StartWith("구겨진 종이 (가장 비슷한 작품: 집 "));
            Assert.That(f.PostTutorialHintVisible, Is.True, "tutorial sheet finished (even skipped / crumpled)");
            Do(f, GameKeys.StandUp);
            Do(f, GameKeys.Escape);
            Do(f, GameKeys.Enter);
            Do(f, GameKeys.Enter);
            AssertNewRunState(f);
        }

        [Test]
        public void TakeSheetAndWalkHints_Lifecycle()
        {
            var f = new ScreenFlow();
            Assert.That(f.WalkHintVisible, Is.True);
            Do(f, GameKeys.Interact, Bookshelf);
            Assert.That(f.WalkHintVisible, Is.False, "not walking");
            Do(f, GameKeys.Escape);
            Assert.That(f.WalkHintVisible, Is.True, "still before the first sit");
            Do(f, GameKeys.Interact, Bed);
            Do(f, GameKeys.Enter);
            Do(f, GameKeys.Enter);
            Assert.That(f.Snapshot.Day, Is.EqualTo(2));
            Assert.That(f.WalkHintVisible, Is.True, "first sit not done yet on day 2");

            Do(f, GameKeys.Interact, Table);
            Assert.That(f.WalkHintVisible, Is.False);
            Assert.That(f.TakeSheetHintVisible, Is.True, "first sheet on day 2 is still the free tutorial sheet");
            Do(f, GameKeys.Book);
            Assert.That(f.TakeSheetHintVisible, Is.True, "book open, still no sheet");
            Do(f, GameKeys.StandUp);
            Assert.That(f.TakeSheetHintVisible, Is.False, "walking");
            Assert.That(f.WalkHintVisible, Is.False, "already sat");
            Do(f, GameKeys.Interact, Table);
            Assert.That(f.TakeSheetHintVisible, Is.True);
            Do(f, GameKeys.TakeSheet);
            Assert.That(f.TakeSheetHintVisible, Is.False);
            Assert.That(f.Snapshot.Sheet.IsTutorial, Is.True);
            Assert.That(f.Snapshot.Money, Is.EqualTo(20));
            Do(f, GameKeys.Discard);
            Assert.That(f.TakeSheetHintVisible, Is.False, "first sheet already used");
        }
    }
}
