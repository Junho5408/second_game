using System;
using Origami.Core.Folding;
using Origami.Core.Game;
using Origami.Core.Geometry;
using Origami.Core.Screens;
using Origami.Runtime.Folding;

namespace Origami.Runtime.Gameplay
{
    /// <summary>
    /// <see cref="IFoldTableHost"/> over the game's <see cref="ScreenFlow"/> (C-193, A2-7). All values are read live from the flow.
    /// Sheet changes: Folded/Creased/Flipped keep their kind, every other <see cref="SheetChangeKind"/> becomes Replaced (C-171).
    /// </summary>
    public sealed class FoldHostAdapter : IFoldTableHost
    {
        private readonly ScreenFlow _flow;

        /// <exception cref="ArgumentNullException">flow is null.</exception>
        public FoldHostAdapter(ScreenFlow flow)
        {
            _flow = flow ?? throw new ArgumentNullException(nameof(flow));
        }

        /// <summary>The table sheet, null if none.</summary>
        public PaperState Sheet => _flow.Snapshot.Sheet?.State;

        /// <summary>Mode Seated ∧ no overlay.</summary>
        public bool InputEnabled => _flow.Snapshot.Mode == WorkshopMode.Seated && _flow.Overlay == Overlay.None;

        /// <summary>The tutorial guide step while <c>Sheet.GuideVisible</c>.</summary>
        public bool TryGetGuide(out FoldTableGuide guide)
        {
            var sheet = _flow.Snapshot.Sheet;
            if (sheet == null || !sheet.GuideVisible || sheet.Guide == null)
            {
                guide = default;
                return false;
            }
            var g = sheet.Guide;
            guide = new FoldTableGuide(g.Grab, g.Drop, g.LineA, g.LineB, g.Kind == FoldStepKind.Crease);
            return true;
        }

        public int ChangeSerial => _flow.LastSheetChange.Serial;

        public FoldTableChangeKind ChangeKind
        {
            get
            {
                switch (_flow.LastSheetChange.Kind)
                {
                    case SheetChangeKind.None: return FoldTableChangeKind.None;
                    case SheetChangeKind.Folded: return FoldTableChangeKind.Folded;
                    case SheetChangeKind.Creased: return FoldTableChangeKind.Creased;
                    case SheetChangeKind.Flipped: return FoldTableChangeKind.Flipped;
                    default: return FoldTableChangeKind.Replaced;
                }
            }
        }

        public PaperState ChangePrevious => _flow.LastSheetChange.Previous;

        /// <summary><c>ScreenFlow.Fold</c>; error = the result's fold error (None for rule rejections, which the flow reports itself).</summary>
        public bool Fold(Vec2 grab, Vec2 drop, bool mountain, int layers, out FoldError error)
        {
            var r = _flow.Fold(grab, drop, mountain, layers);
            error = r.FoldError;
            return r.Accepted;
        }

        /// <summary><c>ScreenFlow.Crease</c>, same conventions as <see cref="Fold"/>.</summary>
        public bool Crease(Vec2 grab, Vec2 drop, int layers, out FoldError error)
        {
            var r = _flow.Crease(grab, drop, layers);
            error = r.FoldError;
            return r.Accepted;
        }
    }
}
