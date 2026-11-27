using System;
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
    public sealed class OrigamiFlow
    {
        /// <summary>New run (R-002), screen Workbench, empty message.</summary>
        public OrigamiFlow()
        {
            throw new NotImplementedException();
        }

        public FlowScreen Screen => throw new NotImplementedException();

        /// <summary>Snapshot taken after the latest command (refreshed after every Core call, accepted or rejected).</summary>
        public RunSnapshot Snapshot => throw new NotImplementedException();

        /// <summary>Model shown on Preview / being folded / shown on Notice; null on other screens.</summary>
        public string SelectedModelId => throw new NotImplementedException();

        /// <summary>Result of the latest Core command (default before any).</summary>
        public CommandResult LastResult => throw new NotImplementedException();

        /// <summary>Settlement shown on the Settlement screen; null on other screens.</summary>
        public SettlementResult Settlement => throw new NotImplementedException();

        /// <summary>One guidance line for the latest rejection (FlowTexts.Rejection), "" if none. Cleared by any screen change or accepted command.</summary>
        public string Message => throw new NotImplementedException();

        /// <summary>Workbench: open Preview of an unlocked model. Unknown/locked → Message only (no preview of locked models).</summary>
        public bool SelectModel(string modelId) => throw new NotImplementedException();

        /// <summary>Preview → Workbench.</summary>
        public bool ClosePreview() => throw new NotImplementedException();

        /// <summary>Preview: Core Start(selected). Accepted → Folding; rejected → Message, stay on Preview.</summary>
        public bool StartSelected() => throw new NotImplementedException();

        /// <summary>Folding: Core Fold(action). FoldCompleted/FoldRuined → Notice; rejection → Message.</summary>
        public bool Fold(FoldAction action) => throw new NotImplementedException();

        /// <summary>Folding: Core Hint().</summary>
        public bool Hint() => throw new NotImplementedException();

        /// <summary>Folding → AbandonConfirm (no Core call, state unchanged).</summary>
        public bool RequestAbandon() => throw new NotImplementedException();

        /// <summary>AbandonConfirm: Core Abandon() → Notice.</summary>
        public bool ConfirmAbandon() => throw new NotImplementedException();

        /// <summary>AbandonConfirm → Folding (state unchanged).</summary>
        public bool CancelAbandon() => throw new NotImplementedException();

        /// <summary>Workbench: Core EndDay(). Accepted → Settlement; rejected → Message.</summary>
        public bool EndDay() => throw new NotImplementedException();

        /// <summary>Notice → Workbench; Settlement → Result if the run ended, else Workbench.</summary>
        public bool Acknowledge() => throw new NotImplementedException();

        /// <summary>Result: Core NewRun() → Workbench.</summary>
        public bool Restart() => throw new NotImplementedException();

        /// <summary>
        /// One frame of keys. Only the highest-priority pressed key is considered
        /// (Escape, Enter, Hint, FoldUp, FoldDown, FoldLeft, FoldRight, Flip); it is mapped per screen
        /// (C-020 key table) or ignored. Returns true if it did something.
        /// </summary>
        public bool ProcessKeys(FlowKeys keys) => throw new NotImplementedException();
    }
}
