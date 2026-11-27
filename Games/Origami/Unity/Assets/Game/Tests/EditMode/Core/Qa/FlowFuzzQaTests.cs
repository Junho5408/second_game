using System;
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using Origami.Core.Content;
using Origami.Core.Flow;
using Origami.Core.Rules;
using Origami.Core.Run;

namespace Origami.Tests.Qa
{
    // QA ORIGAMI-004: seeded exploratory testing of OrigamiFlow (C-020) against an independent reference flow
    // (FlowReference, written from the C-020 tables, driving its own OrigamiRun). Long random frame streams mix
    // key combinations (all 256 masks, single keys, "smart" keys that make progress) with button calls (including
    // calls on the wrong screen, unknown/locked/null model IDs) and restarts. After EVERY frame:
    //  - flow == reference (screen, return value, selection, message, last result, settlement, full snapshot);
    //  - screen/snapshot consistency (Folding|AbandonConfirm <=> fold in progress, Settlement <=> settlement, ...);
    //  - Lines never reveal remaining fold steps: on Folding/AbandonConfirm the glyphs shown are exactly
    //    steps[0..progress) plus steps[progress] when hinted; glyphs appear nowhere else except the Preview;
    //  - an abandon only happens on a frame that is an Enter-only confirm (or the confirm button) on AbandonConfirm.
    // Own xorshift PRNG (no System.Random) so streams are identical under .NET and Unity/Mono.
    public class FlowFuzzQaTests
    {
        private const int Streams = 300;
        private const int FramesPerStream = 600;

        private sealed class Rng
        {
            private ulong _s;
            public Rng(int seed) { _s = (ulong)(uint)seed * 0x9E3779B97F4A7C15UL + 0x2545F4914F6CDD1DUL; if (_s == 0) _s = 1; }
            public uint Next() { _s ^= _s << 13; _s ^= _s >> 7; _s ^= _s << 17; return (uint)(_s >> 16); }
            public int Below(int n) => (int)(Next() % (uint)n);
            public bool Chance(int pct) => Below(100) < pct;
        }

        private static readonly string[] SelectIds =
        {
            ModelIds.Plane, ModelIds.Boat, ModelIds.Frog, ModelIds.Crane, ModelIds.Dragon, null, "", "PLANE", "swan",
        };

        private static readonly FlowKeys[] Priority =
        {
            FlowKeys.Escape, FlowKeys.Enter, FlowKeys.Hint, FlowKeys.FoldUp, FlowKeys.FoldDown,
            FlowKeys.FoldLeft, FlowKeys.FoldRight, FlowKeys.Flip,
        };

        /// <summary>Independent model of C-020 (screens, Message, key table), own OrigamiRun.</summary>
        private sealed class FlowReference
        {
            public readonly OrigamiRun Run = new OrigamiRun();
            public FlowScreen Screen = FlowScreen.Workbench;
            public string Selected;
            public CommandResult Last;
            public SettlementResult Settlement;
            public string Message = "";

            private static string RejectText(RejectReason r, string modelId, CompletionCounts counts)
            {
                switch (r)
                {
                    case RejectReason.RunEnded: return "런이 끝났습니다";
                    case RejectReason.FoldInProgress: return "접는 중에는 할 수 없습니다";
                    case RejectReason.UnknownModel: return "없는 작품입니다";
                    case RejectReason.NoAttemptsLeft: return "오늘은 더 접을 수 없습니다";
                    case RejectReason.NotEnoughMoney: return "돈이 부족합니다";
                    case RejectReason.NoFold: return "접는 중이 아닙니다";
                    case RejectReason.InvalidAction: return "알 수 없는 동작입니다";
                    case RejectReason.Locked:
                        var m = OrigamiCatalog.FindModel(modelId);
                        if (m == null || m.IsInitiallyUnlocked) return "";
                        int p = Math.Min(counts.Total(m.UnlockPrerequisiteId), 3);
                        return OrigamiCatalog.FindModel(m.UnlockPrerequisiteId).DisplayName + " 3개 완성 시 (" + p + "/3)";
                    default: return "";
                }
            }

            private bool Exec(CommandResult r)
            {
                Last = r;
                Message = r.IsAccepted ? "" : RejectText(r.Reason, Selected, Run.GetSnapshot().Completions);
                return r.IsAccepted;
            }

            private void Go(FlowScreen s) { Screen = s; Message = ""; }

            public bool SelectModel(string id)
            {
                if (Screen != FlowScreen.Workbench) return false;
                var m = OrigamiCatalog.FindModel(id);
                if (m == null) { Message = "없는 작품입니다"; return true; }
                if (!Unlocking.IsUnlocked(m.Id, Run.GetSnapshot().Completions)) { Message = RejectText(RejectReason.Locked, m.Id, Run.GetSnapshot().Completions); return true; }
                Selected = m.Id; Go(FlowScreen.Preview); return true;
            }

            public bool ClosePreview() { if (Screen != FlowScreen.Preview) return false; Selected = null; Go(FlowScreen.Workbench); return true; }
            public bool StartSelected() { if (Screen != FlowScreen.Preview) return false; if (Exec(Run.Start(Selected))) Go(FlowScreen.Folding); return true; }

            public bool Fold(FoldAction a)
            {
                if (Screen != FlowScreen.Folding) return false;
                if (Exec(Run.Fold(a)) && (Last.Outcome == CommandOutcome.FoldCompleted || Last.Outcome == CommandOutcome.FoldRuined)) Go(FlowScreen.Notice);
                return true;
            }

            public bool Hint() { if (Screen != FlowScreen.Folding) return false; Exec(Run.Hint()); return true; }
            public bool RequestAbandon() { if (Screen != FlowScreen.Folding) return false; Go(FlowScreen.AbandonConfirm); return true; }
            public bool ConfirmAbandon() { if (Screen != FlowScreen.AbandonConfirm) return false; Exec(Run.Abandon()); Go(FlowScreen.Notice); return true; }
            public bool CancelAbandon() { if (Screen != FlowScreen.AbandonConfirm) return false; Go(FlowScreen.Folding); return true; }

            public bool EndDay()
            {
                if (Screen != FlowScreen.Workbench) return false;
                if (Exec(Run.EndDay())) { Settlement = Last.Settlement; Go(FlowScreen.Settlement); }
                return true;
            }

            public bool Acknowledge()
            {
                if (Screen == FlowScreen.Notice) { Selected = null; Go(FlowScreen.Workbench); return true; }
                if (Screen == FlowScreen.Settlement) { bool end = Settlement.RunEnded; Settlement = null; Go(end ? FlowScreen.Result : FlowScreen.Workbench); return true; }
                return false;
            }

            public bool Restart() { if (Screen != FlowScreen.Result) return false; Exec(Run.NewRun()); Selected = null; Settlement = null; Go(FlowScreen.Workbench); return true; }

            public bool Keys(FlowKeys k)
            {
                FlowKeys top = FlowKeys.None;
                foreach (var p in Priority) if ((k & p) != 0) { top = p; break; }
                switch (top)
                {
                    case FlowKeys.Escape:
                        if (Screen == FlowScreen.Folding) return RequestAbandon();
                        if (Screen == FlowScreen.AbandonConfirm) return CancelAbandon();
                        if (Screen == FlowScreen.Preview) return ClosePreview();
                        return false;
                    case FlowKeys.Enter:
                        if (Screen == FlowScreen.AbandonConfirm) return ConfirmAbandon();
                        if (Screen == FlowScreen.Preview) return StartSelected();
                        if (Screen == FlowScreen.Notice || Screen == FlowScreen.Settlement) return Acknowledge();
                        return false;
                    case FlowKeys.Hint: return Hint();
                    case FlowKeys.FoldUp: return Fold(FoldAction.U);
                    case FlowKeys.FoldDown: return Fold(FoldAction.D);
                    case FlowKeys.FoldLeft: return Fold(FoldAction.L);
                    case FlowKeys.FoldRight: return Fold(FoldAction.R);
                    case FlowKeys.Flip: return Fold(FoldAction.F);
                    default: return false;
                }
            }
        }

        private static FlowKeys KeyFor(FoldAction a)
        {
            switch (a)
            {
                case FoldAction.U: return FlowKeys.FoldUp;
                case FoldAction.D: return FlowKeys.FoldDown;
                case FoldAction.L: return FlowKeys.FoldLeft;
                case FoldAction.R: return FlowKeys.FoldRight;
                default: return FlowKeys.Flip;
            }
        }

        private static bool IsGlyph(char c) => c == '↑' || c == '↓' || c == '←' || c == '→' || c == 'F';

        private static string GlyphsIn(IReadOnlyList<string> lines)
        {
            var sb = new StringBuilder();
            foreach (var l in lines) foreach (var c in l) if (IsGlyph(c)) sb.Append(c);
            return sb.ToString();
        }

        private static string GlyphsOf(IReadOnlyList<FoldAction> steps, int count)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < count; i++) sb.Append(FlowTexts.ActionGlyph(steps[i]));
            return sb.ToString();
        }

        private static string Snap(RunSnapshot s)
        {
            var f = s.Fold;
            var sb = new StringBuilder();
            sb.Append(s.Day).Append('|').Append(s.Money).Append('|').Append(s.AttemptsLeft).Append('|').Append(s.PopularModelId)
              .Append('|').Append(string.Join(",", s.UnlockedModelIds)).Append('|').Append(s.FailureCount).Append('|')
              .Append(s.TotalSales).Append('|').Append(s.TotalPaperCost).Append('|').Append(s.EndReason).Append('|')
              .Append(s.EndingId ?? "null").Append('|');
            foreach (var p in s.TodayPieces) sb.Append(p).Append(',');
            foreach (var m in OrigamiCatalog.Models)
                for (int g = 1; g <= 3; g++) sb.Append(s.Completions.Get(m.Id, (Grade)g)).Append('.');
            sb.Append('|').Append(f == null ? "none" : f.ModelId + " " + string.Join("", f.CorrectSteps) + "/" + f.StepCount
                + " m" + f.Mistakes + " h" + f.Hints + " r" + f.RevealedAction + " p" + f.Penalty + " g" + f.ExpectedGrade);
            return sb.ToString();
        }

        private static string Settle(SettlementResult s)
        {
            if (s == null) return "null";
            var sb = new StringBuilder();
            sb.Append(s.SettledDay).Append('|').Append(s.Total).Append('|').Append(s.MoneyAfter).Append('|').Append(s.EndReason)
              .Append('|').Append(s.NextPopularModelId ?? "null").Append('|');
            foreach (var l in s.Lines) sb.Append(l.ModelId).Append(l.Grade).Append(l.Popular).Append(l.Price).Append(',');
            return sb.ToString();
        }

        private static string Result(CommandResult r) =>
            r + "|" + r.Piece + "|" + Settle(r.Settlement);

        private sealed class Coverage
        {
            public readonly HashSet<FlowScreen> Screens = new HashSet<FlowScreen>();
            public readonly HashSet<string> Messages = new HashSet<string>();
            public readonly HashSet<string> Endings = new HashSet<string>();
            public int Abandons, Completions, Ruins, Hints, Restarts, Bankrupt, FullRuns, KeyAbandonBlockedByEsc;
        }

        private static void Check(string ctx, OrigamiFlow flow, FlowReference rf, bool got, bool expected)
        {
            Assert.AreEqual(expected, got, ctx + " return");
            Assert.AreEqual(rf.Screen, flow.Screen, ctx + " screen");
            Assert.AreEqual(rf.Selected, flow.SelectedModelId, ctx + " selected");
            Assert.AreEqual(rf.Message, flow.Message, ctx + " message");
            Assert.AreEqual(Result(rf.Last), Result(flow.LastResult), ctx + " last result");
            Assert.AreEqual(Settle(rf.Settlement), Settle(flow.Settlement), ctx + " settlement");
            var s = flow.Snapshot;
            Assert.AreEqual(Snap(rf.Run.GetSnapshot()), Snap(s), ctx + " snapshot");

            // Structural invariants.
            Assert.IsTrue(Enum.IsDefined(typeof(FlowScreen), flow.Screen), ctx + " defined screen");
            bool foldScreen = flow.Screen == FlowScreen.Folding || flow.Screen == FlowScreen.AbandonConfirm;
            Assert.AreEqual(foldScreen, s.IsFolding, ctx + " folding <=> fold screen");
            Assert.AreEqual(flow.Screen == FlowScreen.Settlement, flow.Settlement != null, ctx + " settlement <=> screen");
            bool selScreen = foldScreen || flow.Screen == FlowScreen.Preview || flow.Screen == FlowScreen.Notice;
            Assert.AreEqual(selScreen, flow.SelectedModelId != null, ctx + " selection <=> screen");
            if (foldScreen) Assert.AreEqual(flow.SelectedModelId, s.Fold.ModelId, ctx + " selected == folded");
            if (flow.SelectedModelId != null) Assert.IsTrue(s.IsUnlocked(flow.SelectedModelId), ctx + " selected unlocked");
            if (s.IsRunEnded) Assert.IsTrue(flow.Screen == FlowScreen.Result || flow.Screen == FlowScreen.Settlement, ctx + " ended run screen");
            else Assert.AreNotEqual(FlowScreen.Result, flow.Screen, ctx + " result only when ended");
            if (flow.Screen == FlowScreen.Notice || flow.Screen == FlowScreen.Settlement || flow.Screen == FlowScreen.Result)
                Assert.AreEqual("", flow.Message, ctx + " no message on " + flow.Screen);

            // Lines never reveal remaining fold steps.
            var lines = FlowTexts.ScreenLines(flow);
            string shown = GlyphsIn(lines);
            if (foldScreen)
            {
                var f = s.Fold;
                var steps = OrigamiCatalog.FindModel(f.ModelId).Steps;
                string expect = GlyphsOf(steps, f.Progress);
                if (f.IsCurrentStepRevealed)
                {
                    Assert.AreEqual(steps[f.Progress], f.RevealedAction, ctx + " hint = current step");
                    expect += FlowTexts.ActionGlyph(steps[f.Progress]);
                }
                Assert.AreEqual(expect, shown, ctx + " fold glyphs (only done steps + hinted current)\n" + string.Join("\n", lines));
                int hintLines = 0;
                foreach (var l in lines) if (l.StartsWith("힌트: ", StringComparison.Ordinal)) hintLines++;
                Assert.AreEqual(f.IsCurrentStepRevealed ? 1 : 0, hintLines, ctx + " hint line only when revealed");
                if (f.IsCurrentStepRevealed)
                    CollectionAssert.Contains(lines, "힌트: " + FlowTexts.ActionGlyph(steps[f.Progress]) + " " + FlowTexts.ActionName(steps[f.Progress]), ctx);
                CollectionAssert.Contains(lines, "진행 " + f.Progress + "/" + f.StepCount, ctx);
                CollectionAssert.Contains(lines, "실수 " + f.Mistakes + "/3", ctx);
            }
            else if (flow.Screen == FlowScreen.Preview)
            {
                var m = OrigamiCatalog.FindModel(flow.SelectedModelId);
                Assert.AreEqual(GlyphsOf(m.Steps, m.StepCount), shown, ctx + " preview glyphs");
            }
            else
            {
                Assert.AreEqual("", shown, ctx + " no glyphs on " + flow.Screen + "\n" + string.Join("\n", lines));
            }
            if (flow.Screen == FlowScreen.AbandonConfirm)
                Assert.AreEqual("정말 포기할까요? (Enter: 포기한다 / Esc: 계속 접기)", lines[lines.Count - 1], ctx);
            if (rf.Message.Length > 0) Assert.AreEqual(rf.Message, lines[lines.Count - 1 - (flow.Screen == FlowScreen.AbandonConfirm ? 1 : 0)], ctx + " message line");
        }

        private static void RunStream(int seed, Coverage cov)
        {
            var rng = new Rng(seed);
            var flow = new OrigamiFlow();
            var rf = new FlowReference();
            Check("seed " + seed + " init", flow, rf, true, true);
            // Per-stream play style, so some streams play well (unlocks, long runs) and others are chaotic.
            int smartPct = 20 + rng.Below(75);
            int endDayPct = 3 + rng.Below(20);
            int wastePct = seed % 3 == 0 ? 70 + rng.Below(28) : 0; // "spendthrift" streams: abandon a lot, run out of money mid-day
            int frames = wastePct > 0 ? 3 * FramesPerStream : FramesPerStream;
            for (int frame = 0; frame < frames; frame++)
            {
                var before = flow.Screen;
                int failsBefore = flow.Snapshot.FailureCount;
                bool runEndedBefore = flow.Snapshot.IsRunEnded;
                string ctx = "seed " + seed + " frame " + frame + " on " + before + ": ";
                bool got, exp;
                FlowKeys keys = FlowKeys.None;
                bool viaKeys = rng.Chance(65);
                if (viaKeys)
                {
                    int mode = rng.Below(100);
                    if (mode < smartPct)
                    {
                        switch (flow.Screen)
                        {
                            case FlowScreen.Folding:
                                var f = flow.Snapshot.Fold;
                                var steps = OrigamiCatalog.FindModel(f.ModelId).Steps;
                                int r = rng.Below(100);
                                keys = rng.Chance(wastePct) ? FlowKeys.Escape : r < 80 ? KeyFor(steps[f.Progress]) : r < 88 ? FlowKeys.Hint : r < 94 ? FlowKeys.Escape : KeyFor((FoldAction)(1 + rng.Below(5)));
                                break;
                            case FlowScreen.AbandonConfirm:
                                keys = rng.Chance(wastePct > 0 ? 90 : 50) ? FlowKeys.Enter : FlowKeys.Escape;
                                break;
                            default:
                                keys = rng.Chance(85) ? FlowKeys.Enter : FlowKeys.Escape;
                                break;
                        }
                        if (rng.Chance(15)) keys |= (FlowKeys)(rng.Below(256)) & ~(FlowKeys.Escape | FlowKeys.Enter);
                    }
                    else if (mode < smartPct + (100 - smartPct) / 2)
                    {
                        keys = Priority[rng.Below(Priority.Length)];
                    }
                    else
                    {
                        keys = (FlowKeys)rng.Below(256);
                    }
                    ctx += "keys " + keys;
                    got = flow.ProcessKeys(keys);
                    exp = rf.Keys(keys);
                }
                else
                {
                    int b = rng.Below(100);
                    if (flow.Screen == FlowScreen.Workbench && (b < 70 || wastePct > 0))
                    {
                        bool spendthriftEnd = wastePct > 0 && (flow.Snapshot.AttemptsLeft == 0 || rng.Chance(3));
                        if (spendthriftEnd || (wastePct == 0 && rng.Below(100) < endDayPct)) { ctx += "EndDay"; got = flow.EndDay(); exp = rf.EndDay(); }
                        else
                        {
                            string id = rng.Chance(80) ? SelectIds[rng.Below(5)] : SelectIds[5 + rng.Below(4)];
                            ctx += "SelectModel(" + (id ?? "null") + ")";
                            got = flow.SelectModel(id); exp = rf.SelectModel(id);
                        }
                    }
                    else if (flow.Screen == FlowScreen.Result && b < 60)
                    {
                        ctx += "Restart"; got = flow.Restart(); exp = rf.Restart();
                    }
                    else
                    {
                        switch (rng.Below(13))
                        {
                            case 0: { string id = SelectIds[rng.Below(SelectIds.Length)]; ctx += "SelectModel(" + (id ?? "null") + ")"; got = flow.SelectModel(id); exp = rf.SelectModel(id); break; }
                            case 1: ctx += "ClosePreview"; got = flow.ClosePreview(); exp = rf.ClosePreview(); break;
                            case 2: ctx += "StartSelected"; got = flow.StartSelected(); exp = rf.StartSelected(); break;
                            case 3: { var a = (FoldAction)rng.Below(7); ctx += "Fold(" + a + ")"; got = flow.Fold(a); exp = rf.Fold(a); break; }
                            case 4: ctx += "Hint"; got = flow.Hint(); exp = rf.Hint(); break;
                            case 5: ctx += "RequestAbandon"; got = flow.RequestAbandon(); exp = rf.RequestAbandon(); break;
                            case 6: ctx += "ConfirmAbandon"; got = flow.ConfirmAbandon(); exp = rf.ConfirmAbandon(); break;
                            case 7: ctx += "CancelAbandon"; got = flow.CancelAbandon(); exp = rf.CancelAbandon(); break;
                            case 8: ctx += "EndDay"; got = flow.EndDay(); exp = rf.EndDay(); break;
                            case 9: case 10: ctx += "Acknowledge"; got = flow.Acknowledge(); exp = rf.Acknowledge(); break;
                            case 11: ctx += "Restart"; got = flow.Restart(); exp = rf.Restart(); break;
                            default: { var k = (FlowKeys)rng.Below(256); ctx += "keys " + k; keys = k; viaKeys = true; got = flow.ProcessKeys(k); exp = rf.Keys(k); break; }
                        }
                    }
                }
                Check(ctx, flow, rf, got, exp);

                // Abandon never happens without a confirm frame on AbandonConfirm.
                bool abandoned = flow.LastResult.Outcome == CommandOutcome.FoldAbandoned && flow.Snapshot.FailureCount == failsBefore + 1
                    && flow.Screen == FlowScreen.Notice && before != FlowScreen.Notice;
                if (abandoned)
                {
                    Assert.AreEqual(FlowScreen.AbandonConfirm, before, ctx + " abandon only from AbandonConfirm");
                    if (viaKeys)
                    {
                        Assert.AreEqual(0, (int)(keys & FlowKeys.Escape), ctx + " abandon frame has no Esc");
                        Assert.AreNotEqual(0, (int)(keys & FlowKeys.Enter), ctx + " abandon frame has Enter");
                    }
                    cov.Abandons++;
                }
                if (viaKeys && before == FlowScreen.Folding && (keys & FlowKeys.Escape) != 0 && (keys & FlowKeys.Enter) != 0)
                {
                    Assert.AreEqual(FlowScreen.AbandonConfirm, flow.Screen, ctx + " Esc+Enter on Folding opens confirm only");
                    Assert.AreEqual(failsBefore, flow.Snapshot.FailureCount, ctx);
                    cov.KeyAbandonBlockedByEsc++;
                }

                cov.Screens.Add(flow.Screen);
                if (flow.Message.Length > 0) cov.Messages.Add(flow.Message.Contains("완성 시") ? "locked" : flow.Message);
                if (before != flow.Screen || got)
                {
                    if (flow.LastResult.Outcome == CommandOutcome.FoldCompleted && flow.Screen == FlowScreen.Notice && before == FlowScreen.Folding) cov.Completions++;
                    if (flow.LastResult.Outcome == CommandOutcome.FoldRuined && flow.Screen == FlowScreen.Notice && before == FlowScreen.Folding) cov.Ruins++;
                    if (flow.LastResult.Outcome == CommandOutcome.HintRevealed) cov.Hints++;
                }
                if (before == FlowScreen.Result && flow.Screen == FlowScreen.Workbench) cov.Restarts++;
                if (!runEndedBefore && flow.Snapshot.IsRunEnded)
                {
                    cov.Endings.Add(flow.Snapshot.EndingId);
                    if (flow.Snapshot.EndReason == RunEndReason.Bankrupt) cov.Bankrupt++; else cov.FullRuns++;
                    Assert.AreEqual(EndingRules.Evaluate(flow.Snapshot.EndReason, flow.Snapshot.Money, flow.Snapshot.Completions),
                        flow.Snapshot.EndingId, ctx + " ending");
                }
                if (flow.Screen == FlowScreen.Result)
                {
                    var lines = FlowTexts.ScreenLines(flow);
                    var e = OrigamiCatalog.FindEnding(EndingRules.Evaluate(flow.Snapshot.EndReason, flow.Snapshot.Money, flow.Snapshot.Completions));
                    Assert.AreEqual(e.DisplayName, lines[0], ctx + " result title");
                    Assert.AreEqual(e.Text, lines[1], ctx + " result text");
                }
            }
        }

        [Test]
        public void SeededStreams_MatchReferenceFlow_AndHoldInvariantsEveryFrame()
        {
            var cov = new Coverage();
            for (int seed = 1; seed <= Streams; seed++) RunStream(seed, cov);

            // Coverage gate: the streams really exercised what the invariants talk about.
            foreach (FlowScreen sc in Enum.GetValues(typeof(FlowScreen))) Assert.IsTrue(cov.Screens.Contains(sc), "screen " + sc + " reached");
            foreach (var m in new[] { "locked", "없는 작품입니다", "오늘은 더 접을 수 없습니다", "돈이 부족합니다", "알 수 없는 동작입니다" })
                Assert.IsTrue(cov.Messages.Contains(m), "message '" + m + "' seen; seen=" + string.Join(",", cov.Messages));
            Assert.Greater(cov.Abandons, 50, "abandons");
            Assert.Greater(cov.Completions, 200, "completions");
            Assert.Greater(cov.Ruins, 20, "ruins");
            Assert.Greater(cov.Hints, 50, "hints");
            Assert.Greater(cov.Restarts, 10, "restarts");
            Assert.Greater(cov.Bankrupt, 0, "bankrupt runs");
            Assert.Greater(cov.FullRuns, 0, "7-day runs");
            Assert.Greater(cov.KeyAbandonBlockedByEsc, 0, "Esc+Enter frames on Folding");
            Assert.IsTrue(cov.Endings.Contains(EndingIds.EmptyHanded), "E-01 reached by fuzz");
            Assert.GreaterOrEqual(cov.Endings.Count, 2, "endings reached by fuzz: " + string.Join(",", cov.Endings));
        }
    }
}
