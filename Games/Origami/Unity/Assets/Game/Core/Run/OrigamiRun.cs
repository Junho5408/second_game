using System;
using System.Collections.Generic;
using Origami.Core.Content;
using Origami.Core.Rules;

namespace Origami.Core.Run
{
    /// <summary>
    /// The single mutable game state of one run plus all player commands (F-001..F-006, R-001..R-015).
    /// Deterministic: no clock, no randomness, no static mutable state. Commands never throw for rule
    /// violations — they return a rejection and leave state untouched (R-015). Not thread-safe.
    /// See INTERFACE_CONTRACTS.md C-010.
    /// </summary>
    /// <remarks>
    /// Every command runs all of its checks first (in the documented order) and mutates state only after
    /// they pass, so a rejected command can never leave a partial change. Unlocks are not stored: they are
    /// derived from <see cref="CompletionCounts"/> via <see cref="Unlocking"/> (C-008).
    /// </remarks>
    public sealed class OrigamiRun
    {
        private int _day;
        private int _money;
        private int _attemptsLeft;
        private CompletionCounts _completions;
        private int _failureCount;
        private int _totalSales;
        private int _totalPaperCost;
        private readonly List<CompletedPiece> _todayPieces = new List<CompletedPiece>();
        private RunEndReason _endReason;
        private string _endingId;

        // Fold in progress; _foldModel == null means "not folding".
        private ModelDefinition _foldModel;
        private int _foldProgress;
        private int _foldMistakes;
        private int _foldHints;
        private bool _foldRevealed;

        /// <summary>Creates a run already in the R-002 start state (same as calling <see cref="NewRun"/>).</summary>
        public OrigamiRun()
        {
            Reset();
        }

        /// <summary>Always accepted (RunStarted). Discards any fold and today's pieces; no ending is evaluated.</summary>
        public CommandResult NewRun()
        {
            Reset();
            return CommandResult.Accepted(CommandOutcome.RunStarted);
        }

        /// <summary>Starts folding <paramref name="modelId"/> (R-004). Accepted → FoldStarted.</summary>
        public CommandResult Start(string modelId)
        {
            if (IsEnded) return CommandResult.Rejected(RejectReason.RunEnded);
            if (IsFolding) return CommandResult.Rejected(RejectReason.FoldInProgress);
            var model = OrigamiCatalog.FindModel(modelId);
            if (model == null) return CommandResult.Rejected(RejectReason.UnknownModel);
            if (!Unlocking.IsUnlocked(model.Id, _completions)) return CommandResult.Rejected(RejectReason.Locked);
            if (_attemptsLeft <= 0) return CommandResult.Rejected(RejectReason.NoAttemptsLeft);
            if (_money < model.PaperCost) return CommandResult.Rejected(RejectReason.NotEnoughMoney);

            _money -= model.PaperCost;
            _attemptsLeft -= 1;
            _totalPaperCost += model.PaperCost;
            _foldModel = model;
            _foldProgress = 0;
            _foldMistakes = 0;
            _foldHints = 0;
            _foldRevealed = false;
            return CommandResult.Accepted(CommandOutcome.FoldStarted);
        }

        /// <summary>One fold action (R-005, R-007, R-009, R-010). Accepted → StepCorrect / StepMistake / FoldCompleted / FoldRuined.</summary>
        public CommandResult Fold(FoldAction action)
        {
            if (IsEnded) return CommandResult.Rejected(RejectReason.RunEnded);
            if (!IsFolding) return CommandResult.Rejected(RejectReason.NoFold);
            if (!FoldActions.IsValid(action)) return CommandResult.Rejected(RejectReason.InvalidAction);

            if (action == _foldModel.Steps[_foldProgress])
            {
                _foldProgress += 1;
                _foldRevealed = false;
                if (_foldProgress < _foldModel.StepCount) return CommandResult.Accepted(CommandOutcome.StepCorrect);

                var piece = new CompletedPiece(_foldModel.Id, Grading.GradeFor(Grading.Penalty(_foldMistakes, _foldHints)));
                _todayPieces.Add(piece);
                _completions = _completions.Increment(piece.ModelId, piece.Grade);
                ClearFold();
                return CommandResult.Completed(piece);
            }

            // Wrong action: same step, reveal (if any) stays (F-003 AC-10).
            _foldMistakes += 1;
            if (_foldMistakes < RuleConstants.MistakeLimit) return CommandResult.Accepted(CommandOutcome.StepMistake);

            _failureCount += 1;
            ClearFold();
            return CommandResult.Accepted(CommandOutcome.FoldRuined);
        }

        /// <summary>Reveals the current step (R-006). Accepted → HintRevealed.</summary>
        public CommandResult Hint()
        {
            if (IsEnded) return CommandResult.Rejected(RejectReason.RunEnded);
            if (!IsFolding) return CommandResult.Rejected(RejectReason.NoFold);

            if (!_foldRevealed)
            {
                _foldRevealed = true;
                _foldHints += 1;
            }
            return CommandResult.Accepted(CommandOutcome.HintRevealed);
        }

        /// <summary>Gives up the current fold (R-008). Accepted → FoldAbandoned.</summary>
        public CommandResult Abandon()
        {
            if (IsEnded) return CommandResult.Rejected(RejectReason.RunEnded);
            if (!IsFolding) return CommandResult.Rejected(RejectReason.NoFold);

            _failureCount += 1;
            ClearFold();
            return CommandResult.Accepted(CommandOutcome.FoldAbandoned);
        }

        /// <summary>Evening settlement and day advance (R-012, R-013, R-014). Accepted → DayEnded / RunFinished with Settlement.</summary>
        public CommandResult EndDay()
        {
            if (IsEnded) return CommandResult.Rejected(RejectReason.RunEnded);
            if (IsFolding) return CommandResult.Rejected(RejectReason.FoldInProgress);

            // R-011/R-012: sell today's pieces at the settled day's prices, in completion order.
            int settledDay = _day;
            string popularId = OrigamiCatalog.PopularModelId(settledDay);
            var lines = new List<SaleLine>(_todayPieces.Count);
            int total = 0;
            for (int i = 0; i < _todayPieces.Count; i++)
            {
                var piece = _todayPieces[i];
                var model = OrigamiCatalog.FindModel(piece.ModelId);
                bool popular = string.Equals(popularId, piece.ModelId, StringComparison.Ordinal);
                int price = Pricing.SalePrice(model.BasePrice, piece.Grade, popular);
                lines.Add(new SaleLine(piece.ModelId, piece.Grade, popular, price));
                total += price;
            }
            _money += total;
            _totalSales += total;
            _todayPieces.Clear();

            if (settledDay >= RuleConstants.TotalDays)
            {
                // R-012: day 7 completes the run; no bankruptcy check (R-013 exception).
                _endReason = RunEndReason.Completed;
            }
            else
            {
                _day = settledDay + 1;
                _attemptsLeft = RuleConstants.DailyAttempts;
                // R-013: bankruptcy is checked only here, at the start of a new day.
                if (_money < CheapestUnlockedPaperCost()) _endReason = RunEndReason.Bankrupt;
            }

            bool ended = _endReason != RunEndReason.None;
            if (ended) _endingId = EndingRules.Evaluate(_endReason, _money, _completions);

            var settlement = new SettlementResult(settledDay, lines, total, _money, _endReason,
                ended ? null : OrigamiCatalog.PopularModelId(_day));
            return CommandResult.Settled(ended ? CommandOutcome.RunFinished : CommandOutcome.DayEnded, settlement);
        }

        /// <summary>A new immutable snapshot of the current state.</summary>
        public RunSnapshot GetSnapshot()
        {
            var unlocked = new List<string>(OrigamiCatalog.Models.Count);
            for (int i = 0; i < OrigamiCatalog.Models.Count; i++)
            {
                string id = OrigamiCatalog.Models[i].Id;
                if (Unlocking.IsUnlocked(id, _completions)) unlocked.Add(id);
            }

            FoldSnapshot fold = null;
            if (IsFolding)
            {
                var correct = new List<FoldAction>(_foldProgress);
                for (int i = 0; i < _foldProgress; i++) correct.Add(_foldModel.Steps[i]);
                int penalty = Grading.Penalty(_foldMistakes, _foldHints);
                fold = new FoldSnapshot(_foldModel.Id, _foldModel.StepCount, correct, _foldMistakes, _foldHints,
                    _foldRevealed ? _foldModel.Steps[_foldProgress] : default, penalty, Grading.GradeFor(penalty));
            }

            return new RunSnapshot(_day, _money, _attemptsLeft, OrigamiCatalog.PopularModelId(_day), unlocked,
                _completions, _failureCount, _totalSales, _totalPaperCost, _todayPieces, fold, _endReason, _endingId);
        }

        private bool IsEnded => _endReason != RunEndReason.None;

        private bool IsFolding => _foldModel != null;

        private void Reset()
        {
            _day = 1;
            _money = RuleConstants.StartMoney;
            _attemptsLeft = RuleConstants.DailyAttempts;
            _completions = CompletionCounts.Empty;
            _failureCount = 0;
            _totalSales = 0;
            _totalPaperCost = 0;
            _todayPieces.Clear();
            _endReason = RunEndReason.None;
            _endingId = null;
            ClearFold();
        }

        private void ClearFold()
        {
            _foldModel = null;
            _foldProgress = 0;
            _foldMistakes = 0;
            _foldHints = 0;
            _foldRevealed = false;
        }

        // R-013: "가장 싼 해금 작품의 종이값" (always plane = 1 in this version; plane is initially unlocked).
        private int CheapestUnlockedPaperCost()
        {
            int min = int.MaxValue;
            for (int i = 0; i < OrigamiCatalog.Models.Count; i++)
            {
                var m = OrigamiCatalog.Models[i];
                if (m.PaperCost < min && Unlocking.IsUnlocked(m.Id, _completions)) min = m.PaperCost;
            }
            return min;
        }
    }
}
