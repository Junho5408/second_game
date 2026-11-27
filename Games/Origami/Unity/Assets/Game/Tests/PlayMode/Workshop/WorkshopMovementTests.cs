using NUnit.Framework;
using Origami.Core.Game;
using Origami.Core.Screens;
using Origami.Core.Texts;
using Origami.Runtime.Folding;
using Origami.Runtime.Gameplay;
using UnityEngine;

namespace Origami.Tests.PlayMode.Workshop
{
    /// <summary>
    /// ORIGAMI-014: F-002 (character movement and walking camera) through <see cref="OrigamiApp.ProcessFrame"/> with dt 0.02
    /// (the numbers themselves are Core <c>RoomMotion</c>, verified by dotnet). Tolerances: FEATURE_SPECS 공통 표기.
    /// </summary>
    public class WorkshopMovementTests
    {
        private WorkshopRig _rig;

        [SetUp]
        public void SetUp() => _rig = new WorkshopRig();

        [TearDown]
        public void TearDown()
        {
            _rig.Destroy();
            _rig = null;
        }

        private Vector2 Pos => _rig.App.CharacterPosition;

        [Test]
        public void F002_AC01_StartPositionFacingEast_NoInteractPrompt()
        {
            _rig.Idle(0.1f);
            Assert.AreEqual(-1.6f, Pos.x, 0.05f);
            Assert.AreEqual(0.0f, Pos.y, 0.05f);
            Assert.AreEqual(90f, _rig.App.CharacterYaw, 0.01f, "east");
            Assert.IsNull(_rig.App.InteractCandidate);
            foreach (var line in _rig.Gui.VisibleLines) StringAssert.DoesNotStartWith("E: ", line);
            Assert.IsTrue(_rig.Shows(GameTexts.WalkHint));
        }

        [Test]
        public void F002_AC02_D1Second_XPlus3_ZKept_FacesEast()
        {
            _rig.Hold(1f, 0f, 1.0f);
            Assert.AreEqual(-1.6f + 3.0f, Pos.x, 0.15f);
            Assert.AreEqual(0.0f, Pos.y, 0.05f);
            Assert.AreEqual(90f, _rig.App.CharacterYaw, 0.01f);
            Assert.AreEqual(Pos, _rig.App.Character.Position, "the view follows");
        }

        [Test]
        public void F002_AC03_WPlusD_HalfSecond_Is1Point5Metres()
        {
            var start = Pos;
            _rig.Hold(1f, 1f, 0.5f);
            Assert.AreEqual(1.5f, Vector2.Distance(start, Pos), 0.1f);
            Assert.AreEqual(45f, _rig.App.CharacterYaw, 0.01f, "faces the move direction (north-east)");
        }

        [Test]
        public void F002_AC04_A5Seconds_StopsAtTheWestWall()
        {
            _rig.Hold(-1f, 0f, 5f);
            Assert.That(Pos.x, Is.InRange(-3.75f, -3.65f));
            Assert.AreEqual(0.0f, Pos.y, 0.05f);
        }

        [Test]
        public void F002_AC04b_BedAndBookshelfBlock()
        {
            _rig.Place(-1.6, 2.0);
            _rig.Hold(-1f, 0f, 2f);
            Assert.AreEqual(-2.2f, Pos.x, 0.05f);
            _rig.Place(0.0, 1.0);
            _rig.Hold(0f, 1f, 2f);
            Assert.AreEqual(2.25f, Pos.y, 0.05f);
        }

        [Test]
        public void F002_ZeroMove_DoesNotMove()
        {
            var start = Pos;
            _rig.Hold(0f, 0f, 0.5f);   // what InputReader gives for W+S or without focus
            Assert.AreEqual(start, Pos);
        }

        [Test]
        public void F002_AC05_NoMovementOutsideWalking()
        {
            // Seated.
            _rig.Sit();
            Assert.AreEqual(WorkshopMode.Seated, _rig.Mode);
            var p = Pos;
            _rig.Hold(1f, 0f, 1f);
            Assert.AreEqual(p.x, Pos.x, 0.01f);
            Assert.AreEqual(p.y, Pos.y, 0.01f);
            _rig.Key(GameKeys.StandUp);

            // Bookshelf.
            _rig.Place(0.0, 1.5);
            _rig.Key(GameKeys.Interact);
            Assert.AreEqual(WorkshopMode.Bookshelf, _rig.Mode);
            p = Pos;
            _rig.Hold(0f, -1f, 1f);
            Assert.AreEqual(p, Pos);
            _rig.Key(GameKeys.Escape);

            // Counter.
            _rig.Place(2.5, -1.0);
            _rig.Key(GameKeys.Interact);
            Assert.AreEqual(WorkshopMode.Counter, _rig.Mode);
            p = Pos;
            _rig.Hold(-1f, 0f, 1f);
            Assert.AreEqual(p, Pos);
            _rig.Key(GameKeys.Escape);

            // Sleep confirm.
            _rig.Place(-1.5, 1.5);
            _rig.Key(GameKeys.Interact);
            Assert.AreEqual(WorkshopMode.SleepConfirm, _rig.Mode);
            p = Pos;
            _rig.Hold(1f, 1f, 1f);
            Assert.AreEqual(p, Pos);

            // Pause overlay while walking.
            _rig.Key(GameKeys.Escape);
            Assert.AreEqual(WorkshopMode.Walking, _rig.Mode);
            _rig.Key(GameKeys.Escape);
            Assert.AreEqual(Overlay.PauseMenu, _rig.Flow.Overlay);
            p = Pos;
            _rig.Hold(1f, 0f, 1f);
            Assert.AreEqual(p, Pos, "no walking behind the pause menu");
        }

        [Test]
        public void F002_AC06_WalkingCameraFollowsAtFixedOffset_NoMouseRotation()
        {
            _rig.Hold(1f, 0f, 1f);
            _rig.Idle(2f);   // follow lag e^(-8·2) ≈ 1e-7
            var rig = _rig.App.CameraRig;
            var c = Pos;
            var expected = new Vector3(c.x, 0f, c.y) + CameraRig.WalkOffset;
            Assert.Less(Vector3.Distance(expected, rig.Position), 0.2f);
            var head = new Vector3(c.x, 0f, c.y) + CameraRig.LookOffset;
            var toHead = (head - rig.Position).normalized;
            Assert.Greater(Vector3.Dot(rig.Rotation * Vector3.forward, toHead), 0.9999f, "looks at the character's head");
            Assert.Less(Vector3.Distance(rig.Position, _rig.Camera.transform.position), 1e-4f, "the camera gets the rig pose");
            Assert.AreEqual(CameraRig.WalkNearClip, _rig.Camera.nearClipPlane, 1e-6f);
            Assert.IsFalse(_rig.Camera.orthographic);

            var before = rig.Rotation;
            for (int i = 0; i < 20; i++)
            {
                var p = new PointerFrame { RightHeld = true, RightDragDelta = new Vector2(40f, 25f), Wheel = 1f };
                _rig.Frame(new InputFrame { Pointer = p });
            }
            Assert.Less(Quaternion.Angle(before, rig.Rotation), 0.01f, "mouse does not rotate the walking camera");
            Assert.Less(Vector3.Distance(expected, rig.Position), 0.2f);
        }

        [Test]
        public void F002_AC06_CameraLagsThenCatchesUp()
        {
            var rig = _rig.App.CameraRig;
            _rig.Idle(0.1f);
            var start = rig.Position;
            _rig.Hold(1f, 0f, 0.2f);
            var c = Pos;
            var target = new Vector3(c.x, 0f, c.y) + CameraRig.WalkOffset;
            Assert.Greater(Vector3.Distance(target, rig.Position), 0.05f, "smooth follow, not a hard attach");
            Assert.Greater(rig.Position.x, start.x, "but it moves along");
        }

        [Test]
        public void CameraRig_AcquiresTheMainCameraLazily_OrCreatesOne()
        {
            var rigNoCam = new WorkshopRig(ownCamera: false);
            try
            {
                var main = Camera.main;
                rigNoCam.Idle(0.02f);
                var cam = rigNoCam.App.CameraRig.Camera;
                Assert.IsNotNull(cam, "camera obtained on the first Tick (review ORIGAMI-008 N-2)");
                if (main != null) Assert.AreSame(main, cam, "uses the existing MainCamera (Bootstrap) instead of creating one");
                else Assert.AreEqual("MainCamera", cam.tag);
                Assert.IsFalse(cam.orthographic, "made perspective");
            }
            finally
            {
                rigNoCam.Destroy();
            }
        }
    }
}
