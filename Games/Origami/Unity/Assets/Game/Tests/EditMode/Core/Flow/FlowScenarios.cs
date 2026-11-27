using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Origami.Core.Content;
using Origami.Core.Flow;
using Origami.Core.Run;

namespace Origami.Tests.Flow
{
    // ORIGAMI-004: helpers that drive OrigamiFlow only through its public commands (C-020).
    internal static class FlowScenarios
    {
        public static readonly FlowScreen[] AllScreens = (FlowScreen[])Enum.GetValues(typeof(FlowScreen));

        /// <summary>Single keys in C-020 priority order (Escape highest).</summary>
        public static readonly FlowKeys[] KeysByPriority =
        {
            FlowKeys.Escape, FlowKeys.Enter, FlowKeys.Hint, FlowKeys.FoldUp, FlowKeys.FoldDown,
            FlowKeys.FoldLeft, FlowKeys.FoldRight, FlowKeys.Flip,
        };

        /// <summary>A fresh flow standing on <paramref name="screen"/> (plane is the selected model where relevant).</summary>
        public static OrigamiFlow At(FlowScreen screen)
        {
            var flow = new OrigamiFlow();
            switch (screen)
            {
                case FlowScreen.Workbench:
                    break;
                case FlowScreen.Preview:
                    True(flow.SelectModel(ModelIds.Plane), "SelectModel");
                    break;
                case FlowScreen.Folding:
                    StartFolding(flow, ModelIds.Plane);
                    break;
                case FlowScreen.AbandonConfirm:
                    StartFolding(flow, ModelIds.Plane);
                    True(flow.RequestAbandon(), "RequestAbandon");
                    break;
                case FlowScreen.Notice:
                    StartFolding(flow, ModelIds.Plane);
                    True(flow.RequestAbandon(), "RequestAbandon");
                    True(flow.ConfirmAbandon(), "ConfirmAbandon");
                    break;
                case FlowScreen.Settlement:
                    True(flow.EndDay(), "EndDay");
                    break;
                case FlowScreen.Result:
                    for (int d = 0; d < 7; d++)
                    {
                        True(flow.EndDay(), "EndDay " + (d + 1));
                        True(flow.Acknowledge(), "Acknowledge " + (d + 1));
                    }
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(screen));
            }
            Assert.AreEqual(screen, flow.Screen, "setup screen");
            return flow;
        }

        public static void StartFolding(OrigamiFlow flow, string modelId)
        {
            True(flow.SelectModel(modelId), "SelectModel(" + modelId + ")");
            Assert.AreEqual(FlowScreen.Preview, flow.Screen, "preview of " + modelId);
            True(flow.StartSelected(), "StartSelected(" + modelId + ")");
            Assert.AreEqual(FlowScreen.Folding, flow.Screen, "folding " + modelId + " (" + flow.Message + ")");
        }

        /// <summary>Make(m) through the flow: Start, correct steps, acknowledge the notice.</summary>
        public static void Make(OrigamiFlow flow, string modelId, int times = 1)
        {
            for (int t = 0; t < times; t++)
            {
                StartFolding(flow, modelId);
                foreach (var a in OrigamiCatalog.FindModel(modelId).Steps) True(flow.Fold(a), "Fold " + a);
                Assert.AreEqual(FlowScreen.Notice, flow.Screen);
                Assert.AreEqual(CommandOutcome.FoldCompleted, flow.LastResult.Outcome);
                True(flow.Acknowledge(), "Acknowledge notice");
            }
        }

        /// <summary>Waste(m) through the flow: Start, abandon with confirm, acknowledge.</summary>
        public static void Waste(OrigamiFlow flow, string modelId, int times = 1)
        {
            for (int t = 0; t < times; t++)
            {
                StartFolding(flow, modelId);
                True(flow.RequestAbandon(), "RequestAbandon");
                True(flow.ConfirmAbandon(), "ConfirmAbandon");
                Assert.AreEqual(CommandOutcome.FoldAbandoned, flow.LastResult.Outcome);
                True(flow.Acknowledge(), "Acknowledge notice");
            }
        }

        public static void True(bool handled, string what) => Assert.IsTrue(handled, what + " should be handled");

        public static IReadOnlyList<string> Lines(OrigamiFlow flow) => FlowTexts.ScreenLines(flow);

        public static string Text(OrigamiFlow flow) => string.Join("\n", FlowTexts.ScreenLines(flow));

        /// <summary>Everything observable about a flow, for "unchanged" and "same result" comparisons.</summary>
        public static string Fingerprint(OrigamiFlow flow)
        {
            var s = flow.Snapshot;
            var f = s.Fold;
            var parts = new List<string>
            {
                "screen=" + flow.Screen,
                "sel=" + (flow.SelectedModelId ?? "null"),
                "msg=" + flow.Message,
                "last=" + flow.LastResult,
                "settle=" + (flow.Settlement == null ? "null" : flow.Settlement.SettledDay + "/" + flow.Settlement.Total),
                "day=" + s.Day, "money=" + s.Money, "att=" + s.AttemptsLeft, "fail=" + s.FailureCount,
                "sales=" + s.TotalSales, "paper=" + s.TotalPaperCost, "end=" + s.EndReason, "ending=" + (s.EndingId ?? "null"),
                "today=" + string.Join(",", s.TodayPieces.Select(p => p.ToString())),
                "fold=" + (f == null ? "none" : f.ModelId + " " + f.Progress + "/" + f.StepCount + " m" + f.Mistakes + " h" + f.Hints + " r" + f.RevealedAction),
                "lines=" + Text(flow),
            };
            return string.Join("|", parts);
        }
    }
}
