using System.Collections.Generic;
using NUnit.Framework;
using Origami.Core.Screens;
using Origami.Core.Game;
using Origami.Core.Room;
using Origami.Runtime.Gameplay;
using Origami.Runtime.Systems;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;

namespace Origami.Tests.PlayMode.Workshop
{
    /// <summary>
    /// ORIGAMI-019: the CC0 models in the workshop. Furniture models fill the Core <c>RoomLayout</c> boxes (ASSETS.md fits,
    /// DECISIONS 2026-11-28 scale rule), the window glass is tinted by MaterialPropertyBlock, the door cut-away, lighting,
    /// the Crafter's measured height/front, Idle↔Walk by movement, SitDown on the chair seat, and the code-mesh fallback.
    /// Everything runs through synchronous <see cref="OrigamiApp.ProcessFrame"/> calls with dt 0.02 (no frame timing).
    /// Native sizes, measured fronts and sit numbers are printed for the report.
    /// </summary>
    public class WorkshopArtTests
    {
        private const float Tol = 0.02f;

        private WorkshopRig _rig;
        private readonly List<Object> _spawned = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            _rig?.Destroy();
            _rig = null;
            foreach (var o in _spawned)
                if (o != null) Object.Destroy(o);
            _spawned.Clear();
        }

        private static FurnitureSpec Spec(string id)
        {
            foreach (var f in RoomLayout.Furniture)
                if (f.Id == id) return f;
            Assert.Fail("no furniture " + id);
            return null;
        }

        private Bounds FurnitureBounds(string id)
        {
            var w = _rig.App.Workshop;
            Assert.IsTrue(w.UsesModel(id), id + " should be drawn with its model");
            Assert.IsTrue(w.TryFurnitureBounds(id, out var b), id + " bounds");
            TestContext.WriteLine($"{id}: size {b.size.ToString("F3")}, centre {b.center.ToString("F3")}, min y {b.min.y:F3}, max y {b.max.y:F3}");
            return b;
        }

        private static void AssertBox(Bounds b, FurnitureSpec f, float height, string what)
        {
            Assert.AreEqual((float)f.Width, b.size.x, Tol, what + " width (x)");
            Assert.AreEqual((float)f.Depth, b.size.z, Tol, what + " depth (z)");
            Assert.AreEqual(height, b.size.y, Tol, what + " height");
            Assert.AreEqual((float)f.Center.X, b.center.x, Tol, what + " centre x");
            Assert.AreEqual((float)f.Center.Y, b.center.z, Tol, what + " centre z");
            Assert.AreEqual(0f, b.min.y, Tol, what + " stands on the floor");
        }

        // ------------------------------------------------------------------ furniture

        [Test]
        public void WorkFurniture_ModelsFillTheCoreBoxes_TableTopAndCounterTopKeepTheirHeights()
        {
            _rig = new WorkshopRig();
            var table = Spec("table");
            AssertBox(FurnitureBounds("table"), table, (float)table.Height, "table");
            var space = _rig.App.Table.transform.Find("PaperSpace");
            Assert.IsNotNull(space, "FoldTable paper space");
            Assert.AreEqual(space.position.y, FurnitureBounds("table").max.y, Tol, "table top right under the paper (FoldTable 0.75)");

            var shelf = Spec("bookshelf");
            AssertBox(FurnitureBounds("bookshelf"), shelf, (float)shelf.Height, "bookshelf");

            var counter = Spec("counter");
            var cb = FurnitureBounds("counter");
            AssertBox(cb, counter, (float)counter.Height, "counter (two display shelves)");
            Assert.AreEqual(WorkshopView.CounterTop, cb.max.y, Tol, "counter top = display slot height");
            Assert.AreEqual(WorkshopView.CounterTop, _rig.App.Workshop.Slot(0).transform.position.y, 1e-4f);

            var chair = Spec("chair");
            AssertBox(FurnitureBounds("chair"), chair, WorkshopView.ChairHeight, "chair (with backrest)");
        }

        [Test]
        public void OtherFurniture_UniformFits_StayInsideTheirCoreBoxes()
        {
            _rig = new WorkshopRig();
            var bed = Spec("bed");
            var b = FurnitureBounds("bed");
            Assert.AreEqual((float)bed.Width, b.size.x, Tol, "bed width fills the box (uniform)");
            Assert.LessOrEqual(b.size.z, (float)bed.Depth + Tol);
            Assert.AreEqual((float)bed.Center.X, b.center.x, Tol);
            Assert.AreEqual((float)bed.Center.Y, b.center.z, Tol);
            Assert.AreEqual(0f, b.min.y, Tol);

            var trash = Spec("trash");
            var t = FurnitureBounds("trash");
            Assert.LessOrEqual(t.size.x, (float)trash.Width + Tol);
            Assert.LessOrEqual(t.size.z, (float)trash.Depth + Tol);
            Assert.That(Mathf.Min(Mathf.Abs(t.size.x - (float)trash.Width), Mathf.Abs(t.size.z - (float)trash.Depth)), Is.LessThan(Tol),
                "one footprint side fills the box");
            Assert.AreEqual((float)trash.Center.X, t.center.x, Tol);
            Assert.AreEqual((float)trash.Center.Y, t.center.z, Tol);
            AssertUniform("trash");

            var door = Spec("door");
            var d = FurnitureBounds("door");
            Assert.AreEqual((float)door.Width, d.size.x, Tol, "door width 1.0");
            Assert.That(d.size.y, Is.InRange(1.9f, 2.2f), "door ~2.08 m");
            Assert.AreEqual((float)door.Center.X, d.center.x, Tol);
            Assert.AreEqual((float)RoomLayout.MinZ, d.center.z, Tol, "door depth centred on the south wall plane");
            AssertUniform("door");

            var window = Spec("window");
            var w = FurnitureBounds("window");
            Assert.AreEqual(WorkshopView.WindowBottom, w.min.y, Tol, "window bottom");
            Assert.AreEqual(WorkshopView.WindowHeight, w.size.y, Tol, "window height");
            Assert.AreEqual((float)window.Center.X, w.center.x, Tol, "window centre x");
            Assert.LessOrEqual(w.size.x, (float)window.Width + Tol, "two frames within the 1.2 m opening");
            Assert.That(w.max.z, Is.LessThan((float)RoomLayout.MaxZ), "in front of the north wall");
        }

        private void AssertUniform(string id)
        {
            var obj = _rig.App.Workshop.transform.Find(id == "trash" ? "TrashCan" : "Door");
            Assert.IsNotNull(obj, id + " wrapper");
            var s = obj.localScale;
            Assert.AreEqual(s.x, s.y, 1e-5f, id + " uniform scale");
            Assert.AreEqual(s.x, s.z, 1e-5f, id + " uniform scale");
        }

        [Test]
        public void Props_Load_AndTrashBallsSitOnTheBin()
        {
            _rig = new WorkshopRig();
            var w = _rig.App.Workshop;
            TestContext.WriteLine("props: " + w.PropCount);
            Assert.That(w.PropCount, Is.GreaterThanOrEqualTo(10), "rug, lamps, plants, box, coat rack, books, panels");
            Assert.IsTrue(w.TryFurnitureBounds("trash", out var t));
            var ball = w.Ball(0).transform.position;
            Assert.That(ball.y, Is.InRange(t.max.y - 0.1f, t.max.y + 0.05f), "first ball at the rim");
            Assert.AreEqual(t.center.x, ball.x, 0.05f);
        }

        [Test]
        public void Window_GlassTintedByPropertyBlock_FollowsTheDayToSunsetColour()
        {
            _rig = new WorkshopRig();
            var w = _rig.App.Workshop;
            _rig.Idle(0.02f);
            Assert.AreEqual(2, w.GlassSlotCount, "one glass slot per casement");
            var day = w.WindowColor;
            AssertTint(w, day);

            _rig.Sit();
            Assert.IsTrue(_rig.Flow.TakeSheet().Accepted);
            _rig.Idle(0.02f);
            var later = w.WindowColor;
            Assert.AreNotEqual(day, later, "one sheet used → towards sunset");
            AssertTint(w, later);
        }

        private static void AssertTint(WorkshopView w, Color expected)
        {
            for (int i = 0; i < w.GlassSlotCount; i++)
            {
                var c = w.GlassTint(i);
                Assert.AreEqual(expected.r, c.r, 1e-3f, "glass " + i + " r");
                Assert.AreEqual(expected.g, c.g, 1e-3f, "glass " + i + " g");
                Assert.AreEqual(expected.b, c.b, 1e-3f, "glass " + i + " b");
            }
        }

        [Test]
        public void Door_IsCutAway_WhileTheCharacterStandsBehindIt()
        {
            _rig = new WorkshopRig();
            var w = _rig.App.Workshop;
            _rig.Idle(0.02f);
            Assert.IsFalse(w.DoorCutaway, "start position (−1.6, 0)");
            _rig.Place(-3.0, -2.5);
            Assert.IsTrue(w.DoorCutaway, "character right behind the door");
            _rig.Idle(0.02f);
            Assert.IsTrue(w.DoorCutaway);
            _rig.Hold(1f, 0f, 1f);   // east, away from the door
            Assert.IsFalse(w.DoorCutaway);
            Assert.IsTrue(w.UsesModel("door"));
        }

        [Test]
        public void Lighting_WarmSoftShadowSun_TrilightAmbient_NoFog_PaperOutsideTheSeatedShadow()
        {
            _rig = new WorkshopRig();
            var w = _rig.App.Workshop;
            var sun = w.Sun;
            Assert.IsNotNull(sun);
            Assert.AreEqual(LightType.Directional, sun.type);
            Assert.AreEqual(LightShadows.Soft, sun.shadows);
            Assert.That(sun.intensity, Is.GreaterThan(0.8f));
            Assert.That(sun.color.r, Is.GreaterThanOrEqualTo(sun.color.b), "warm");
            Assert.That(Vector3.Dot(sun.transform.forward, Vector3.down), Is.GreaterThan(0.7f), "steep: the table top is lit");
            Assert.AreEqual(AmbientMode.Trilight, RenderSettings.ambientMode);
            Assert.IsFalse(RenderSettings.fog, "no fog (ARCHITECTURE §14.5)");
            Assert.IsNotNull(w.LampLight, "warm bedside lamp");

            // Seated: no bone of the character throws its shadow onto the paper square (table centre ± 0.1 m + margin).
            _rig.Sit();
            var character = _rig.App.Character;
            Assert.IsTrue(character.UsesModel);
            _rig.Idle(character.Animation[ArtKeys.ClipSitDown].length + 0.2f);
            var paper = new Vector3(2.5f, 0.75f, 1.6f);
            var d = sun.transform.forward;
            foreach (var t in character.Model.GetComponentsInChildren<Transform>())
            {
                var p = t.position;
                if (p.y <= paper.y || t.name.StartsWith("PoleTarget", System.StringComparison.Ordinal)) continue;   // IK helpers, not drawn
                var hit = p + d * ((p.y - paper.y) / -d.y);
                bool onPaper = Mathf.Abs(hit.x - paper.x) < 0.15f && Mathf.Abs(hit.z - paper.z) < 0.15f;
                Assert.IsFalse(onPaper, $"{t.name} at {p.ToString("F2")} shadows the paper at {hit.ToString("F2")}");
            }
        }

        // ------------------------------------------------------------------ character

        [Test]
        public void Character_IsTheCrafter_FittedTo1_6m_FrontMeasured()
        {
            _rig = new WorkshopRig();
            var c = _rig.App.Character;
            Assert.IsTrue(c.UsesModel, "Characters/Crafter loaded");
            Assert.IsNotNull(c.Animation);
            TestContext.WriteLine($"Crafter native height {c.NativeHeight:F3} ({c.HeightSource}), head_end {c.HeadNativeHeight:F3}, scale {c.ModelScale:F5}, front yaw {c.FrontYaw} " +
                $"(measured {c.FrontMeasured}), walk clip ground speed {c.ClipGroundSpeed:F2} m/s, idle hips {c.IdleHips.ToString("F3")}, " +
                $"sit hips {c.SitHips.ToString("F3")}, sit offset {c.SitOffset.ToString("F3")}");
            Assert.AreEqual((float)RoomLayout.CharacterHeight, c.NativeHeight * c.ModelScale, 1e-3f);
            Assert.AreEqual(0f, Mathf.Repeat(c.FrontYaw, 90f), 1e-3f, "front snapped to 90°");
            // Review ORIGAMI-019 F-1: a degenerate scale (0.00487 from the 100x imported localBounds) must not pass again.
            // Crafter.fbx: mesh 3.33 units tall, Head_end 2.77, so the scale is ~0.48 and the mesh top / Head_end ratio ~1.2.
            Assert.That(c.ModelScale, Is.InRange(0.3f, 0.7f), "scale of a ~3.3-unit model fitted to 1.6 m");
            Assert.That(c.NativeHeight / c.HeadNativeHeight, Is.InRange(CharacterView.MinTopToHead, CharacterView.MaxTopToHead), "mesh top vs head bone");
            // Stylised big-head proportions: the hips are at ~27 % of the height (FBX bind pose 0.90 of 3.33 units), so ~0.43 m.
            Assert.That(c.IdleHips.y, Is.InRange(0.3f, 1.0f), "standing hips height");
            Assert.That(c.ClipGroundSpeed, Is.InRange(0.3f, 3.0f), "walk ground speed measured from the foot travel");
            Assert.AreNotEqual(CharacterClips.DefaultClipGroundSpeed, c.ClipGroundSpeed, "measured, not the default");
            foreach (var smr in c.Model.GetComponentsInChildren<SkinnedMeshRenderer>())
                Assert.IsTrue(smr.updateWhenOffscreen, "bounds recomputed from the pose (imported localBounds are 100x)");

            _rig.Idle(0.1f);
            var head = c.HeadBoneTransform;
            Assert.IsNotNull(head, "Head_end / Head bone");
            Assert.That(head.position.y, Is.InRange(1.1f, 1.6f), "standing Head_end of a 1.6 m character (top of the hair 1.6)");

            // Facing: the character looks east at the start (yaw 90); the toes point east.
            var foot = FindBone(c.Model.transform, "Foot.L");
            var toe = FindBone(c.Model.transform, "Foot.L_end");
            if (foot != null && toe != null)
            {
                var dir = toe.position - foot.position;
                dir.y = 0f;
                Assert.That(Vector3.Dot(dir.normalized, Vector3.right), Is.GreaterThan(0.5f), "toes point the facing direction (east)");
            }
        }

        [Test]
        public void Character_SwitchesIdleToWalkAndBack_WithMovement_ShortCrossfade()
        {
            _rig = new WorkshopRig();
            var c = _rig.App.Character;
            _rig.Idle(0.1f);
            Assert.AreEqual(ArtKeys.ClipIdle, c.CurrentClip);
            Assert.AreEqual(0f, c.WalkWeight, 1e-6f);

            _rig.Hold(0f, 1f, 0.02f);   // one frame north
            Assert.AreEqual(WorkshopRig.Dt / 0.15f, c.WalkWeight, 1e-4f, "cross-fade starts");
            _rig.Hold(0f, 1f, 0.4f);
            Assert.AreEqual(ArtKeys.ClipWalk, c.CurrentClip);
            Assert.AreEqual(1f, c.WalkWeight, 1e-6f);
            Assert.AreEqual(0f, Quaternion.Angle(c.transform.rotation, Quaternion.Euler(0f, 0f, 0f)), 0.01f, "faces north (move direction)");
            Assert.IsTrue(c.Animation[ArtKeys.ClipWalk].enabled);
            Assert.AreEqual(0f, c.Animation[ArtKeys.ClipWalk].speed, "time is driven from dt, not by the engine");

            _rig.Idle(0.3f);
            Assert.AreEqual(ArtKeys.ClipIdle, c.CurrentClip);
            Assert.AreEqual(0f, c.WalkWeight, 1e-6f);
            Assert.IsFalse(c.Animation[ArtKeys.ClipWalk].enabled);
        }

        [Test]
        public void Character_WalkPose_IsDeterministicFromDt()
        {
            _rig = new WorkshopRig();
            var other = new WorkshopRig();
            try
            {
                for (int i = 0; i < 17; i++)
                {
                    _rig.Frame(new InputFrame { Move = new Vector2(1f, 0f) });
                    other.Frame(new InputFrame { Move = new Vector2(1f, 0f) });
                }
                var a = FindBone(_rig.App.Character.Model.transform, "Foot.L");
                var b = FindBone(other.App.Character.Model.transform, "Foot.L");
                Assert.IsNotNull(a);
                Assert.AreEqual(a.localRotation, b.localRotation, "same dt sequence → same pose");
                Assert.AreEqual(_rig.App.Character.Animation[ArtKeys.ClipWalk].time, other.App.Character.Animation[ArtKeys.ClipWalk].time, 1e-6f);
            }
            finally
            {
                other.Destroy();
            }
        }

        [Test]
        public void Character_SitsDownOnTheChairSeat_AndStandsUpToIdle()
        {
            _rig = new WorkshopRig();
            var c = _rig.App.Character;
            _rig.Sit();
            Assert.AreEqual(WorkshopMode.Seated, _rig.Mode);
            _rig.Idle(0.02f);
            Assert.AreEqual(ArtKeys.ClipSitDown, c.CurrentClip);
            float len = c.Animation[ArtKeys.ClipSitDown].length;
            _rig.Idle(len + 0.5f);
            Assert.AreEqual(len, c.Animation[ArtKeys.ClipSitDown].time, 1e-4f, "last frame held");
            Assert.IsTrue(c.Animation[ArtKeys.ClipSitDown].enabled);

            var hips = c.HipsBoneTransform;
            Assert.IsNotNull(hips, "Hips bone");
            TestContext.WriteLine($"seated hips {hips.position.ToString("F3")}, sit offset {c.SitOffset.ToString("F3")}, raw sit hips {c.SitHips.ToString("F3")}");
            Assert.That(Mathf.Abs(c.SitOffset.y), Is.LessThanOrEqualTo(0.35f), "the sit correction is an adjustment, not a lift");
            Assert.AreEqual(WorkshopView.ChairSeatHeight + CharacterView.HipAboveSeat, hips.position.y, 0.03f, "hips rest on the 0.48 m seat");
            var chair = RoomLayout.ChairPosition;
            Assert.AreEqual((float)chair.X, hips.position.x, 0.05f);
            Assert.AreEqual((float)chair.Y - CharacterView.SeatBack, hips.position.z, 0.05f);

            // The folding view (camera 0.44 m from the paper) is not inside the model; a camera at the hips hides it.
            Assert.IsFalse(c.CameraCutaway, "default seated view");
            c.ApplyCameraCutaway(hips.position);
            Assert.IsTrue(c.CameraCutaway);
            foreach (var r in c.Model.GetComponentsInChildren<Renderer>()) Assert.AreEqual(ShadowCastingMode.ShadowsOnly, r.shadowCastingMode);
            c.ApplyCameraCutaway(hips.position + new Vector3(0f, 0f, 3f));
            Assert.IsFalse(c.CameraCutaway);

            _rig.Key(GameKeys.StandUp);
            Assert.AreEqual(WorkshopMode.Walking, _rig.Mode);
            Assert.AreEqual(ArtKeys.ClipIdle, c.CurrentClip);
            Assert.IsFalse(c.Animation[ArtKeys.ClipSitDown].enabled);
            Assert.AreEqual(Vector3.zero, c.transform.Find("Body").localPosition, "no sit offset when standing");
        }

        // ------------------------------------------------------------------ fallback

        [Test]
        public void Fallback_WithoutModels_DrawsTheCodeMeshes_AndKeepsWorking()
        {
            var go = new GameObject("FallbackWorkshop");
            _spawned.Add(go);
            go.SetActive(false);
            var w = go.AddComponent<WorkshopView>();
            w.LoadModels = false;
            var cgo = new GameObject("FallbackCharacter");
            _spawned.Add(cgo);
            cgo.SetActive(false);
            var c = cgo.AddComponent<CharacterView>();
            c.LoadModel = false;
            go.SetActive(true);
            cgo.SetActive(true);

            foreach (var f in RoomLayout.Furniture) Assert.IsFalse(w.UsesModel(f.Id), f.Id);
            Assert.IsNotNull(go.transform.Find("TableTop"), "code table");
            Assert.IsNull(go.transform.Find("Table"));
            Assert.AreEqual(0, w.PropCount);
            Assert.AreEqual(0, w.GlassSlotCount);
            Assert.IsTrue(w.TryFurnitureBounds("table", out var tb));
            Assert.AreEqual(0.75f, tb.max.y, Tol);
            w.Refresh(null);
            Assert.AreEqual(0, w.DisplayedPieceCount);
            w.UpdateCutaway(new Vector2(-3f, -2.5f));
            Assert.IsTrue(w.DoorCutaway);

            Assert.IsFalse(c.UsesModel);
            Assert.IsNull(c.CurrentClip);
            c.SetPose(new Vector2(1f, 1f), 90f, false, 0.5f);
            c.Tick(0.02f, 3f);
            c.SetPose(new Vector2(2.5f, 0.8f), 0f, true, 0f);
            c.Tick(0.02f, 0f);
            Assert.IsTrue(c.Sitting);
            Assert.IsNotNull(cgo.transform.Find("Body/Head"), "code figure");
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void ModelFit_MissingKey_ReturnsNull_WithoutThrowingOrLogging()
        {
            var parent = new GameObject("FitParent");
            _spawned.Add(parent);
            Assert.IsNull(ModelFit.Load(null));
            Assert.IsNull(ModelFit.Load(""));
            Assert.IsNull(ModelFit.Spawn("Workshop/DoesNotExist", parent.transform, "X", FitSpec.Scale(1f), Vector3.zero, 0f));
            Assert.IsNull(ModelFit.Place(null, parent.transform, "X", FitSpec.Scale(1f), Vector3.zero, 0f));
            var empty = new GameObject("NoRenderer");
            _spawned.Add(empty);
            Assert.IsNull(ModelFit.Place(empty, parent.transform, "X", FitSpec.Scale(1f), Vector3.zero, 0f), "no renderer → fallback");
            Assert.AreEqual(0, parent.transform.childCount, "nothing attached on failure");
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void ModelFit_BoxFitsPerAxis_WithinTheRule_ElseUniform_HeightAndOrigin()
        {
            var mesh = MeshFactory.Box(new Vector3(2f, 4f, 1f));
            _spawned.Add(mesh);
            var mat = Materials.Lit(Color.white);
            _spawned.Add(mat);
            var src = new GameObject("Src");
            _spawned.Add(src);
            var child = new GameObject("Part");
            child.transform.SetParent(src.transform, false);
            child.transform.localPosition = new Vector3(10f, 5f, -3f);   // pivot far from the mesh, like Kenney
            child.AddComponent<MeshFilter>().sharedMesh = mesh;
            child.AddComponent<MeshRenderer>().sharedMaterial = mat;
            var parent = new GameObject("FitParent");
            _spawned.Add(parent);
            parent.transform.position = new Vector3(1f, 2f, 3f);

            // Box within ±40 %: exact per-axis fill, bottom centre on the target.
            var a = ModelFit.Place(src, parent.transform, "A", FitSpec.Box(new Vector3(0.5f, 0.75f, 0.2f)), new Vector3(0.5f, 0f, -1f), 0f);
            Assert.IsNotNull(a);
            Assert.IsTrue(ModelFit.TryWorldBounds(a, out var ab));
            Assert.AreEqual(new Vector3(0.5f, 0.75f, 0.2f).ToString("F4"), ab.size.ToString("F4"));
            Assert.AreEqual(1.5f, ab.center.x, 1e-4f);
            Assert.AreEqual(2f, ab.min.y, 1e-4f);
            Assert.AreEqual(2f, ab.center.z, 1e-4f);

            // Box breaking the rule (y scale 4× the others): uniform = the smallest, still bottom-centred.
            var scale = ModelFit.ScaleFor(FitSpec.Box(new Vector3(1f, 8f, 0.5f)), new Vector3(2f, 4f, 1f), out bool fellBack);
            Assert.IsTrue(fellBack);
            Assert.AreEqual(new Vector3(0.5f, 0.5f, 0.5f), scale);
            var b = ModelFit.Place(src, parent.transform, "B", FitSpec.Box(new Vector3(1f, 8f, 0.5f)), Vector3.zero, 90f);
            Assert.IsTrue(ModelFit.TryWorldBounds(b, out var bb));
            Assert.AreEqual(2f, bb.size.y, 1e-4f, "uniform 0.5");
            Assert.AreEqual(1f, bb.center.x, 1e-4f, "rotation keeps the bottom centre");
            Assert.AreEqual(3f, bb.center.z, 1e-4f);
            Assert.AreEqual(0.5f, bb.size.x, 1e-3f, "yaw 90 swaps the footprint (native depth 1 × 0.5)");
            Assert.AreEqual(1f, bb.size.z, 1e-3f, "native width 2 × 0.5");

            // Height and origin anchoring.
            var h = ModelFit.Place(src, parent.transform, "H", FitSpec.Height(1f), Vector3.zero, 0f, anchorOrigin: true);
            Assert.AreEqual(0.25f, h.transform.localScale.y, 1e-5f);
            Assert.AreEqual(Vector3.zero, h.transform.GetChild(0).localPosition, "origin anchor keeps the model's own origin on the target");
            LogAssert.NoUnexpectedReceived();
        }

        private static Transform FindBone(Transform root, string name)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t;
            return null;
        }
    }
}
