using System;
using Origami.Core.Folding;
using Origami.Core.Game;
using Origami.Core.Geometry;
using Origami.Core.Texts;

namespace Origami.Core.Screens
{
    /// <summary>
    /// Screen flow of the workshop (C-171): owns the only <see cref="OrigamiGame"/>, the overlays (pause, new-run confirm, settlement,
    /// result), the key → command table (one key per frame, F-003), the latest toast message, the latest sheet change and the one-time
    /// hints of F-013. Keys and on-screen buttons call the same methods. Deterministic, no clock (R-001).
    /// </summary>
    /// <remarks>
    /// Decisions where C-171 is open (ORIGAMI-011):
    /// <list type="bullet">
    /// <item>A game command while an overlay is open returns <c>WrongMode</c> without calling Core (A2-11); that rejection has no text,
    /// so the message does not change.</item>
    /// <item><see cref="ConfirmNewRun"/> also acts when the run has ended without an overlay (the "Mode Ended" row of the key table).</item>
    /// <item><see cref="ToggleBook"/> = <see cref="CloseBook"/> while the book is visible (seated + open, or bookshelf), else <see cref="OpenBook"/>.</item>
    /// <item><see cref="TurnPageBy"/> = <c>TurnPage(BookPage + delta)</c>, the sum clamped to the int range (no overflow).</item>
    /// <item>A new run keeps the last message (its serial does not change) and clears the post-tutorial hint.</item>
    /// </list>
    /// </remarks>
    public sealed class ScreenFlow
    {
        private readonly OrigamiGame _game = new OrigamiGame();
        private int _messageSerial;
        private int _changeSerial;
        private bool _postTutorialHint;

        /// <summary>New run (= <c>NewRun</c> state), no overlay.</summary>
        public ScreenFlow()
        {
        }

        /// <summary>The game's immutable snapshot (new object only when a command is accepted).</summary>
        public GameSnapshot Snapshot => _game.Snapshot;

        public Overlay Overlay { get; private set; }

        /// <summary>The settlement on screen while <see cref="Overlay"/> is Settlement; otherwise null.</summary>
        public SettlementResult PendingSettlement { get; private set; }

        /// <summary>Latest message; a larger Serial means a new message.</summary>
        public FlowMessage Message { get; private set; }

        /// <summary>Latest change of the sheet on the table; a larger Serial means a new change.</summary>
        public SheetChange LastSheetChange { get; private set; }

        /// <summary>F-013 walk hint: walking and no accepted <c>Interact(table)</c> in this run yet.</summary>
        public bool WalkHintVisible => Snapshot.Mode == WorkshopMode.Walking && !Snapshot.HasSat;

        /// <summary>F-013 "Space: 종이 꺼내기": seated, no sheet, the run's first sheet not taken yet.</summary>
        public bool TakeSheetHintVisible => Snapshot.Mode == WorkshopMode.Seated && Snapshot.Sheet == null && Snapshot.FreeSheetAvailable;

        /// <summary>
        /// F-013 one-time hint: from the accepted Finish/Discard of the tutorial sheet until the next accepted TakeSheet, OpenBook
        /// (also through ToggleBook) or Interact(bookshelf), or a new run.
        /// </summary>
        public bool PostTutorialHintVisible => _postTutorialHint;

        // =====================================================================================================
        // keys (C-171 table)
        // =====================================================================================================

        private static readonly GameKeys[] Priority =
        {
            GameKeys.Escape, GameKeys.Enter, GameKeys.Interact, GameKeys.TakeSheet, GameKeys.Discard, GameKeys.Undo, GameKeys.Unfold,
            GameKeys.Flip, GameKeys.Book, GameKeys.SkipGuide, GameKeys.StandUp, GameKeys.PageLeft, GameKeys.PageRight,
        };

        /// <summary>
        /// Handles the highest-priority pressed key only (C-171 order). Returns true when the key meant something in the current
        /// situation (a rejected command also counts); false when no key is pressed or that key has no meaning here.
        /// </summary>
        /// <param name="keys">Keys pressed this frame.</param>
        /// <param name="interactCandidate">Nearest interaction target while walking (R-004), null when none.</param>
        public bool ProcessKeys(GameKeys keys, string interactCandidate)
        {
            var key = GameKeys.None;
            foreach (var k in Priority)
            {
                if ((keys & k) != 0)
                {
                    key = k;
                    break;
                }
            }
            if (key == GameKeys.None) return false;

            switch (Overlay)
            {
                case Overlay.Settlement:
                    if (key != GameKeys.Enter) return false;
                    AcknowledgeSettlement();
                    return true;
                case Overlay.Result:
                    if (key != GameKeys.Enter) return false;
                    ConfirmNewRun();
                    return true;
                case Overlay.PauseMenu:
                    if (key == GameKeys.Escape) ClosePause();
                    else if (key == GameKeys.Enter) RequestNewRun();
                    else return false;
                    return true;
                case Overlay.ConfirmNewRun:
                    if (key == GameKeys.Escape) ClosePause();
                    else if (key == GameKeys.Enter) ConfirmNewRun();
                    else return false;
                    return true;
            }

            var s = Snapshot;
            switch (s.Mode)
            {
                case WorkshopMode.Ended:
                    if (key != GameKeys.Enter) return false;
                    ConfirmNewRun();
                    return true;
                case WorkshopMode.Walking:
                    if (key == GameKeys.Escape)
                    {
                        OpenPause();
                        return true;
                    }
                    if (key == GameKeys.Interact && interactCandidate != null)
                    {
                        Interact(interactCandidate);
                        return true;
                    }
                    return false;
                case WorkshopMode.Seated:
                    switch (key)
                    {
                        case GameKeys.Enter: Finish(); return true;
                        case GameKeys.TakeSheet: TakeSheet(); return true;
                        case GameKeys.Discard: Discard(); return true;
                        case GameKeys.Undo: Undo(); return true;
                        case GameKeys.Unfold: Unfold(); return true;
                        case GameKeys.Flip: Flip(); return true;
                        case GameKeys.Book: ToggleBook(); return true;
                        case GameKeys.SkipGuide: SkipGuide(); return true;
                        case GameKeys.StandUp: StandUp(); return true;
                        case GameKeys.PageLeft:
                        case GameKeys.PageRight:
                            if (!s.BookOpen) return false;
                            TurnPageBy(key == GameKeys.PageLeft ? -2 : 2);
                            return true;
                        default: return false;
                    }
                case WorkshopMode.Bookshelf:
                    switch (key)
                    {
                        case GameKeys.Escape:
                        case GameKeys.Interact:
                        case GameKeys.Book:
                            CloseBook();
                            return true;
                        case GameKeys.PageLeft: TurnPageBy(-2); return true;
                        case GameKeys.PageRight: TurnPageBy(2); return true;
                        default: return false;
                    }
                case WorkshopMode.Counter:
                    if (key != GameKeys.Escape && key != GameKeys.Interact) return false;
                    CloseCounter();
                    return true;
                case WorkshopMode.SleepConfirm:
                    if (key == GameKeys.Escape) Sleep(false);
                    else if (key == GameKeys.Enter) Sleep(true);
                    else return false;
                    return true;
                default:
                    return false;
            }
        }

        // =====================================================================================================
        // game commands (Core results unchanged; WrongMode without Core while an overlay is open)
        // =====================================================================================================

        public CommandResult Interact(string target)
        {
            if (Overlay != Overlay.None) return Blocked();
            var r = Track(_game.Interact(target));
            if (r.Accepted && Snapshot.Mode == WorkshopMode.Bookshelf) _postTutorialHint = false;
            return r;
        }

        public CommandResult StandUp() => Overlay != Overlay.None ? Blocked() : Track(_game.StandUp());

        /// <summary>CloseBook while the book is visible, otherwise OpenBook.</summary>
        public CommandResult ToggleBook()
        {
            if (Overlay != Overlay.None) return Blocked();
            return Snapshot.BookVisible ? CloseBook() : OpenBook();
        }

        public CommandResult OpenBook()
        {
            if (Overlay != Overlay.None) return Blocked();
            var r = Track(_game.OpenBook());
            if (r.Accepted) _postTutorialHint = false;
            return r;
        }

        public CommandResult CloseBook() => Overlay != Overlay.None ? Blocked() : Track(_game.CloseBook());

        public CommandResult TurnPage(int page) => Overlay != Overlay.None ? Blocked() : Track(_game.TurnPage(page));

        /// <summary>TurnPage(current page + delta); ←/→ use ∓2.</summary>
        public CommandResult TurnPageBy(int delta)
        {
            if (Overlay != Overlay.None) return Blocked();
            long target = (long)Snapshot.BookPage + delta;
            int page = (int)Math.Max(int.MinValue, Math.Min(int.MaxValue, target));
            return Track(_game.TurnPage(page));
        }

        public CommandResult BuyPage(string modelId) => Overlay != Overlay.None ? Blocked() : Track(_game.BuyPage(modelId));

        public CommandResult CloseCounter() => Overlay != Overlay.None ? Blocked() : Track(_game.CloseCounter());

        /// <summary>Sleep(yes) accepted → Settlement overlay with <see cref="PendingSettlement"/>.</summary>
        public CommandResult Sleep(bool yes)
        {
            if (Overlay != Overlay.None) return Blocked();
            var r = Track(_game.Sleep(yes));
            if (r.Accepted && r.Settlement != null)
            {
                PendingSettlement = r.Settlement;
                Overlay = Overlay.Settlement;
            }
            return r;
        }

        public CommandResult TakeSheet()
        {
            if (Overlay != Overlay.None) return Blocked();
            var r = SheetCommand(_game.TakeSheet, SheetChangeKind.Taken);
            if (r.Accepted) _postTutorialHint = false;
            return r;
        }

        public CommandResult Fold(Vec2 grab, Vec2 drop, bool mountain, int layers) =>
            Overlay != Overlay.None ? Blocked() : SheetCommand(() => _game.Fold(grab, drop, mountain, layers), SheetChangeKind.Folded);

        public CommandResult Crease(Vec2 grab, Vec2 drop, int layers) =>
            Overlay != Overlay.None ? Blocked() : SheetCommand(() => _game.Crease(grab, drop, layers), SheetChangeKind.Creased);

        public CommandResult Flip() => Overlay != Overlay.None ? Blocked() : SheetCommand(_game.Flip, SheetChangeKind.Flipped);

        public CommandResult Undo() => Overlay != Overlay.None ? Blocked() : SheetCommand(_game.Undo, SheetChangeKind.Undone);

        public CommandResult Unfold() => Overlay != Overlay.None ? Blocked() : SheetCommand(_game.Unfold, SheetChangeKind.Unfolded);

        /// <summary>Accepted → Recognition message (<see cref="GameTexts.RecognitionLine"/>), sheet Removed.</summary>
        public CommandResult Finish() => Overlay != Overlay.None ? Blocked() : RemoveSheet(_game.Finish);

        public CommandResult Discard() => Overlay != Overlay.None ? Blocked() : RemoveSheet(_game.Discard);

        public CommandResult SkipGuide() => Overlay != Overlay.None ? Blocked() : Track(_game.SkipGuide());

        // =====================================================================================================
        // overlays
        // =====================================================================================================

        /// <summary>Walking ∧ no overlay → PauseMenu; otherwise nothing.</summary>
        public void OpenPause()
        {
            if (Overlay == Overlay.None && Snapshot.Mode == WorkshopMode.Walking) Overlay = Overlay.PauseMenu;
        }

        /// <summary>PauseMenu / ConfirmNewRun → None; otherwise nothing.</summary>
        public void ClosePause()
        {
            if (Overlay == Overlay.PauseMenu || Overlay == Overlay.ConfirmNewRun) Overlay = Overlay.None;
        }

        /// <summary>PauseMenu → ConfirmNewRun; otherwise nothing.</summary>
        public void RequestNewRun()
        {
            if (Overlay == Overlay.PauseMenu) Overlay = Overlay.ConfirmNewRun;
        }

        /// <summary>ConfirmNewRun or Result (or an ended run without overlay) → NewRun, no overlay, sheet change Reset; otherwise nothing.</summary>
        public void ConfirmNewRun()
        {
            bool allowed = Overlay == Overlay.ConfirmNewRun || Overlay == Overlay.Result || (Overlay == Overlay.None && Snapshot.IsEnded);
            if (!allowed) return;
            var previous = Snapshot.Sheet?.State;
            _game.NewRun();
            Overlay = Overlay.None;
            PendingSettlement = null;
            _postTutorialHint = false;
            LastSheetChange = new SheetChange(++_changeSerial, SheetChangeKind.Reset, null, previous);
        }

        /// <summary>Settlement → Result when the run ended, else None; otherwise nothing.</summary>
        public void AcknowledgeSettlement()
        {
            if (Overlay != Overlay.Settlement) return;
            PendingSettlement = null;
            Overlay = Snapshot.IsEnded ? Overlay.Result : Overlay.None;
        }

        // =====================================================================================================
        // internals
        // =====================================================================================================

        private static CommandResult Blocked() => CommandResult.Reject(RejectReason.WrongMode);

        /// <summary>Rejections with a text become a Reject message.</summary>
        private CommandResult Track(CommandResult r)
        {
            if (!r.Accepted)
            {
                string text = GameTexts.RejectMessage(r.Reason, r.FoldError);
                if (text.Length > 0) Message = new FlowMessage(++_messageSerial, MessageKind.Reject, text);
            }
            return r;
        }

        private CommandResult SheetCommand(Func<CommandResult> command, SheetChangeKind kind)
        {
            var previous = Snapshot.Sheet?.State;
            var r = Track(command());
            if (r.Accepted) LastSheetChange = new SheetChange(++_changeSerial, kind, r.Plan, previous);
            return r;
        }

        private CommandResult RemoveSheet(Func<CommandResult> command)
        {
            bool tutorial = Snapshot.Sheet != null && Snapshot.Sheet.IsTutorial;
            var r = SheetCommand(command, SheetChangeKind.Removed);
            if (!r.Accepted) return r;
            if (r.Recognition != null)
                Message = new FlowMessage(++_messageSerial, MessageKind.Recognition, GameTexts.RecognitionLine(r.Recognition));
            if (tutorial) _postTutorialHint = true;
            return r;
        }
    }
}
