using NUnit.Framework;
using Origami.Core.Folding;
using Origami.Core.Geometry;
using Origami.Runtime.Folding;

namespace Origami.Tests.PlayMode.FoldTableTests
{
    /// <summary>
    /// ORIGAMI-012: host-driven behaviour of the folding table — guide markers (F-006 AC-09), changes made outside the
    /// table (flip animation, immediate redraw), crease lines, no sheet, unbinding.
    /// </summary>
    public class FoldTableHostTests
    {
        private TableRig _rig;

        [SetUp]
        public void SetUp() => _rig = new TableRig();

        [TearDown]
        public void TearDown()
        {
            _rig.Destroy();
            _rig = null;
        }

        private void ShowPlaneStep1Guide()
        {
            _rig.Host.Guide = new FoldTableGuide(new Vec2(-1, 1), new Vec2(1, 1), new Vec2(0, 1), new Vec2(0, -1), true);
            _rig.Host.GuideVisible = true;
        }

        [Test]
        public void F006_AC09_GuideMarkersRenderOnlyWhileTheHostHasAGuide()
        {
            var t = _rig.Table;
            _rig.Frame(new PointerFrame());
            Assert.IsFalse(t.GuideMarkersVisible, "no guide → no markers");
            Assert.IsFalse(_rig.ChildActive("PaperSpace/GuideBall"));

            ShowPlaneStep1Guide();
            _rig.Frame(new PointerFrame());
            Assert.IsTrue(t.GuideMarkersVisible, "tutorial sheet: yellow ball, green ring, red dashes");
            Assert.IsTrue(_rig.ChildActive("PaperSpace/GuideBall"));

            // Second sheet: the host no longer gives a guide.
            _rig.Host.RemoveSheet();
            _rig.Frame(new PointerFrame());
            _rig.Host.HasSheet = true;
            _rig.Host.GuideVisible = false;
            _rig.Host.ApplySteps(new FoldStep[0]);   // a new sheet arrives (Replaced) without a guide
            _rig.Frame(new PointerFrame());
            Assert.IsFalse(t.GuideMarkersVisible, "second sheet: no markers");
            Assert.IsFalse(_rig.ChildActive("PaperSpace/GuideBall"));
        }

        [Test]
        public void F006_AC09_NoSheet_NoMarkersEvenIfGuideFlagSet()
        {
            ShowPlaneStep1Guide();
            _rig.Host.RemoveSheet();
            _rig.Frame(new PointerFrame());
            Assert.IsFalse(_rig.Table.GuideMarkersVisible);
            Assert.AreEqual(0, _rig.Table.PaperVertexCount, "no sheet → nothing drawn");
        }

        [Test]
        public void GuideDrop_IsTheFirstDropMagnet()
        {
            ShowPlaneStep1Guide();
            _rig.Press(-1f, 1f);
            _rig.Move(0.95f, 0.96f);
            Assert.AreEqual(SnapKind.Guide, _rig.Table.CurrentSnap);
            Assert.AreEqual(new Vec2(1, 1), _rig.Table.Gesture.Drop.Point);
        }

        [Test]
        public void GuideMarkers_HiddenWhileAnimating()
        {
            ShowPlaneStep1Guide();
            _rig.Press(-1f, 1f);
            _rig.Move(1f, 1f, ctrl: true);
            _rig.Release(1f, 1f, ctrl: true);
            Assert.IsTrue(_rig.Table.IsAnimating);
            Assert.IsFalse(_rig.Table.GuideMarkersVisible);
            _rig.Table.FinishAnimationNow();
            Assert.IsTrue(_rig.Table.GuideMarkersVisible);
        }

        [Test]
        public void HostFlip_AnimatesFromThePreviousState()
        {
            var t = _rig.Table;
            _rig.Host.Flip();
            _rig.Frame(new PointerFrame(), 0f);
            Assert.IsTrue(t.IsAnimating, "Flipped → 0.6 s flip animation");
            Assert.IsTrue(_rig.ChildActive("PaperSpace/Paper/Flap"));
            _rig.Frame(new PointerFrame(), 0.5f);
            Assert.IsTrue(t.IsAnimating);
            _rig.Frame(new PointerFrame(), 0.2f);
            Assert.IsFalse(t.IsAnimating);
        }

        [Test]
        public void HostUndo_RedrawsImmediately()
        {
            var t = _rig.Table;
            int flat = t.PaperVertexCount;
            _rig.Press(-1f, 1f);
            _rig.Move(-1f, -1f);
            _rig.Release(-1f, -1f);
            t.FinishAnimationNow();
            Assert.AreNotEqual(flat, t.PaperVertexCount);

            _rig.Host.Undo();
            _rig.Frame(new PointerFrame());
            Assert.IsFalse(t.IsAnimating, "Replaced → no animation");
            Assert.AreEqual(flat, t.PaperVertexCount);
        }

        [Test]
        public void FoldNotMadeByTheTable_RedrawsImmediately()
        {
            var t = _rig.Table;
            int flat = t.PaperVertexCount;
            _rig.Host.ForeignFold(new Vec2(-1, 1), new Vec2(-1, -1));
            _rig.Frame(new PointerFrame());
            Assert.IsFalse(t.IsAnimating);
            Assert.AreNotEqual(flat, t.PaperVertexCount);
        }

        [Test]
        public void HostChangeDuringAnimation_EndsItAndShowsTheNewState()
        {
            var t = _rig.Table;
            int flat = t.PaperVertexCount;
            _rig.Press(-1f, 1f);
            _rig.Move(-1f, -1f);
            _rig.Release(-1f, -1f);
            Assert.IsTrue(t.IsAnimating);
            _rig.Host.Undo();
            _rig.Frame(new PointerFrame());
            Assert.IsFalse(t.IsAnimating);
            Assert.AreEqual(flat, t.PaperVertexCount);
        }

        [Test]
        public void F005_CreaseLines_OnlyVisibleParts()
        {
            // F-005 AC-11 state: two crease segments, the one on the lower layer is covered by the folded flap.
            _rig.Host.ApplySteps(new[]
            {
                FoldStep.Fold(new Vec2(-1, 1), new Vec2(-1, -1)),
                FoldStep.Crease(new Vec2(-1, 0), new Vec2(1, 0)),
            });
            _rig.Frame(new PointerFrame());
            Assert.AreEqual(2, _rig.Host.Session.State.Creases.Count);
            Assert.AreEqual(2, DropSnap.MarkPieces(_rig.Host.Session.State).Count);
            Assert.AreEqual(1, _rig.Table.CreaseLineCount, "the covered mark piece is not drawn");

            _rig.Host.Flip();
            _rig.Table.FinishAnimationNow();
            Assert.AreEqual(1, _rig.Table.CreaseLineCount, "marks move with the paper");
        }

        [Test]
        public void BindNull_DisconnectsAndDrawsNothing()
        {
            var t = _rig.Table;
            _rig.Press(-1f, 1f);
            t.Bind(null);
            Assert.IsFalse(t.IsDragging);
            Assert.AreEqual(0, t.PaperVertexCount);
            _rig.Frame(new PointerFrame { HasPaperPoint = true, LeftDown = true, LeftHeld = true });
            Assert.IsFalse(t.IsDragging);
            Assert.IsFalse(t.GuideMarkersVisible);

            t.Bind(_rig.Host);
            Assert.Greater(t.PaperVertexCount, 0);
        }

        [Test]
        public void InvalidDt_IsTreatedAsZero()
        {
            var t = _rig.Table;
            _rig.Host.Flip();
            _rig.Frame(new PointerFrame(), float.NaN);
            Assert.IsTrue(t.IsAnimating);
            _rig.Frame(new PointerFrame(), -1f);
            Assert.IsTrue(t.IsAnimating);
            _rig.Frame(new PointerFrame(), 1f);
            Assert.IsFalse(t.IsAnimating);
        }
    }
}
