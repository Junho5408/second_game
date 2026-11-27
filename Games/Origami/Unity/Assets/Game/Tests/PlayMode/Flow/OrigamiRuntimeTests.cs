using System.Collections;
using NUnit.Framework;
using Origami.Core.Content;
using Origami.Core.Flow;
using Origami.Runtime.Gameplay;
using Origami.Runtime.UI;
using UnityEngine;
using UnityEngine.TestTools;

namespace Origami.Tests.PlayMode.Flow
{
    /// <summary>
    /// ORIGAMI-005: the runtime <c>[Origami]</c> object is created once by the AfterSceneLoad hooks, carries the
    /// controller and a bound view, and code-generated graphics exist without assets (F-007 AC-07).
    /// The UnityTest only asserts timing-independent state: nothing drives the runtime instance (no real input in
    /// batchmode, tests never call it), so it stays on the initial Workbench in every frame. It yields twice so
    /// that a full Update → LateUpdate has run before asserting the view's lines.
    /// </summary>
    public class OrigamiRuntimeTests
    {
        private GameObject _manual;

        [TearDown]
        public void TearDown()
        {
            if (_manual != null) Object.Destroy(_manual);
            _manual = null;
        }

        private static int CountRuntimeControllers()
        {
            int count = 0;
            foreach (var c in Resources.FindObjectsOfTypeAll<OrigamiController>())
            {
                if (c != null && c.gameObject.scene.IsValid() && c.gameObject.name == OrigamiController.ObjectName) count++;
            }
            return count;
        }

        [UnityTest]
        public IEnumerator RuntimeInstance_IsCreatedOnce_WithBoundView()
        {
            yield return null;
            yield return null;

            Assert.AreEqual(1, CountRuntimeControllers(), "exactly one runtime [Origami] controller");
            var go = GameObject.Find(OrigamiController.ObjectName);
            Assert.IsNotNull(go, "runtime [Origami] object missing");
            var controller = go.GetComponent<OrigamiController>();
            Assert.IsNotNull(controller);
            Assert.AreSame(controller, OrigamiController.Instance);
            Assert.AreSame(controller, OrigamiController.EnsureInstance(), "EnsureInstance must not duplicate");
            Assert.AreEqual(1, CountRuntimeControllers(), "still one after EnsureInstance");
            Assert.AreEqual(1, go.GetComponents<OrigamiController>().Length);

            var views = go.GetComponents<OrigamiView>();
            Assert.AreEqual(1, views.Length, "exactly one OrigamiView on the runtime object");
            Assert.AreSame(controller, views[0].Source);

            Assert.AreEqual(FlowScreen.Workbench, controller.Flow.Screen);
            Assert.GreaterOrEqual(views[0].Lines.Count, 2);
            Assert.AreEqual("일차 1/7   돈 20원   시도 5/5", views[0].Lines[0]);
            Assert.AreEqual("오늘의 인기 작품: 종이비행기", views[0].Lines[1]);

            // A manually added controller never replaces the runtime instance.
            _manual = new GameObject("TestOrigamiManual");
            _manual.AddComponent<OrigamiController>();
            Assert.AreSame(controller, OrigamiController.Instance);
            Assert.AreSame(controller, OrigamiController.EnsureInstance());
            Assert.AreEqual(1, CountRuntimeControllers());
        }

        // ---- F-007 AC-07: code-generated graphics ----

        [Test]
        public void ModelIcons_FiveModels_DistinctCodeGeneratedTextures()
        {
            var textures = new Texture2D[OrigamiCatalog.Models.Count];
            for (int i = 0; i < textures.Length; i++)
            {
                var id = OrigamiCatalog.Models[i].Id;
                var tex = ModelIcons.Get(id);
                Assert.IsNotNull(tex, id);
                Assert.AreEqual(ModelIcons.Size, tex.width, id);
                Assert.AreEqual(ModelIcons.Size, tex.height, id);
                Assert.AreSame(tex, ModelIcons.Get(id), "cached: " + id);
                textures[i] = tex;
            }

            for (int i = 0; i < textures.Length; i++)
            {
                for (int j = i + 1; j < textures.Length; j++)
                {
                    Assert.AreNotSame(textures[i], textures[j]);
                    Assert.IsFalse(SamePixels(textures[i], textures[j]),
                        OrigamiCatalog.Models[i].Id + " and " + OrigamiCatalog.Models[j].Id + " must look different");
                }
            }
        }

        [Test]
        public void ModelIcons_UnknownOrNull_ReturnsNull()
        {
            Assert.IsNull(ModelIcons.Get(null));
            Assert.IsNull(ModelIcons.Get(""));
            Assert.IsNull(ModelIcons.Get("unicorn"));
        }

        [Test]
        public void ModelIcons_Paper_IsSolidSquare()
        {
            var paper = ModelIcons.Paper;
            Assert.IsNotNull(paper);
            Assert.AreSame(paper, ModelIcons.Paper);
            var pixels = paper.GetPixels32();
            Assert.AreEqual(ModelIcons.Size * ModelIcons.Size, pixels.Length);
            for (int i = 1; i < pixels.Length; i++)
            {
                Assert.AreEqual(pixels[0], pixels[i], "pixel " + i);
            }
            Assert.AreEqual(255, pixels[0].a);
        }

        private static bool SamePixels(Texture2D a, Texture2D b)
        {
            var pa = a.GetPixels32();
            var pb = b.GetPixels32();
            if (pa.Length != pb.Length) return false;
            for (int i = 0; i < pa.Length; i++)
            {
                if (!pa[i].Equals(pb[i])) return false;
            }
            return true;
        }
    }
}
