using NUnit.Framework;
using Origami.Core.Folding;
using Origami.Core.Geometry;
using Origami.Runtime.Folding;

namespace Origami.Tests.PlayMode.FoldTableTests
{
    /// <summary>
    /// ORIGAMI-012: drag input of the folding table (F-005 AC-17, AC-18, review N-3). Every test calls
    /// <see cref="FoldTable.ProcessFrame"/> directly with a fixed dt and hand-made <see cref="PointerFrame"/>s.
    /// </summary>
    public class FoldTableDragTests
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

        [Test]
        public void F005_AC17_CornerGrab_Preview_Release_AnimatesThenFoldCountIsOne()
        {
            var t = _rig.Table;
            int before = t.PaperVertexCount;
            Assert.Greater(before, 0, "the initial sheet is drawn");

            _rig.Press(-0.9f, 0.9f);   // 0.141 from the corner (-1, 1) < 0.16
            Assert.IsTrue(t.IsDragging);
            Assert.IsNotNull(t.Gesture);
            Assert.IsTrue(t.Gesture.GrabbedCorner);
            Assert.AreEqual(new Vec2(-1, 1), t.Gesture.Grab);
            Assert.AreEqual("겹: 전체", t.CursorLabel);

            _rig.Move(-1f, -0.5f);
            _rig.Move(-1f, -1f);
            Assert.AreEqual(SnapKind.Point, t.CurrentSnap, "the opposite corner is a landmark");
            Assert.IsTrue(_rig.ChildActive("PaperSpace/Ghost"), "ghost preview of the result");
            Assert.IsTrue(_rig.ChildActive("PaperSpace/Paper/Flap"), "moving facets are lifted");
            Assert.AreEqual(0, _rig.Host.FoldCalls, "nothing is applied while dragging");
            Assert.AreEqual(0, _rig.Host.Session.FoldCount);

            _rig.Release(-1f, -1f);
            Assert.IsFalse(t.IsDragging);
            Assert.AreEqual(1, _rig.Host.FoldCalls);
            Assert.AreEqual(new Vec2(-1, 1), _rig.Host.LastGrab);
            Assert.AreEqual(new Vec2(-1, -1), _rig.Host.LastDrop);
            Assert.IsFalse(_rig.Host.LastMountain);
            Assert.AreEqual(0, _rig.Host.LastLayers);
            Assert.AreEqual(1, _rig.Host.Session.FoldCount, "Core is applied on release (A2-4)");
            Assert.IsTrue(t.IsAnimating, "then the fold animates");
            Assert.AreEqual(FoldError.None, t.LastError);

            _rig.Frame(new PointerFrame(), 0.2f);
            Assert.IsTrue(t.IsAnimating, "0.2 s < FoldDuration 0.4 s");
            _rig.Frame(new PointerFrame(), 0.25f);
            Assert.IsFalse(t.IsAnimating);
            Assert.IsFalse(_rig.ChildActive("PaperSpace/Paper/Flap"));
            Assert.IsFalse(_rig.ChildActive("PaperSpace/Ghost"));
            Assert.AreEqual(1, _rig.Host.Session.FoldCount);
            Assert.AreNotEqual(before, t.PaperVertexCount, "the static mesh shows the folded sheet");
        }

        [Test]
        public void F005_AC17_EscapeWhileDragging_CancelsWithoutHostCall()
        {
            var t = _rig.Table;
            _rig.Press(-1f, 1f);
            _rig.Move(-1f, -1f);
            Assert.IsTrue(t.IsDragging);

            var esc = TableRig.At(-1f, -1f);
            esc.LeftHeld = true;
            esc.Escape = true;
            _rig.Frame(esc);
            Assert.IsFalse(t.IsDragging);
            Assert.AreEqual("", t.CursorLabel);
            Assert.AreEqual(SnapKind.None, t.CurrentSnap);

            _rig.Release(-1f, -1f);
            Assert.AreEqual(0, _rig.Host.FoldCalls);
            Assert.AreEqual(0, _rig.Host.Session.FoldCount);
            Assert.IsFalse(t.IsAnimating);
        }

        [Test]
        public void F005_AC18_DiamondBase_TwoWheelNotches_ShowTop2Of6_AndFoldWithLayers2()
        {
            _rig.DiamondBase();
            Assert.AreEqual(6, _rig.Host.Session.State.Facets.Count, "setup: diamond base");
            var t = _rig.Table;

            _rig.Press(-1f, -1f);
            Assert.IsTrue(t.IsDragging);
            Assert.AreEqual(6, t.Gesture.StackCount);

            _rig.Move(-1f, -1f, wheel: 1f);
            Assert.AreEqual("겹: 위 1 / 6", t.CursorLabel);
            _rig.Move(-1f, -1f, wheel: 1f);
            Assert.AreEqual(2, t.Gesture.Layers);
            Assert.AreEqual("겹: 위 2 / 6", t.CursorLabel);

            _rig.Move(0.3f, -0.6f);
            _rig.Move(0.689655f, -0.275862f);
            _rig.Release(0.689655f, -0.275862f);

            Assert.AreEqual(1, _rig.Host.FoldCalls);
            Assert.AreEqual(0, _rig.Host.CreaseCalls);
            Assert.AreEqual(2, _rig.Host.LastLayers, "Core Fold is called with layers 2");
            Assert.Less(Vec2.Distance(new Vec2(-1, -1), _rig.Host.LastGrab), 1e-6);
            Assert.AreEqual(8, _rig.Host.Session.State.Facets.Count, "top-two fold of the diamond base (F-005 AC-12)");
        }

        [Test]
        public void F005_AC18_CtrlRelease_CallsCrease_AndDrawsTheCreaseLine()
        {
            var t = _rig.Table;
            Assert.AreEqual(0, t.CreaseLineCount);

            _rig.Press(-1f, 1f);
            _rig.Move(0f, 1f, ctrl: true);
            _rig.Move(1f, 1f, ctrl: true);
            Assert.AreEqual(FoldTexts.CreaseLabel, t.CursorLabel);
            _rig.Release(1f, 1f, ctrl: true);

            Assert.AreEqual(1, _rig.Host.CreaseCalls);
            Assert.AreEqual(0, _rig.Host.FoldCalls);
            Assert.AreEqual(1, _rig.Host.Session.CreaseCount);
            Assert.AreEqual(0, _rig.Host.Session.FoldCount);
            Assert.IsTrue(t.IsAnimating, "fold-and-unfold animation");

            _rig.Frame(new PointerFrame(), 0.45f);
            Assert.IsTrue(t.IsAnimating, "0.45 s < CreaseDuration 0.6 s");
            t.FinishAnimationNow();
            Assert.IsFalse(t.IsAnimating);
            Assert.AreEqual(1, t.CreaseLineCount, "one crease mark (0,−1)–(0,1) is drawn");
        }

        [Test]
        public void ShiftHeldOnRelease_FoldsMountain()
        {
            _rig.Press(-1f, 1f);
            _rig.Move(-1f, -1f, shift: true);
            _rig.Release(-1f, -1f, shift: true);
            Assert.AreEqual(1, _rig.Host.FoldCalls);
            Assert.IsTrue(_rig.Host.LastMountain);
            Assert.AreEqual(1, _rig.Host.Session.FoldCount);
        }

        [Test]
        public void N3_WheelCountsOnlyBySign_OneLayerPerFrame()
        {
            _rig.DiamondBase();
            var t = _rig.Table;
            _rig.Press(-1f, -1f);

            _rig.Move(-1f, -1f, wheel: 3.7f);
            Assert.AreEqual(1, t.Gesture.Layers, "a big delta is still one notch");
            _rig.Move(-1f, -1f, wheel: -0.2f);
            Assert.AreEqual(0, t.Gesture.Layers, "a small negative delta is one notch back");
            _rig.Move(-1f, -1f, wheel: -1f);
            Assert.AreEqual(5, t.Gesture.Layers, "all → top M−1 backwards (A2-8)");

            var both = TableRig.At(-1f, -1f);
            both.LeftHeld = true;
            both.Wheel = 1f;
            both.LayerUp = true;
            _rig.Frame(both);
            Assert.AreEqual(0, t.Gesture.Layers, "wheel and ] in one frame are still ±1");

            var bracket = TableRig.At(-1f, -1f);
            bracket.LeftHeld = true;
            bracket.LayerUp = true;
            _rig.Frame(bracket);
            Assert.AreEqual(1, t.Gesture.Layers, "] = +1");
            bracket.LayerUp = false;
            bracket.LayerDown = true;
            _rig.Frame(bracket);
            Assert.AreEqual(0, t.Gesture.Layers, "[ = −1");
        }

        [Test]
        public void SingleLayerGrab_WheelIsIgnored_LabelAll()
        {
            var t = _rig.Table;
            _rig.Press(-1f, 1f);
            Assert.AreEqual(1, t.Gesture.StackCount);
            _rig.Move(-1f, 1f, wheel: 1f);
            Assert.AreEqual(0, t.Gesture.Layers);
            Assert.AreEqual("겹: 전체", t.CursorLabel);
        }

        [Test]
        public void TooShortDrag_SetsLastError_WithoutHostCall()
        {
            var t = _rig.Table;
            _rig.Press(-1f, 1f);
            _rig.Release(-0.99f, 0.99f);
            Assert.AreEqual(FoldError.DragTooShort, t.LastError);
            Assert.AreEqual(0, _rig.Host.FoldCalls);
            Assert.IsFalse(t.IsAnimating);

            _rig.Press(-1f, 1f);
            Assert.AreEqual(FoldError.None, t.LastError, "a new drag clears the error");
        }

        [Test]
        public void InputDisabled_IgnoresPointer_AndCancelsARunningDrag()
        {
            var t = _rig.Table;
            _rig.Host.Enabled = false;
            _rig.Press(-1f, 1f);
            Assert.IsFalse(t.IsDragging);

            _rig.Host.Enabled = true;
            _rig.Press(-1f, 1f);
            _rig.Move(-1f, -1f);
            Assert.IsTrue(t.IsDragging);
            _rig.Host.Enabled = false;
            _rig.Move(-1f, -1f);
            Assert.IsFalse(t.IsDragging);
            _rig.Host.Enabled = true;
            _rig.Release(-1f, -1f);
            Assert.AreEqual(0, _rig.Host.FoldCalls);
        }

        [Test]
        public void OverGui_DoesNotStartADrag()
        {
            var p = TableRig.At(-1f, 1f);
            p.LeftDown = true;
            p.LeftHeld = true;
            p.OverGui = true;
            _rig.Frame(p);
            Assert.IsFalse(_rig.Table.IsDragging);
        }

        [Test]
        public void NoNewDragWhileAnimating()
        {
            var t = _rig.Table;
            _rig.Press(-1f, 1f);
            _rig.Move(-1f, -1f);
            _rig.Release(-1f, -1f);
            Assert.IsTrue(t.IsAnimating);

            _rig.Press(1f, -1f);
            Assert.IsFalse(t.IsDragging, "no drag starts during the animation");
            _rig.Frame(new PointerFrame(), 1f);
            Assert.IsFalse(t.IsAnimating);
            _rig.Press(1f, -1f);
            Assert.IsTrue(t.IsDragging);
        }

        [Test]
        public void PressOffThePaper_DoesNotGrab()
        {
            _rig.Press(1.6f, 1.6f);
            Assert.IsFalse(_rig.Table.IsDragging);
            _rig.Frame(new PointerFrame { LeftDown = true, LeftHeld = true });   // no paper point
            Assert.IsFalse(_rig.Table.IsDragging);
        }
    }
}
