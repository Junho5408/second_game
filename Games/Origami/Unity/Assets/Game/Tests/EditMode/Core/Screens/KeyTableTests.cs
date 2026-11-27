using System;
using System.Collections.Generic;
using NUnit.Framework;
using Origami.Core.Catalog;
using Origami.Core.Game;
using Origami.Core.Screens;
using static Origami.Tests.Screens.FlowScenarios;

namespace Origami.Tests.Screens
{
    // C-171 ProcessKeys — the F-003 mode × key table (Core part, owned by ORIGAMI-011). Every cell of the table is checked:
    // "—" cells return false and change nothing (snapshot reference, overlay, message and sheet-change serials).
    public class KeyTableTests
    {
        private static readonly GameKeys[] AllKeys =
        {
            GameKeys.Escape, GameKeys.Enter, GameKeys.Interact, GameKeys.TakeSheet, GameKeys.Discard, GameKeys.Undo, GameKeys.Unfold,
            GameKeys.Flip, GameKeys.Book, GameKeys.SkipGuide, GameKeys.StandUp, GameKeys.PageLeft, GameKeys.PageRight,
        };

        private sealed class Situation
        {
            public string Name;
            public Func<ScreenFlow> Make;
            public string Candidate;
            public Dictionary<GameKeys, Action<ScreenFlow>> Cells;
        }

        // ------------------------------------------------------------------------------------------------ situations

        private static ScreenFlow SettlementFlow()
        {
            var f = new ScreenFlow();
            Ok(f.Interact(InteractTargets.Bed));
            Ok(f.Sleep(true));
            Assert.AreEqual(Overlay.Settlement, f.Overlay);
            return f;
        }

        private static ScreenFlow ResultFlow()
        {
            var f = new ScreenFlow();
            for (int d = 0; d < 6; d++) Night(f);
            Ok(f.Interact(InteractTargets.Bed));
            Ok(f.Sleep(true));
            f.AcknowledgeSettlement();
            Assert.AreEqual(Overlay.Result, f.Overlay);
            Assert.AreEqual(WorkshopMode.Ended, f.Snapshot.Mode);
            return f;
        }

        private static ScreenFlow PauseFlow()
        {
            var f = new ScreenFlow();
            Night(f);   // day 2, so that a new run is visible
            f.OpenPause();
            Assert.AreEqual(Overlay.PauseMenu, f.Overlay);
            return f;
        }

        private static ScreenFlow ConfirmFlow()
        {
            var f = PauseFlow();
            f.RequestNewRun();
            Assert.AreEqual(Overlay.ConfirmNewRun, f.Overlay);
            return f;
        }

        /// <summary>Seated with the tutorial sheet after plane steps 1 (crease) and 2 (fold); book open on page 4 when requested.</summary>
        private static ScreenFlow SeatedFlow(bool bookOpen)
        {
            var f = new ScreenFlow();
            Sit(f);
            Take(f);
            var plane = ModelCatalog.Find(ModelIds.Plane).Steps;
            Apply(f, plane[0]);
            Apply(f, plane[1]);
            if (bookOpen)
            {
                Ok(f.OpenBook());
                Ok(f.TurnPage(4));
            }
            return f;
        }

        private static ScreenFlow ModeFlow(string target, int page = -1)
        {
            var f = new ScreenFlow();
            Ok(f.Interact(target));
            if (page >= 0) Ok(f.TurnPage(page));
            return f;
        }

        private static IEnumerable<Situation> Situations()
        {
            yield return new Situation
            {
                Name = "Overlay Settlement", Make = SettlementFlow,
                Cells = new Dictionary<GameKeys, Action<ScreenFlow>>
                {
                    [GameKeys.Enter] = f =>
                    {
                        Assert.AreEqual(Overlay.None, f.Overlay);
                        Assert.IsNull(f.PendingSettlement);
                        Assert.AreEqual(2, f.Snapshot.Day);
                    },
                },
            };
            yield return new Situation
            {
                Name = "Overlay Result", Make = ResultFlow,
                Cells = new Dictionary<GameKeys, Action<ScreenFlow>> { [GameKeys.Enter] = AssertNewRun },
            };
            yield return new Situation
            {
                Name = "Overlay PauseMenu", Make = PauseFlow,
                Cells = new Dictionary<GameKeys, Action<ScreenFlow>>
                {
                    [GameKeys.Escape] = f =>
                    {
                        Assert.AreEqual(Overlay.None, f.Overlay);
                        Assert.AreEqual(2, f.Snapshot.Day);
                    },
                    [GameKeys.Enter] = f => Assert.AreEqual(Overlay.ConfirmNewRun, f.Overlay),
                },
            };
            yield return new Situation
            {
                Name = "Overlay ConfirmNewRun", Make = ConfirmFlow,
                Cells = new Dictionary<GameKeys, Action<ScreenFlow>>
                {
                    [GameKeys.Escape] = f =>
                    {
                        Assert.AreEqual(Overlay.None, f.Overlay);
                        Assert.AreEqual(2, f.Snapshot.Day, "closing does not restart");
                    },
                    [GameKeys.Enter] = AssertNewRun,
                },
            };
            yield return new Situation
            {
                Name = "Walking (candidate table)", Make = () => new ScreenFlow(), Candidate = InteractTargets.Table,
                Cells = new Dictionary<GameKeys, Action<ScreenFlow>>
                {
                    [GameKeys.Escape] = f => Assert.AreEqual(Overlay.PauseMenu, f.Overlay),
                    [GameKeys.Interact] = f => Assert.AreEqual(WorkshopMode.Seated, f.Snapshot.Mode),
                },
            };
            yield return new Situation
            {
                Name = "Walking (no candidate)", Make = () => new ScreenFlow(), Candidate = null,
                Cells = new Dictionary<GameKeys, Action<ScreenFlow>>
                {
                    [GameKeys.Escape] = f => Assert.AreEqual(Overlay.PauseMenu, f.Overlay),
                },
            };
            yield return new Situation
            {
                Name = "Seated (book closed)", Make = () => SeatedFlow(false), Candidate = InteractTargets.Bookshelf,
                Cells = SeatedCells(false),
            };
            yield return new Situation
            {
                Name = "Seated (book open)", Make = () => SeatedFlow(true), Candidate = InteractTargets.Bookshelf,
                Cells = SeatedCells(true),
            };
            yield return new Situation
            {
                Name = "Bookshelf", Make = () => ModeFlow(InteractTargets.Bookshelf, 4), Candidate = InteractTargets.Bookshelf,
                Cells = new Dictionary<GameKeys, Action<ScreenFlow>>
                {
                    [GameKeys.Escape] = AssertWalking,
                    [GameKeys.Interact] = AssertWalking,
                    [GameKeys.Book] = AssertWalking,
                    [GameKeys.PageLeft] = f => Assert.AreEqual(2, f.Snapshot.BookPage),
                    [GameKeys.PageRight] = f => Assert.AreEqual(6, f.Snapshot.BookPage),
                },
            };
            yield return new Situation
            {
                Name = "Counter", Make = () => ModeFlow(InteractTargets.Counter), Candidate = InteractTargets.Counter,
                Cells = new Dictionary<GameKeys, Action<ScreenFlow>>
                {
                    [GameKeys.Escape] = AssertWalking,
                    [GameKeys.Interact] = AssertWalking,
                },
            };
            yield return new Situation
            {
                Name = "SleepConfirm", Make = () => ModeFlow(InteractTargets.Bed), Candidate = InteractTargets.Bed,
                Cells = new Dictionary<GameKeys, Action<ScreenFlow>>
                {
                    [GameKeys.Escape] = f =>
                    {
                        AssertWalking(f);
                        Assert.AreEqual(1, f.Snapshot.Day);
                        Assert.AreEqual(Overlay.None, f.Overlay);
                    },
                    [GameKeys.Enter] = f =>
                    {
                        Assert.AreEqual(Overlay.Settlement, f.Overlay);
                        Assert.IsNotNull(f.PendingSettlement);
                        Assert.AreEqual(2, f.Snapshot.Day);
                    },
                },
            };
        }

        private static Dictionary<GameKeys, Action<ScreenFlow>> SeatedCells(bool bookOpen)
        {
            var cells = new Dictionary<GameKeys, Action<ScreenFlow>>
            {
                [GameKeys.Enter] = f =>
                {
                    Assert.IsNull(f.Snapshot.Sheet, "Finish");
                    Assert.AreEqual(MessageKind.Recognition, f.Message.Kind);
                },
                [GameKeys.TakeSheet] = f =>
                {
                    // TakeSheet with a sheet on the table is rejected but still "did something" (true) and shows the reason
                    Assert.AreEqual(MessageKind.Reject, f.Message.Kind);
                    Assert.AreEqual("테이블 위 종이를 먼저 완성하거나 버리세요", f.Message.Text);
                },
                [GameKeys.Discard] = f =>
                {
                    Assert.IsNull(f.Snapshot.Sheet);
                    Assert.AreEqual(1, f.Snapshot.FailureCount);
                },
                [GameKeys.Undo] = f => Assert.AreEqual(1, f.Snapshot.Sheet.ActionCount),
                [GameKeys.Unfold] = f => Assert.AreEqual(0, f.Snapshot.Sheet.ActionCount),
                [GameKeys.Flip] = f => Assert.AreEqual(SheetChangeKind.Flipped, f.LastSheetChange.Kind),
                [GameKeys.Book] = f => Assert.AreEqual(!bookOpen, f.Snapshot.BookOpen),
                [GameKeys.SkipGuide] = f => Assert.IsFalse(f.Snapshot.Sheet.GuideVisible),
                [GameKeys.StandUp] = AssertWalking,
            };
            if (bookOpen)
            {
                cells[GameKeys.PageLeft] = f => Assert.AreEqual(2, f.Snapshot.BookPage);
                cells[GameKeys.PageRight] = f => Assert.AreEqual(6, f.Snapshot.BookPage);
            }
            return cells;
        }

        private static void AssertWalking(ScreenFlow f) => Assert.AreEqual(WorkshopMode.Walking, f.Snapshot.Mode);

        private static void AssertNewRun(ScreenFlow f)
        {
            Assert.AreEqual(Overlay.None, f.Overlay);
            Assert.AreEqual(1, f.Snapshot.Day);
            Assert.AreEqual(20, f.Snapshot.Money);
            Assert.AreEqual(WorkshopMode.Walking, f.Snapshot.Mode);
            Assert.IsFalse(f.Snapshot.IsEnded);
            Assert.AreEqual(SheetChangeKind.Reset, f.LastSheetChange.Kind);
        }

        // ------------------------------------------------------------------------------------------------ the table

        [Test]
        public void F003_KeyTable_EveryCell()
        {
            int cells = 0;
            foreach (var s in Situations())
            {
                foreach (var key in AllKeys)
                {
                    var f = s.Make();
                    string what = s.Name + " / " + key;
                    var snapshot = f.Snapshot;
                    var overlay = f.Overlay;
                    int message = f.Message.Serial, change = f.LastSheetChange.Serial;
                    bool did = f.ProcessKeys(key, s.Candidate);
                    if (s.Cells.TryGetValue(key, out var check))
                    {
                        Assert.IsTrue(did, what + " should act");
                        check(f);
                    }
                    else
                    {
                        Assert.IsFalse(did, what + " should be ignored");
                        Assert.AreSame(snapshot, f.Snapshot, what);
                        Assert.AreEqual(overlay, f.Overlay, what);
                        Assert.AreEqual(message, f.Message.Serial, what);
                        Assert.AreEqual(change, f.LastSheetChange.Serial, what);
                    }
                    cells++;
                }
            }
            Assert.AreEqual(11 * 13, cells);
        }

        [Test]
        public void F003_NoKey_DoesNothing()
        {
            var f = new ScreenFlow();
            Assert.IsFalse(f.ProcessKeys(GameKeys.None, InteractTargets.Table));
            Assert.AreEqual(WorkshopMode.Walking, f.Snapshot.Mode);
        }

        // ------------------------------------------------------------------------------------------------ one key per frame, priority

        [Test]
        public void F003_OnlyTheHighestPriorityKeyCounts_EvenWhenItIsMeaningless()
        {
            // Seated: Escape is the highest key and has no meaning → nothing happens, Space is not processed
            var f = SeatedFlow(false);
            Ok(f.Discard());
            var before = f.Snapshot;
            Assert.IsFalse(f.ProcessKeys(GameKeys.Escape | GameKeys.TakeSheet, null));
            Assert.AreSame(before, f.Snapshot);
            Assert.IsNull(f.Snapshot.Sheet);

            // Enter (Finish, rejected NoSheet — silent) beats TakeSheet: true, still no sheet
            Assert.IsTrue(f.ProcessKeys(GameKeys.Enter | GameKeys.TakeSheet, null));
            Assert.IsNull(f.Snapshot.Sheet);

            // Space alone takes a sheet
            Assert.IsTrue(f.ProcessKeys(GameKeys.TakeSheet, null));
            Assert.IsNotNull(f.Snapshot.Sheet);
        }

        [Test]
        public void F003_PriorityOrder_PairwiseInSeatedMode()
        {
            // For each pair (higher, lower) of keys meaningful while seated, pressing both acts like pressing the higher one alone.
            var seated = new[]
            {
                GameKeys.Enter, GameKeys.TakeSheet, GameKeys.Discard, GameKeys.Undo, GameKeys.Unfold, GameKeys.Flip, GameKeys.Book,
                GameKeys.SkipGuide, GameKeys.StandUp, GameKeys.PageLeft, GameKeys.PageRight,
            };
            for (int i = 0; i < seated.Length; i++)
            {
                for (int j = i + 1; j < seated.Length; j++)
                {
                    var both = SeatedFlow(true);
                    var alone = SeatedFlow(true);
                    Assert.IsTrue(both.ProcessKeys(seated[i] | seated[j], null));
                    Assert.IsTrue(alone.ProcessKeys(seated[i], null));
                    string what = seated[i] + " over " + seated[j];
                    Assert.AreEqual(alone.Snapshot.Mode, both.Snapshot.Mode, what);
                    Assert.AreEqual(alone.Snapshot.BookOpen, both.Snapshot.BookOpen, what);
                    Assert.AreEqual(alone.Snapshot.BookPage, both.Snapshot.BookPage, what);
                    Assert.AreEqual(alone.Snapshot.FailureCount, both.Snapshot.FailureCount, what);
                    Assert.AreEqual(alone.Snapshot.Sheet == null, both.Snapshot.Sheet == null, what);
                    Assert.AreEqual(alone.LastSheetChange.Kind, both.LastSheetChange.Kind, what);
                    Assert.AreEqual(alone.Message.Kind, both.Message.Kind, what);
                    if (alone.Snapshot.Sheet != null)
                    {
                        Assert.AreEqual(alone.Snapshot.Sheet.ActionCount, both.Snapshot.Sheet.ActionCount, what);
                        Assert.AreEqual(alone.Snapshot.Sheet.GuideVisible, both.Snapshot.Sheet.GuideVisible, what);
                    }
                }
            }
        }

        [Test]
        public void F003_EscapeBeatsEnterInPauseMenu_AndInteractBeatsPageKeysInBookshelf()
        {
            var f = PauseFlow();
            Assert.IsTrue(f.ProcessKeys(GameKeys.Escape | GameKeys.Enter, null));
            Assert.AreEqual(Overlay.None, f.Overlay);

            var b = ModeFlow(InteractTargets.Bookshelf, 4);
            Assert.IsTrue(b.ProcessKeys(GameKeys.Interact | GameKeys.PageRight, InteractTargets.Bookshelf));
            Assert.AreEqual(WorkshopMode.Walking, b.Snapshot.Mode);
            Assert.AreEqual(4, b.Snapshot.BookPage);
        }

        [Test]
        public void F003_AllKeysAtOnceInWalking_OpensPause()
        {
            var f = new ScreenFlow();
            var all = GameKeys.None;
            foreach (var k in AllKeys) all |= k;
            Assert.IsTrue(f.ProcessKeys(all, InteractTargets.Table));
            Assert.AreEqual(Overlay.PauseMenu, f.Overlay);
            Assert.AreEqual(WorkshopMode.Walking, f.Snapshot.Mode);
        }

        [Test]
        public void F003_WalkingInteractPassesTheCandidate_UnknownIsARejectedCommand()
        {
            var f = new ScreenFlow();
            var before = f.Snapshot;
            Assert.IsTrue(f.ProcessKeys(GameKeys.Interact, "trash"), "rejected commands also count as acting");
            Assert.AreSame(before, f.Snapshot);
            Assert.AreEqual(0, f.Message.Serial, "UnknownTarget has no text");

            Assert.IsTrue(f.ProcessKeys(GameKeys.Interact, InteractTargets.Counter));
            Assert.AreEqual(WorkshopMode.Counter, f.Snapshot.Mode);
        }

        [Test]
        public void F003_AC01_ModeRoundTripsThroughKeys()
        {
            var f = new ScreenFlow();
            Assert.IsTrue(f.ProcessKeys(GameKeys.Interact, InteractTargets.Table));
            Assert.AreEqual(WorkshopMode.Seated, f.Snapshot.Mode);
            Assert.IsFalse(f.Snapshot.BookOpen);
            Assert.IsTrue(f.ProcessKeys(GameKeys.StandUp, null));
            Assert.AreEqual(WorkshopMode.Walking, f.Snapshot.Mode);
            Assert.IsTrue(f.ProcessKeys(GameKeys.Interact, InteractTargets.Bookshelf));
            Assert.IsTrue(f.ProcessKeys(GameKeys.Escape, null));
            Assert.AreEqual(WorkshopMode.Walking, f.Snapshot.Mode);
            Assert.IsTrue(f.ProcessKeys(GameKeys.Interact, InteractTargets.Counter));
            Assert.IsTrue(f.ProcessKeys(GameKeys.Escape, null));
            Assert.AreEqual(WorkshopMode.Walking, f.Snapshot.Mode);
            Assert.IsTrue(f.ProcessKeys(GameKeys.Interact, InteractTargets.Bed));
            Assert.IsTrue(f.ProcessKeys(GameKeys.Escape, null));
            Assert.AreEqual(WorkshopMode.Walking, f.Snapshot.Mode);
            Assert.AreEqual(1, f.Snapshot.Day);
        }

        [Test]
        public void F007_R010_PageKeysAtTheBookEdges()
        {
            var f = ModeFlow(InteractTargets.Bookshelf);
            Assert.IsTrue(f.ProcessKeys(GameKeys.PageLeft, null));
            Assert.AreEqual(0, f.Snapshot.BookPage, "0 − 2 → 0");
            Ok(f.TurnPage(42));
            Assert.IsTrue(f.ProcessKeys(GameKeys.PageRight, null));
            Assert.AreEqual(42, f.Snapshot.BookPage, "42 + 2 → 42");
        }
    }
}
