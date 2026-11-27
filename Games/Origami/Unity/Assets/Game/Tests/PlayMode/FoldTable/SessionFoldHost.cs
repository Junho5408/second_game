using System.Collections.Generic;
using Origami.Core.Folding;
using Origami.Core.Geometry;
using Origami.Runtime.Folding;

namespace Origami.Tests.PlayMode.FoldTableTests
{
    /// <summary>
    /// Test-only <see cref="IFoldTableHost"/> over a <see cref="FoldSession"/> (C-191: "012 PlayMode 테스트는 FoldSession을 감싼
    /// 테스트 전용 host"). Records every Fold/Crease call; changes made by the test itself (flip, undo, step lists) bump
    /// the serial like the game's <c>FoldHostAdapter</c> would.
    /// </summary>
    internal sealed class SessionFoldHost : IFoldTableHost
    {
        public readonly FoldSession Session = new FoldSession();
        public bool HasSheet = true;
        public bool Enabled = true;
        public bool GuideVisible;
        public FoldTableGuide Guide;

        public int FoldCalls, CreaseCalls;
        public Vec2 LastGrab, LastDrop;
        public bool LastMountain;
        public int LastLayers = -99;

        private int _serial;
        private FoldTableChangeKind _kind;
        private PaperState _previous;

        public PaperState Sheet => HasSheet ? Session.State : null;
        public bool InputEnabled => Enabled;
        public int ChangeSerial => _serial;
        public FoldTableChangeKind ChangeKind => _kind;
        public PaperState ChangePrevious => _previous;

        public bool TryGetGuide(out FoldTableGuide guide)
        {
            guide = Guide;
            return GuideVisible && HasSheet;
        }

        public bool Fold(Vec2 grab, Vec2 drop, bool mountain, int layers, out FoldError error)
        {
            FoldCalls++;
            Record(grab, drop, mountain, layers);
            var before = Session.State;
            var plan = Session.Fold(grab, drop, mountain, layers);
            error = plan.Error;
            if (!plan.Valid) return false;
            Change(FoldTableChangeKind.Folded, before);
            return true;
        }

        public bool Crease(Vec2 grab, Vec2 drop, int layers, out FoldError error)
        {
            CreaseCalls++;
            Record(grab, drop, false, layers);
            var before = Session.State;
            var plan = Session.Crease(grab, drop, layers);
            error = plan.Error;
            if (!plan.Valid) return false;
            Change(FoldTableChangeKind.Creased, before);
            return true;
        }

        /// <summary>F key in the game: flip, reported as Flipped.</summary>
        public void Flip()
        {
            var before = Session.State;
            Session.Flip();
            Change(FoldTableChangeKind.Flipped, before);
        }

        /// <summary>Undo in the game: reported as Replaced.</summary>
        public void Undo()
        {
            var before = Session.State;
            if (Session.Undo()) Change(FoldTableChangeKind.Replaced, before);
        }

        /// <summary>A fold made elsewhere (not by the table), reported as Folded.</summary>
        public void ForeignFold(Vec2 grab, Vec2 drop)
        {
            var before = Session.State;
            if (Session.Fold(grab, drop, false, 0).Valid) Change(FoldTableChangeKind.Folded, before);
        }

        /// <summary>Applies reference steps directly (setup), reported as one Replaced change.</summary>
        public void ApplySteps(IEnumerable<FoldStep> steps)
        {
            var before = Session.State;
            foreach (var s in steps)
            {
                switch (s.Kind)
                {
                    case FoldStepKind.Fold: Session.Fold(s.Grab, s.Drop, s.Mountain, s.Layers); break;
                    case FoldStepKind.Crease: Session.Crease(s.Grab, s.Drop, s.Layers); break;
                    default: Session.Flip(); break;
                }
            }
            Change(FoldTableChangeKind.Replaced, before);
        }

        /// <summary>Finish/Discard in the game: the sheet leaves the table.</summary>
        public void RemoveSheet()
        {
            var before = Sheet;
            HasSheet = false;
            Change(FoldTableChangeKind.Replaced, before);
        }

        private void Record(Vec2 grab, Vec2 drop, bool mountain, int layers)
        {
            LastGrab = grab;
            LastDrop = drop;
            LastMountain = mountain;
            LastLayers = layers;
        }

        private void Change(FoldTableChangeKind kind, PaperState previous)
        {
            _serial++;
            _kind = kind;
            _previous = previous;
        }
    }
}
