using System;
using NUnit.Framework;
using Origami.Core.Folding;
using Origami.Runtime.Folding;
using Origami.Runtime.Systems;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Origami.Tests.PlayMode.FoldTableTests
{
    /// <summary>
    /// ORIGAMI-012: ray–plane picking, seated camera orbit/zoom, code materials (review N-4) and code meshes (C-190).
    /// </summary>
    public class FoldTableViewTests
    {
        private TableRig _rig;
        private GameObject _camGo;
        private Mesh _mesh;

        [SetUp]
        public void SetUp() => _rig = new TableRig();

        [TearDown]
        public void TearDown()
        {
            _rig.Destroy();
            _rig = null;
            if (_camGo != null) Object.Destroy(_camGo);
            _camGo = null;
            if (_mesh != null) Object.Destroy(_mesh);
            _mesh = null;
        }

        private Camera SeatedCamera()
        {
            _camGo = new GameObject("FoldTableTestCamera");
            var cam = _camGo.AddComponent<Camera>();
            cam.orthographic = false;
            cam.fieldOfView = 40f;
            cam.nearClipPlane = 0.01f;
            _rig.Table.GetSeatedCameraPose(out var pos, out var rot);
            _camGo.transform.SetPositionAndRotation(pos, rot);
            Assume.That(cam.pixelWidth > 0 && cam.pixelHeight > 0, "the test camera needs a non-empty viewport");
            return cam;
        }

        private static float DistanceToPaper(FoldTable t, Vector3 tablePose)
        {
            t.GetSeatedCameraPose(out var pos, out _);
            return Vector3.Distance(pos, tablePose);
        }

        [Test]
        public void ScreenToPaper_RoundTripsAPaperPoint()
        {
            var cam = SeatedCamera();
            var tablePose = new Vector3(2.5f, 0.75f, 1.6f);
            float h = PaperMeshBuilder.LayerTop(0) * FoldTable.PaperScale;
            var world = tablePose + new Vector3(0.3f * FoldTable.PaperScale, h, -0.4f * FoldTable.PaperScale);
            var screen = cam.WorldToScreenPoint(world);

            Assert.IsTrue(_rig.Table.ScreenToPaper(cam, new Vector2(screen.x, screen.y), out var paper));
            Assert.AreEqual(0.3f, paper.x, 1e-3f);
            Assert.AreEqual(-0.4f, paper.y, 1e-3f);
        }

        [Test]
        public void ScreenToPaper_FollowsSetTablePose()
        {
            var pose = new Vector3(-1f, 0.5f, 2f);
            _rig.Table.SetTablePose(pose);
            var cam = SeatedCamera();
            float h = PaperMeshBuilder.LayerTop(0) * FoldTable.PaperScale;
            var screen = cam.WorldToScreenPoint(pose + new Vector3(-0.05f, h, 0.07f));
            Assert.IsTrue(_rig.Table.ScreenToPaper(cam, new Vector2(screen.x, screen.y), out var paper));
            Assert.AreEqual(-0.5f, paper.x, 1e-3f);
            Assert.AreEqual(0.7f, paper.y, 1e-3f);
        }

        [Test]
        public void ScreenToPaper_FalseWithoutCameraOrLookingUp()
        {
            Assert.IsFalse(_rig.Table.ScreenToPaper(null, Vector2.zero, out _));
            var cam = SeatedCamera();
            _camGo.transform.SetPositionAndRotation(new Vector3(2.5f, 0.2f, 1.6f), Quaternion.Euler(-60f, 0f, 0f));
            var centre = new Vector2(cam.pixelWidth * 0.5f, cam.pixelHeight * 0.5f);
            Assert.IsFalse(_rig.Table.ScreenToPaper(cam, centre, out _));
        }

        [Test]
        public void RightDragOrbits_WheelZoomsOnlyWhenNotDraggingAndNotOverGui()
        {
            var t = _rig.Table;
            var pose = new Vector3(2.5f, 0.75f, 1.6f);
            t.GetSeatedCameraPose(out _, out var rot0);
            float d0 = DistanceToPaper(t, pose);
            Assert.AreEqual(0.44f, d0, 1e-3f, "default distance 4.4 paper units × 0.1");

            _rig.Frame(new PointerFrame { RightHeld = true, RightDragDelta = new Vector2(50f, 0f) });
            t.GetSeatedCameraPose(out _, out var rot1);
            Assert.Greater(Quaternion.Angle(rot0, rot1), 1f, "right drag orbits");

            _rig.Frame(new PointerFrame { Wheel = 1f, OverGui = true });
            Assert.AreEqual(d0, DistanceToPaper(t, pose), 1e-5f, "no zoom over the GUI");

            _rig.Frame(new PointerFrame { Wheel = 1f });
            float d1 = DistanceToPaper(t, pose);
            Assert.Less(d1, d0, "wheel up zooms in");

            _rig.Press(-1f, 1f);
            _rig.Move(-1f, 1f, wheel: 1f);
            Assert.AreEqual(d1, DistanceToPaper(t, pose), 1e-5f, "while dragging the wheel picks layers");
        }

        [Test]
        public void N4_Materials_AlwaysReturnAMaterialWithAShader()
        {
            var lit = Materials.Lit(Color.red);
            var unlit = Materials.Unlit(new Color(1f, 1f, 1f, 0.5f));
            try
            {
                Assert.IsNotNull(lit);
                Assert.IsNotNull(lit.shader);
                Assert.AreEqual(Color.red, lit.color);
                Assert.IsNotNull(unlit);
                Assert.IsNotNull(unlit.shader);
                Assert.AreEqual(0.5f, unlit.color.a, 1e-6f);
            }
            finally
            {
                Object.Destroy(lit);
                Object.Destroy(unlit);
            }
        }

        [Test]
        public void MeshFactory_Box_IsCentredWithTheGivenSize()
        {
            _mesh = MeshFactory.Box(new Vector3(2f, 1f, 0.5f));
            Assert.AreEqual(24, _mesh.vertexCount);
            Assert.AreEqual(36, _mesh.triangles.Length);
            AssertVector(Vector3.zero, _mesh.bounds.center);
            AssertVector(new Vector3(2f, 1f, 0.5f), _mesh.bounds.size);
            AssertOutward(_mesh, Vector3.zero);
        }

        [Test]
        public void MeshFactory_Cylinder_StandsOnTheOrigin()
        {
            _mesh = MeshFactory.Cylinder(0.3f, 1.2f, 12);
            AssertVector(new Vector3(0f, 0.6f, 0f), _mesh.bounds.center);
            AssertVector(new Vector3(0.6f, 1.2f, 0.6f), _mesh.bounds.size);
            AssertOutward(_mesh, new Vector3(0f, 0.6f, 0f));
        }

        [Test]
        public void MeshFactory_SphereAndCapsule_Sizes()
        {
            _mesh = MeshFactory.Sphere(0.25f);
            AssertVector(Vector3.zero, _mesh.bounds.center);
            AssertVector(Vector3.one * 0.5f, _mesh.bounds.size);
            AssertOutward(_mesh, Vector3.zero);
            Object.Destroy(_mesh);

            _mesh = MeshFactory.Capsule(0.3f, 1.6f);
            AssertVector(Vector3.zero, _mesh.bounds.center);
            AssertVector(new Vector3(0.6f, 1.6f, 0.6f), _mesh.bounds.size);
            AssertOutward(_mesh, Vector3.zero);
        }

        [Test]
        public void MeshFactory_RejectsNonPositiveSizes()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => MeshFactory.Box(new Vector3(1f, 0f, 1f)));
            Assert.Throws<ArgumentOutOfRangeException>(() => MeshFactory.Cylinder(-1f, 1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => MeshFactory.Sphere(float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => MeshFactory.Capsule(0.3f, 0f));
        }

        [Test]
        public void PaperMeshBuilder_InitialSheet_TwoSidedWithEdges()
        {
            _mesh = PaperMeshBuilder.NewMesh("TestPaper");
            float minY = PaperMeshBuilder.Build(_mesh, PaperState.Initial.Facets, null, false, true, 0f, Color.black);
            Assert.AreEqual(4 + 4 + 4 * 4, _mesh.vertexCount, "top + bottom + 4 edge strips");
            Assert.AreEqual(3, _mesh.subMeshCount);
            Assert.AreEqual(6, _mesh.GetTriangles(1).Length, "white back side up (FrontUp false) on top");
            Assert.AreEqual(6, _mesh.GetTriangles(0).Length, "coloured front side underneath");
            Assert.AreEqual(PaperMeshBuilder.LayerBottom(0), minY, 1e-6f);

            var overlay = new OverlayBuilder();
            PaperMeshBuilder.AddCreaseLines(overlay, PaperState.Initial, 0.01f, Color.grey);
            overlay.Apply(_mesh);
            Assert.AreEqual(0, _mesh.vertexCount, "no creases → nothing added");
        }

        private static void AssertVector(Vector3 expected, Vector3 actual)
        {
            Assert.AreEqual(expected.x, actual.x, 1e-4f, "x");
            Assert.AreEqual(expected.y, actual.y, 1e-4f, "y");
            Assert.AreEqual(expected.z, actual.z, 1e-4f, "z");
        }

        /// <summary>Every triangle's front face (Unity: Cross(b − a, c − a)) points away from <paramref name="inside"/>.</summary>
        private static void AssertOutward(Mesh mesh, Vector3 inside)
        {
            var v = mesh.vertices;
            var tris = mesh.triangles;
            for (int i = 0; i < tris.Length; i += 3)
            {
                Vector3 a = v[tris[i]], b = v[tris[i + 1]], c = v[tris[i + 2]];
                var n = Vector3.Cross(b - a, c - a);
                if (n.sqrMagnitude < 1e-12f) continue;   // degenerate pole triangles
                var centre = (a + b + c) / 3f;
                Assert.Greater(Vector3.Dot(n, centre - inside), 0f, "triangle " + (i / 3) + " faces inward");
            }
        }
    }
}
