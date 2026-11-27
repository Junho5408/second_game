using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Origami.Core.Content;
using Origami.Core.Flow;

namespace Origami.Runtime.UI
{
    /// <summary>
    /// Buttons of the flow's current screen (ARCHITECTURE.md §5 table, C-031 labels), top to bottom.
    /// Every button calls exactly one <see cref="OrigamiFlow"/> method — the same method its key maps to
    /// (C-020 key table) — so F-007 AC-05 "button = key" holds by construction and is testable without OnGUI.
    /// <see cref="OrigamiView"/> draws exactly this list.
    /// </summary>
    /// <remarks>
    /// Extension of C-031 (not in the contract table): a pure helper so the button → Flow wiring can be tested
    /// synchronously in PlayMode. It makes no rule decisions; labels use <see cref="FlowTexts"/>.
    /// </remarks>
    public static class OrigamiButtons
    {
        public const string EndDayLabel = "하루 마치기";
        public const string CloseLabel = "닫기";
        public const string HintLabel = "힌트";
        public const string AbandonLabel = "포기";
        public const string ConfirmAbandonLabel = "포기한다";
        public const string CancelAbandonLabel = "계속 접기";
        public const string AcknowledgeLabel = "확인";
        public const string RestartLabel = "다시 시작";

        /// <summary>"접기 시작 ({MoneyText(paperCost)})".</summary>
        public static string StartLabel(int paperCost) => "접기 시작 (" + FlowTexts.MoneyText(paperCost) + ")";

        /// <summary>"{ActionGlyph} {ActionName}", e.g. "↑ 위로 접기".</summary>
        public static string ActionLabel(FoldAction action) => FlowTexts.ActionGlyph(action) + " " + FlowTexts.ActionName(action);

        /// <summary>
        /// Buttons for <paramref name="flow"/>'s current screen. A new read-only list on every call.
        /// Workbench: 5 model buttons (display name, <see cref="OrigamiFlow.SelectModel"/>) + "하루 마치기".
        /// Preview: "접기 시작 (n원)", "닫기". Folding: 5 actions (U, D, L, R, F), "힌트", "포기".
        /// AbandonConfirm: "포기한다", "계속 접기". Notice / Settlement: "확인". Result: "다시 시작".
        /// </summary>
        /// <exception cref="ArgumentNullException">flow is null.</exception>
        public static IReadOnlyList<OrigamiButton> For(OrigamiFlow flow)
        {
            if (flow == null) throw new ArgumentNullException(nameof(flow));
            var list = new List<OrigamiButton>();
            switch (flow.Screen)
            {
                case FlowScreen.Workbench:
                    foreach (var model in OrigamiCatalog.Models)
                    {
                        string id = model.Id;
                        list.Add(new OrigamiButton(model.DisplayName, id, () => flow.SelectModel(id)));
                    }
                    list.Add(new OrigamiButton(EndDayLabel, null, flow.EndDay));
                    break;
                case FlowScreen.Preview:
                    var selected = OrigamiCatalog.FindModel(flow.SelectedModelId);
                    int cost = selected != null ? selected.PaperCost : 0;
                    list.Add(new OrigamiButton(StartLabel(cost), null, flow.StartSelected));
                    list.Add(new OrigamiButton(CloseLabel, null, flow.ClosePreview));
                    break;
                case FlowScreen.Folding:
                    foreach (var action in FoldActions.All)
                    {
                        var a = action;
                        list.Add(new OrigamiButton(ActionLabel(a), null, () => flow.Fold(a)));
                    }
                    list.Add(new OrigamiButton(HintLabel, null, flow.Hint));
                    list.Add(new OrigamiButton(AbandonLabel, null, flow.RequestAbandon));
                    break;
                case FlowScreen.AbandonConfirm:
                    list.Add(new OrigamiButton(ConfirmAbandonLabel, null, flow.ConfirmAbandon));
                    list.Add(new OrigamiButton(CancelAbandonLabel, null, flow.CancelAbandon));
                    break;
                case FlowScreen.Notice:
                case FlowScreen.Settlement:
                    list.Add(new OrigamiButton(AcknowledgeLabel, null, flow.Acknowledge));
                    break;
                case FlowScreen.Result:
                    list.Add(new OrigamiButton(RestartLabel, null, flow.Restart));
                    break;
            }
            return new ReadOnlyCollection<OrigamiButton>(list);
        }
    }
}
