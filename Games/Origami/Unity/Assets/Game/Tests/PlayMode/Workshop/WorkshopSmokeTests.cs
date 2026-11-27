using System.Collections;
using NUnit.Framework;
using Origami.Core;
using Origami.Core.Catalog;
using Origami.Core.Game;
using Origami.Core.Room;
using Origami.Core.Screens;
using Origami.Core.Texts;
using Origami.Core.Tutorial;
using Origami.Runtime.Gameplay;
using Origami.Runtime.UI;
using UnityEngine;
using UnityEngine.TestTools;

namespace Origami.Tests.PlayMode.Workshop
{
    /// <summary>
    /// ORIGAMI-014 full-connection smoke test: one day through the real components (keys, walking, the folding table with drags,
    /// GUI texts, workshop display, camera) — sit → the 6 tutorial steps by mouse drags → finish → sleep → settlement — driven
    /// only by <see cref="OrigamiApp.ProcessFrame"/> with dt 0.02. Plus the runtime hook and a real OnGUI pass with the book.
    /// </summary>
    public class WorkshopSmokeTests
    {
        private WorkshopRig _rig;

        [TearDown]
        public void TearDown()
        {
            _rig?.Destroy();
            _rig = null;
        }

        [Test]
        public void Smoke_OneDay_Sit_Tutorial6Steps_Finish_Sleep_Settlement()
        {
            _rig = new WorkshopRig();
            var app = _rig.App;
            Assert.AreEqual("2.0.0", GameInfo.Version);

            // Morning: start position, walk hint.
            _rig.Idle(0.1f);
            Assert.IsTrue(_rig.Shows(GameTexts.WalkHint));
            Assert.IsTrue(_rig.Shows("1일차 / 7"));
            Assert.IsTrue(_rig.Shows("돈 20원"));

            // Walk east (D) to the table and sit with E.
            _rig.Hold(1f, 0f, 1.2f);
            Assert.AreEqual(2.0f, app.CharacterPosition.x, 0.15f);
            Assert.AreEqual(InteractTargets.Table, app.InteractCandidate);
            Assert.IsTrue(_rig.Shows("E: 앉기"));
            _rig.Key(GameKeys.Interact);
            Assert.AreEqual(WorkshopMode.Seated, _rig.Mode);
            _rig.Idle(0.6f);
            Assert.IsTrue(app.CameraRig.InSeatedView);

            // First sheet (free, tutorial).
            Assert.IsTrue(_rig.Shows(GameTexts.TakeSheetHint));
            _rig.Key(GameKeys.TakeSheet);
            var sheet = _rig.Snap.Sheet;
            Assert.IsNotNull(sheet);
            Assert.IsTrue(sheet.IsTutorial);
            Assert.AreEqual(20, _rig.Snap.Money);
            Assert.Greater(app.Table.PaperVertexCount, 0, "the paper is drawn on the table");

            // The 6 guided steps, each by a mouse drag on the table (crease steps with Ctrl held on release).
            for (int n = 1; n <= PlaneGuide.StepCount; n++)
            {
                sheet = _rig.Snap.Sheet;
                Assert.IsTrue(sheet.GuideVisible, "guide step " + n);
                Assert.AreEqual(n, sheet.Guide.Number);
                Assert.IsTrue(_rig.Shows(GameTexts.GuideTitle(sheet.Guide)), "guide title shown");
                Assert.IsTrue(app.Table.GuideMarkersVisible, "guide markers on the table");
                _rig.DoGuideStep();
                Assert.AreEqual(n, _rig.Snap.Sheet.ActionCount, "step " + n + " applied by the drag");
                Assert.AreEqual(n, _rig.Snap.Sheet.GuideScores.Count);
                Assert.IsFalse(app.Table.IsAnimating);
            }
            Assert.IsFalse(_rig.Snap.Sheet.GuideVisible, "guide done after 6 steps");
            Assert.AreEqual(1, _rig.Snap.Sheet.CreaseCount, "step 1 was a crease");

            // Finish (Enter): recognised as the plane, shown on the counter, toast with the result.
            _rig.Key(GameKeys.Enter);
            Assert.IsNull(_rig.Snap.Sheet);
            Assert.AreEqual(1, _rig.Snap.Display.Count);
            Assert.AreEqual(ModelIds.Plane, _rig.Snap.Display[0].ModelId);
            StringAssert.StartsWith("종이비행기 ★", _rig.Gui.Toast);
            Assert.IsTrue(_rig.Shows(GameTexts.PostTutorialHint));
            Assert.AreEqual(1, app.Workshop.DisplayedPieceCount);
            Assert.IsTrue(_rig.Snap.TutorialFinished);

            // Stand up (Q), walk to the bed, sleep (E, Enter).
            _rig.Key(GameKeys.StandUp);
            Assert.AreEqual(WorkshopMode.Walking, _rig.Mode);
            Assert.AreEqual(2.5f, app.CharacterPosition.x, 0.05f);
            Assert.AreEqual(0.2f, app.CharacterPosition.y, 0.05f);
            _rig.Hold(-1f, 0f, 1.5f);   // x 2.5 → −2.0
            _rig.Hold(0f, 1f, 0.5f);    // z 0.2 → 1.7
            Assert.AreEqual(InteractTargets.Bed, app.InteractCandidate);
            Assert.IsTrue(_rig.Shows("E: 잠자기"));
            _rig.Key(GameKeys.Interact);
            Assert.AreEqual(WorkshopMode.SleepConfirm, _rig.Mode);
            Assert.IsTrue(_rig.Shows("잠자기 확인"));
            _rig.Key(GameKeys.Enter);

            // Settlement screen: the plane is sold (popular on day 1).
            Assert.AreEqual(Overlay.Settlement, _rig.Flow.Overlay);
            var settlement = _rig.Flow.PendingSettlement;
            Assert.IsNotNull(settlement);
            Assert.AreEqual(1, settlement.Lines.Count);
            Assert.IsTrue(settlement.Lines[0].Popular);
            foreach (var line in GameTexts.SettlementLines(settlement)) Assert.IsTrue(_rig.Shows(line), line);
            Assert.IsTrue(_rig.Shows("1일차 정산"));
            Assert.IsTrue(_rig.Shows("내일의 인기: 집"));
            Assert.AreEqual(20 + settlement.Total, _rig.Snap.Money);
            Assert.AreEqual(0, app.Workshop.DisplayedPieceCount, "the display was sold");

            // Next morning.
            _rig.Key(GameKeys.Enter);
            Assert.AreEqual(Overlay.None, _rig.Flow.Overlay);
            Assert.AreEqual(2, _rig.Snap.Day);
            Assert.AreEqual(5, _rig.Snap.SheetsLeft);
            Assert.AreEqual(-1.6f, app.CharacterPosition.x, 0.05f);
            Assert.AreEqual(0.0f, app.CharacterPosition.y, 0.05f);
            Assert.IsTrue(_rig.Shows("2일차 / 7"));
            Assert.IsTrue(_rig.Shows("오늘의 인기: 집"));
        }

        [UnityTest]
        public IEnumerator RuntimeHook_CreatesOneAppWithGui_AndKeepsTheBootstrapCamera()
        {
            yield return null;
            var app = OrigamiApp.Instance;
            Assert.IsNotNull(app, "AfterSceneLoad hook → OrigamiApp.EnsureInstance");
            Assert.AreEqual(OrigamiApp.ObjectName, app.gameObject.name);
            Assert.AreSame(app, OrigamiApp.EnsureInstance(), "idempotent");
            var gui = app.GetComponent<OrigamiGui>();
            Assert.IsNotNull(gui, "the GUI is attached to the runtime app");
            Assert.AreEqual(1, CountRuntimeApps(), "exactly one runtime app object");
            Assert.IsNotNull(app.Table);
            Assert.IsNotNull(app.Book);
            Assert.IsNotNull(app.Workshop);
            Assert.IsNotNull(app.Character);
            Assert.IsNotNull(app.CameraRig);
            Assert.IsNotNull(Camera.main, "a main camera exists");
            Assert.IsNotNull(GameObject.Find(Origami.Runtime.Bootstrap.RootName));
            Assert.IsTrue(gui.VisibleLines.Count > 0);
        }

        [UnityTest]
        public IEnumerator OnGui_DrawsTheBookWithBookPanelDraw()
        {
            // Review ORIGAMI-013 W-3: BookPanel.Draw from a real OnGUI pass (bookshelf = full-screen book) without errors.
            _rig = new WorkshopRig();
            var p = RoomLayout.InteractPoint(InteractTargets.Bookshelf);
            _rig.Place(p.X, p.Y);
            _rig.Key(GameKeys.Interact);
            Assert.AreEqual(WorkshopMode.Bookshelf, _rig.Mode);
            Assert.AreEqual(OrigamiGui.BookPlacement.Full, _rig.Gui.Book);
            _rig.App.Book.ClickBuy(ModelIds.Dog);   // rejected → toast drawn over the book
            _rig.Idle(0.02f);
            Assert.AreEqual("돈이 모자라요", _rig.Gui.Toast);

            int start = _rig.Gui.DrawCount;
            for (int i = 0; i < 60 && _rig.Gui.DrawCount <= start; i++) yield return null;
            if (_rig.Gui.DrawCount <= start)
                Assert.Inconclusive("OnGUI was not called in this environment (batch mode without repaint)");
            Assert.IsTrue(_rig.Gui.BookDrawn, "BookPanel.Draw ran inside OnGUI");
            Assert.IsNotNull(_rig.App.Book.LeftPage, "Draw prepared the spread");
            Assert.AreEqual(0, _rig.App.Book.LeftPage.Index);
        }

        private static int CountRuntimeApps()
        {
            int n = 0;
            foreach (var a in Object.FindObjectsByType<OrigamiApp>(FindObjectsSortMode.None))
                if (a.gameObject.name == OrigamiApp.ObjectName) n++;
            return n;
        }
    }
}
