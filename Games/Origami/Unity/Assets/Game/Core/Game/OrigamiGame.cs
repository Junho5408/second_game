using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Origami.Core.Catalog;
using Origami.Core.Economy;
using Origami.Core.Folding;
using Origami.Core.Geometry;
using Origami.Core.Recognition;
using Origami.Core.Tutorial;

namespace Origami.Core.Game
{
    /// <summary>
    /// Workshop rules state (C-150): modes (R-003), every command of GAME_RULES "명령" with the rejection order of R-019,
    /// sheets, tutorial guide, book, buying, finishing, settlement, bankruptcy and the ending.
    /// Commands never throw for rule violations: they return a rejected <see cref="CommandResult"/> and leave the state
    /// (and the <see cref="Snapshot"/> reference) unchanged. All checks run before any state change.
    /// Deterministic: no clock, randomness or static mutable state (R-001).
    /// </summary>
    public sealed class OrigamiGame
    {
        // ---- run state (D-120) ----
        private int _day;
        private int _money;
        private int _sheetsLeft;
        private WorkshopMode _mode;
        private bool _bookOpen;
        private int _bookPage;
        private bool[] _owned;                       // by catalogue index
        private bool _freeSheetAvailable;
        private bool _tutorialFinished;
        private bool _hasSat;
        private List<DisplayedPiece> _display;
        private CompletionCounts _completions;
        private int _failures;
        private int _totalSales;
        private int _totalPaperCost;
        private int _totalBookCost;
        private RunEndReason _endReason;
        private string _endingId;

        // ---- sheet on the table (D-121) ----
        private FoldSession _session;                // null = no sheet
        private bool _sheetIsTutorial;
        private bool _guideSkipped;
        private List<StepScore> _guideScores;

        /// <summary>Same state as right after <see cref="NewRun"/>.</summary>
        public OrigamiGame()
        {
            Reset();
            Snapshot = BuildSnapshot();
        }

        /// <summary>Immutable state view; replaced only when a command is accepted.</summary>
        public GameSnapshot Snapshot { get; private set; }

        // =====================================================================================================
        // Run
        // =====================================================================================================

        /// <summary>R-002 start state. Always accepted (also after the end); discards the sheet, display and tutorial state, no ending.</summary>
        public CommandResult NewRun()
        {
            Reset();
            return Accepted();
        }

        // =====================================================================================================
        // Modes
        // =====================================================================================================

        /// <summary>RunEnded → WrongMode(≠Walking) → UnknownTarget → (bed) SheetOnTable.</summary>
        public CommandResult Interact(string target)
        {
            if (IsEnded) return Rejected(RejectReason.RunEnded);
            if (_mode != WorkshopMode.Walking) return Rejected(RejectReason.WrongMode);
            if (!InteractTargets.IsValid(target)) return Rejected(RejectReason.UnknownTarget);
            if (string.Equals(target, InteractTargets.Bed, StringComparison.Ordinal) && _session != null)
                return Rejected(RejectReason.SheetOnTable);

            switch (target)
            {
                case InteractTargets.Table:
                    _mode = WorkshopMode.Seated;
                    _bookOpen = false;
                    _hasSat = true;
                    break;
                case InteractTargets.Bookshelf:
                    _mode = WorkshopMode.Bookshelf;
                    break;
                case InteractTargets.Counter:
                    _mode = WorkshopMode.Counter;
                    break;
                default:
                    _mode = WorkshopMode.SleepConfirm;
                    break;
            }
            return Accepted();
        }

        /// <summary>RunEnded → WrongMode(≠Seated). Walking, book closed, the sheet stays on the table.</summary>
        public CommandResult StandUp()
        {
            if (IsEnded) return Rejected(RejectReason.RunEnded);
            if (_mode != WorkshopMode.Seated) return Rejected(RejectReason.WrongMode);
            _mode = WorkshopMode.Walking;
            _bookOpen = false;
            return Accepted();
        }

        /// <summary>RunEnded → WrongMode(≠Seated). Opens the book (idempotent).</summary>
        public CommandResult OpenBook()
        {
            if (IsEnded) return Rejected(RejectReason.RunEnded);
            if (_mode != WorkshopMode.Seated) return Rejected(RejectReason.WrongMode);
            _bookOpen = true;
            return Accepted();
        }

        /// <summary>RunEnded → WrongMode(not Seated/Bookshelf). Seated: close (idempotent). Bookshelf: back to Walking.</summary>
        public CommandResult CloseBook()
        {
            if (IsEnded) return Rejected(RejectReason.RunEnded);
            if (_mode == WorkshopMode.Seated) _bookOpen = false;
            else if (_mode == WorkshopMode.Bookshelf) _mode = WorkshopMode.Walking;
            else return Rejected(RejectReason.WrongMode);
            return Accepted();
        }

        /// <summary>RunEnded → WrongMode(book not visible). Page = BookLayout.ClampToSpread(page).</summary>
        public CommandResult TurnPage(int page)
        {
            if (IsEnded) return Rejected(RejectReason.RunEnded);
            if (!BookVisible) return Rejected(RejectReason.WrongMode);
            _bookPage = BookLayout.ClampToSpread(page);
            return Accepted();
        }

        /// <summary>RunEnded → WrongMode(book not visible) → UnknownModel → AlreadyOwned → NotEnoughMoney (money = price allowed).</summary>
        public CommandResult BuyPage(string modelId)
        {
            if (IsEnded) return Rejected(RejectReason.RunEnded);
            if (!BookVisible) return Rejected(RejectReason.WrongMode);
            int index = ModelCatalog.IndexOf(modelId);
            if (index < 0) return Rejected(RejectReason.UnknownModel);
            if (_owned[index]) return Rejected(RejectReason.AlreadyOwned);
            int price = ModelCatalog.Models[index].PagePrice;
            if (_money < price) return Rejected(RejectReason.NotEnoughMoney);

            _money -= price;
            _totalBookCost += price;
            _owned[index] = true;
            return Accepted();
        }

        /// <summary>RunEnded → WrongMode(≠Counter). Walking.</summary>
        public CommandResult CloseCounter()
        {
            if (IsEnded) return Rejected(RejectReason.RunEnded);
            if (_mode != WorkshopMode.Counter) return Rejected(RejectReason.WrongMode);
            _mode = WorkshopMode.Walking;
            return Accepted();
        }

        /// <summary>
        /// RunEnded → WrongMode(≠SleepConfirm). No: Walking. Yes: settlement (R-016) — sells the display at today's prices,
        /// then day 7 ends the run (Completed), otherwise next day (5 sheets, Walking) and bankruptcy check (money &lt; 2, R-017).
        /// </summary>
        public CommandResult Sleep(bool yes)
        {
            if (IsEnded) return Rejected(RejectReason.RunEnded);
            if (_mode != WorkshopMode.SleepConfirm) return Rejected(RejectReason.WrongMode);
            if (!yes)
            {
                _mode = WorkshopMode.Walking;
                return Accepted();
            }

            int settledDay = _day;
            string popular = ModelCatalog.PopularModelId(settledDay);
            var lines = new List<SaleLine>(_display.Count);
            int total = 0;
            foreach (var piece in _display)
            {
                int price = Pricing.SalePrice(piece.ModelId, piece.Grade, settledDay);
                lines.Add(new SaleLine(piece.ModelId, piece.Grade, string.Equals(piece.ModelId, popular, StringComparison.Ordinal), price));
                total += price;
            }
            _money += total;
            _totalSales += total;
            _display.Clear();

            var reason = RunEndReason.None;
            if (settledDay >= RuleConstants.TotalDays)
            {
                reason = RunEndReason.Completed;
            }
            else
            {
                _day = settledDay + 1;
                _sheetsLeft = RuleConstants.DailySheets;
                _mode = WorkshopMode.Walking;
                if (_money < RuleConstants.PaperCost) reason = RunEndReason.Bankrupt;
            }

            if (reason != RunEndReason.None)
            {
                _endReason = reason;
                _mode = WorkshopMode.Ended;
                _bookOpen = false;
                _endingId = EndingRules.Evaluate(reason, _money, _completions);
            }

            var settlement = new SettlementResult(settledDay, lines, total, _money, reason,
                reason == RunEndReason.None ? ModelCatalog.PopularModelId(_day) : null);
            return Accepted(settlement: settlement);
        }

        // =====================================================================================================
        // Sheet
        // =====================================================================================================

        /// <summary>
        /// RunEnded → WrongMode(≠Seated) → SheetOnTable → NoSheetsLeft → NotEnoughMoney (paid sheet and money &lt; 2).
        /// The run's first sheet is free and is the tutorial sheet; others cost 2. Sheets left −1.
        /// </summary>
        public CommandResult TakeSheet()
        {
            if (IsEnded) return Rejected(RejectReason.RunEnded);
            if (_mode != WorkshopMode.Seated) return Rejected(RejectReason.WrongMode);
            if (_session != null) return Rejected(RejectReason.SheetOnTable);
            if (_sheetsLeft <= 0) return Rejected(RejectReason.NoSheetsLeft);
            bool free = _freeSheetAvailable;
            if (!free && _money < RuleConstants.PaperCost) return Rejected(RejectReason.NotEnoughMoney);

            if (!free)
            {
                _money -= RuleConstants.PaperCost;
                _totalPaperCost += RuleConstants.PaperCost;
            }
            _freeSheetAvailable = false;
            _sheetsLeft--;
            _session = new FoldSession();
            _sheetIsTutorial = free;
            _guideSkipped = false;
            _guideScores = new List<StepScore>();
            return Accepted();
        }

        /// <summary>
        /// RunEnded → WrongMode(≠Seated) → NoSheet → InvalidFold (layers &lt; 0, non-finite coordinates, or engine FoldError).
        /// Applies the fold as given (no magnet). Records a guide step score when the guide was visible.
        /// </summary>
        public CommandResult Fold(Vec2 grab, Vec2 drop, bool mountain, int layers)
        {
            return FoldOrCrease(FoldStepKind.Fold, grab, drop, mountain, layers);
        }

        /// <summary>Same checks as <see cref="Fold"/>; leaves crease marks only.</summary>
        public CommandResult Crease(Vec2 grab, Vec2 drop, int layers)
        {
            return FoldOrCrease(FoldStepKind.Crease, grab, drop, false, layers);
        }

        /// <summary>RunEnded → WrongMode → NoSheet. Turns the sheet over (no guide score).</summary>
        public CommandResult Flip()
        {
            var r = CheckSheetCommand();
            if (r != null) return r;
            _session.Flip();
            return Accepted();
        }

        /// <summary>RunEnded → WrongMode → NoSheet → NothingToUndo. Cancels the last Fold/Crease/Flip; guide scores follow the action count.</summary>
        public CommandResult Undo()
        {
            var r = CheckSheetCommand();
            if (r != null) return r;
            if (!_session.CanUndo) return Rejected(RejectReason.NothingToUndo);
            _session.Undo();
            int keep = _session.ActionCount;
            if (_guideScores.Count > keep) _guideScores.RemoveRange(keep, _guideScores.Count - keep);
            return Accepted();
        }

        /// <summary>RunEnded → WrongMode → NoSheet → NothingToUndo. Back to the flat sheet; guide scores cleared.</summary>
        public CommandResult Unfold()
        {
            var r = CheckSheetCommand();
            if (r != null) return r;
            if (!_session.CanUndo) return Rejected(RejectReason.NothingToUndo);
            _session.Unfold();
            _guideScores.Clear();
            return Accepted();
        }

        /// <summary>
        /// RunEnded → WrongMode → NoSheet → NoFolds(FoldCount 0). Recognises the sheet against the owned pages (R-011~R-013):
        /// a piece goes on the display and its completion count +1; crumpled paper is a failure. The sheet leaves the table;
        /// finishing the tutorial sheet ends the tutorial.
        /// </summary>
        public CommandResult Finish()
        {
            var r = CheckSheetCommand();
            if (r != null) return r;
            if (_session.FoldCount == 0) return Rejected(RejectReason.NoFolds);

            var state = _session.State;
            var recognition = Recognizer.Recognize(state, OwnedIds(), ModelCatalog.Recognition);
            if (recognition.IsPiece)
            {
                _display.Add(new DisplayedPiece(recognition.ModelId, recognition.Grade, recognition.Score, recognition.Percent, state));
                _completions = _completions.Increment(recognition.ModelId, recognition.Grade);
            }
            else
            {
                _failures++;
            }
            RemoveSheet();
            return Accepted(recognition: recognition);
        }

        /// <summary>RunEnded → WrongMode → NoSheet. Failure +1, the sheet leaves the table (ends the tutorial if it was the tutorial sheet).</summary>
        public CommandResult Discard()
        {
            var r = CheckSheetCommand();
            if (r != null) return r;
            _failures++;
            RemoveSheet();
            return Accepted();
        }

        /// <summary>RunEnded → WrongMode(≠Seated) → NoGuide (guide not visible, no sheet included — A2-10). Hides this sheet's guide for good.</summary>
        public CommandResult SkipGuide()
        {
            if (IsEnded) return Rejected(RejectReason.RunEnded);
            if (_mode != WorkshopMode.Seated) return Rejected(RejectReason.WrongMode);
            if (CurrentGuide() == null) return Rejected(RejectReason.NoGuide);
            _guideSkipped = true;
            return Accepted();
        }

        // =====================================================================================================
        // internals
        // =====================================================================================================

        private bool IsEnded => _endReason != RunEndReason.None;

        private bool BookVisible => _mode == WorkshopMode.Bookshelf || (_mode == WorkshopMode.Seated && _bookOpen);

        private void Reset()
        {
            _day = 1;
            _money = RuleConstants.StartMoney;
            _sheetsLeft = RuleConstants.DailySheets;
            _mode = WorkshopMode.Walking;
            _bookOpen = false;
            _bookPage = BookLayout.ContentsPage;
            var models = ModelCatalog.Models;
            _owned = new bool[models.Count];
            for (int i = 0; i < models.Count; i++) _owned[i] = models[i].InitiallyOwned;
            _freeSheetAvailable = true;
            _tutorialFinished = false;
            _hasSat = false;
            _display = new List<DisplayedPiece>();
            _completions = CompletionCounts.Empty;
            _failures = 0;
            _totalSales = 0;
            _totalPaperCost = 0;
            _totalBookCost = 0;
            _endReason = RunEndReason.None;
            _endingId = null;
            _session = null;
            _sheetIsTutorial = false;
            _guideSkipped = false;
            _guideScores = new List<StepScore>();
        }

        /// <summary>Common prefix RunEnded → WrongMode(≠Seated) → NoSheet; null when all pass.</summary>
        private CommandResult CheckSheetCommand()
        {
            if (IsEnded) return Rejected(RejectReason.RunEnded);
            if (_mode != WorkshopMode.Seated) return Rejected(RejectReason.WrongMode);
            if (_session == null) return Rejected(RejectReason.NoSheet);
            return null;
        }

        private CommandResult FoldOrCrease(FoldStepKind kind, Vec2 grab, Vec2 drop, bool mountain, int layers)
        {
            var r = CheckSheetCommand();
            if (r != null) return r;
            if (layers < 0) return Rejected(RejectReason.InvalidFold, FoldError.InvalidLayers);
            // Assumption (not in the engine contract): NaN/∞ input cannot come from the pointer; reject it instead of
            // letting it reach the engine. Reported as NothingToFold.
            if (!IsFinite(grab) || !IsFinite(drop)) return Rejected(RejectReason.InvalidFold, FoldError.NothingToFold);

            var guide = CurrentGuide();   // the guide shown while the player made this action
            var plan = kind == FoldStepKind.Fold
                ? _session.Fold(grab, drop, mountain, layers)
                : _session.Crease(grab, drop, layers);
            if (!plan.Valid) return Rejected(RejectReason.InvalidFold, plan.Error);   // session unchanged

            StepScore score = null;
            if (guide != null)
            {
                score = StepScoring.Evaluate(guide, kind, grab, drop);
                _guideScores.Add(score);
            }
            return Accepted(plan: plan, guideScore: score);
        }

        private static bool IsFinite(Vec2 v) => !double.IsNaN(v.X) && !double.IsInfinity(v.X) && !double.IsNaN(v.Y) && !double.IsInfinity(v.Y);

        /// <summary>C-150 "가이드 보임": tutorial sheet ∧ not skipped ∧ ActionCount &lt; 6 → PlaneGuide.Steps[ActionCount]; else null.</summary>
        private GuideStep CurrentGuide()
        {
            if (_session == null || !_sheetIsTutorial || _guideSkipped) return null;
            int a = _session.ActionCount;
            return a < PlaneGuide.StepCount ? PlaneGuide.Steps[a] : null;
        }

        private void RemoveSheet()
        {
            if (_sheetIsTutorial) _tutorialFinished = true;
            _session = null;
            _sheetIsTutorial = false;
            _guideSkipped = false;
            _guideScores = new List<StepScore>();
        }

        private List<string> OwnedIds()
        {
            var ids = new List<string>();
            var models = ModelCatalog.Models;
            for (int i = 0; i < models.Count; i++) if (_owned[i]) ids.Add(models[i].Id);
            return ids;
        }

        private CommandResult Rejected(RejectReason reason, FoldError foldError = FoldError.None) =>
            CommandResult.Reject(reason, foldError);

        private CommandResult Accepted(FoldPlan plan = null, RecognitionResult recognition = null, StepScore guideScore = null,
            SettlementResult settlement = null)
        {
            Snapshot = BuildSnapshot();
            return CommandResult.Accept(plan, recognition, guideScore, settlement);
        }

        private GameSnapshot BuildSnapshot()
        {
            SheetSnapshot sheet = null;
            if (_session != null)
            {
                sheet = new SheetSnapshot(_session.State, _session.FoldCount, _session.CreaseCount, _session.CanUndo,
                    _sheetIsTutorial, CurrentGuide(), _guideScores);
            }
            return new GameSnapshot(_day, _money, _sheetsLeft, _mode, _bookOpen, _bookPage,
                new ReadOnlyCollection<string>(OwnedIds()), _freeSheetAvailable, _tutorialFinished, _hasSat, sheet,
                new ReadOnlyCollection<DisplayedPiece>(new List<DisplayedPiece>(_display)), _completions, _failures,
                _totalSales, _totalPaperCost, _totalBookCost, ModelCatalog.PopularModelId(_day), _endReason, _endingId);
        }
    }
}
