using Origami.Core.Content;
using Origami.Core.Run;

namespace Origami.Core.Flow
{
    /// <summary>
    /// Engine-free F-007 screen state machine. Sole owner of the runtime <see cref="OrigamiRun"/>; buttons and keys
    /// both end up in the same methods here, so "key and button are the same command" holds by construction.
    /// Every public command method returns true if it did something on the current screen, false if it was
    /// ignored (wrong screen). Core rejections are "handled" (true) and shown via <see cref="Message"/>.
    /// See INTERFACE_CONTRACTS.md C-020.
    /// </summary>
    /// <remarks>
    /// Ignored calls (false) change nothing at all, not even <see cref="Message"/>.
    /// Flow-level blocks (unknown/locked model in <see cref="SelectModel"/>) set <see cref="Message"/> without a Core call,
    /// so <see cref="LastResult"/> and <see cref="Snapshot"/> keep their previous values.
    /// </remarks>
    public sealed class OrigamiFlow
    {
        private readonly OrigamiRun _run = new OrigamiRun();
        private FlowScreen _screen;
        private RunSnapshot _snapshot;
        private string _selectedModelId;
        private CommandResult _lastResult;
        private SettlementResult _settlement;
        private string _message;

        /// <summary>New run (R-002), screen Workbench, empty message.</summary>
        public OrigamiFlow()
        {
            _screen = FlowScreen.Workbench;
            _snapshot = _run.GetSnapshot();
            _message = "";
        }

        public FlowScreen Screen => _screen;

        /// <summary>Snapshot taken after the latest command (refreshed after every Core call, accepted or rejected).</summary>
        public RunSnapshot Snapshot => _snapshot;

        /// <summary>Model shown on Preview / being folded / shown on Notice; null on other screens.</summary>
        public string SelectedModelId => _selectedModelId;

        /// <summary>Result of the latest Core command (default before any).</summary>
        public CommandResult LastResult => _lastResult;

        /// <summary>Settlement shown on the Settlement screen; null on other screens.</summary>
        public SettlementResult Settlement => _settlement;

        /// <summary>One guidance line for the latest rejection (FlowTexts.Rejection), "" if none. Cleared by any screen change or accepted command.</summary>
        public string Message => _message;

        /// <summary>Workbench: open Preview of an unlocked model. Unknown/locked → Message only (no preview of locked models).</summary>
        public bool SelectModel(string modelId)
        {
            if (Screen != FlowScreen.Workbench) return false;
            var model = OrigamiCatalog.FindModel(modelId);
            if (model == null)
            {
                Reject(RejectReason.UnknownModel, null);
                return true;
            }
            if (!Snapshot.IsUnlocked(model.Id))
            {
                Reject(RejectReason.Locked, model);
                return true;
            }
            _selectedModelId = model.Id;
            GoTo(FlowScreen.Preview);
            return true;
        }

        /// <summary>Preview → Workbench.</summary>
        public bool ClosePreview()
        {
            if (Screen != FlowScreen.Preview) return false;
            _selectedModelId = null;
            GoTo(FlowScreen.Workbench);
            return true;
        }

        /// <summary>Preview: Core Start(selected). Accepted → Folding; rejected → Message, stay on Preview.</summary>
        public bool StartSelected()
        {
            if (Screen != FlowScreen.Preview) return false;
            if (Execute(_run.Start(SelectedModelId))) GoTo(FlowScreen.Folding);
            return true;
        }

        /// <summary>Folding: Core Fold(action). FoldCompleted/FoldRuined → Notice; rejection → Message.</summary>
        public bool Fold(FoldAction action)
        {
            if (Screen != FlowScreen.Folding) return false;
            if (Execute(_run.Fold(action))
                && (LastResult.Outcome == CommandOutcome.FoldCompleted || LastResult.Outcome == CommandOutcome.FoldRuined))
            {
                GoTo(FlowScreen.Notice);
            }
            return true;
        }

        /// <summary>Folding: Core Hint().</summary>
        public bool Hint()
        {
            if (Screen != FlowScreen.Folding) return false;
            Execute(_run.Hint());
            return true;
        }

        /// <summary>Folding → AbandonConfirm (no Core call, state unchanged).</summary>
        public bool RequestAbandon()
        {
            if (Screen != FlowScreen.Folding) return false;
            GoTo(FlowScreen.AbandonConfirm);
            return true;
        }

        /// <summary>AbandonConfirm: Core Abandon() → Notice.</summary>
        public bool ConfirmAbandon()
        {
            if (Screen != FlowScreen.AbandonConfirm) return false;
            // Abandon cannot be rejected here (a fold is in progress); if it ever were, go back to folding with the message.
            GoTo(Execute(_run.Abandon()) ? FlowScreen.Notice : FlowScreen.Folding, keepMessage: true);
            return true;
        }

        /// <summary>AbandonConfirm → Folding (state unchanged).</summary>
        public bool CancelAbandon()
        {
            if (Screen != FlowScreen.AbandonConfirm) return false;
            GoTo(FlowScreen.Folding);
            return true;
        }

        /// <summary>Workbench: Core EndDay(). Accepted → Settlement; rejected → Message.</summary>
        public bool EndDay()
        {
            if (Screen != FlowScreen.Workbench) return false;
            if (Execute(_run.EndDay()))
            {
                _settlement = LastResult.Settlement;
                GoTo(FlowScreen.Settlement);
            }
            return true;
        }

        /// <summary>Notice → Workbench; Settlement → Result if the run ended, else Workbench.</summary>
        public bool Acknowledge()
        {
            switch (Screen)
            {
                case FlowScreen.Notice:
                    _selectedModelId = null;
                    GoTo(FlowScreen.Workbench);
                    return true;
                case FlowScreen.Settlement:
                    bool ended = Settlement != null ? Settlement.RunEnded : Snapshot.IsRunEnded;
                    _settlement = null;
                    GoTo(ended ? FlowScreen.Result : FlowScreen.Workbench);
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>Result: Core NewRun() → Workbench.</summary>
        public bool Restart()
        {
            if (Screen != FlowScreen.Result) return false;
            Execute(_run.NewRun());
            _selectedModelId = null;
            _settlement = null;
            GoTo(FlowScreen.Workbench);
            return true;
        }

        /// <summary>
        /// One frame of keys. Only the highest-priority pressed key is considered
        /// (Escape, Enter, Hint, FoldUp, FoldDown, FoldLeft, FoldRight, Flip); it is mapped per screen
        /// (C-020 key table) or ignored. Returns true if it did something.
        /// </summary>
        public bool ProcessKeys(FlowKeys keys)
        {
            switch (TopKey(keys))
            {
                case FlowKeys.Escape:
                    switch (Screen)
                    {
                        case FlowScreen.Folding: return RequestAbandon();
                        case FlowScreen.AbandonConfirm: return CancelAbandon();
                        case FlowScreen.Preview: return ClosePreview();
                        default: return false;
                    }
                case FlowKeys.Enter:
                    switch (Screen)
                    {
                        case FlowScreen.AbandonConfirm: return ConfirmAbandon();
                        case FlowScreen.Preview: return StartSelected();
                        case FlowScreen.Notice:
                        case FlowScreen.Settlement: return Acknowledge();
                        default: return false;
                    }
                case FlowKeys.Hint: return Hint();
                case FlowKeys.FoldUp: return Fold(FoldAction.U);
                case FlowKeys.FoldDown: return Fold(FoldAction.D);
                case FlowKeys.FoldLeft: return Fold(FoldAction.L);
                case FlowKeys.FoldRight: return Fold(FoldAction.R);
                case FlowKeys.Flip: return Fold(FoldAction.F);
                default: return false;
            }
        }

        private static FlowKeys TopKey(FlowKeys keys)
        {
            if ((keys & FlowKeys.Escape) != 0) return FlowKeys.Escape;
            if ((keys & FlowKeys.Enter) != 0) return FlowKeys.Enter;
            if ((keys & FlowKeys.Hint) != 0) return FlowKeys.Hint;
            if ((keys & FlowKeys.FoldUp) != 0) return FlowKeys.FoldUp;
            if ((keys & FlowKeys.FoldDown) != 0) return FlowKeys.FoldDown;
            if ((keys & FlowKeys.FoldLeft) != 0) return FlowKeys.FoldLeft;
            if ((keys & FlowKeys.FoldRight) != 0) return FlowKeys.FoldRight;
            if ((keys & FlowKeys.Flip) != 0) return FlowKeys.Flip;
            return FlowKeys.None;
        }

        /// <summary>Records a Core result, refreshes the snapshot and sets/clears Message. Returns IsAccepted.</summary>
        private bool Execute(CommandResult result)
        {
            _lastResult = result;
            _snapshot = _run.GetSnapshot();
            if (result.IsAccepted)
            {
                _message = "";
                return true;
            }
            _message = FlowTexts.Rejection(result.Reason, OrigamiCatalog.FindModel(SelectedModelId), Snapshot.Completions);
            return false;
        }

        private void Reject(RejectReason reason, ModelDefinition model)
        {
            _message = FlowTexts.Rejection(reason, model, Snapshot.Completions);
        }

        private void GoTo(FlowScreen screen, bool keepMessage = false)
        {
            _screen = screen;
            if (!keepMessage) _message = "";
        }
    }
}
