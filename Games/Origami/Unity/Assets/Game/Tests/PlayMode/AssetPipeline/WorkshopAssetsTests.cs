using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Origami.Runtime.Systems;
using UnityEngine;

namespace Origami.Tests.PlayMode.AssetPipeline
{
    /// <summary>
    /// ORIGAMI-019 (QA ORIGAMI-018 recommendation): every Resources key of <c>Art/ASSETS.md</c> "Keys for tests" loads —
    /// 19 models with at least one Renderer, the Crafter with its Legacy clips, the 2 surface textures as Texture2D ≤ 1024 px,
    /// the hand-edited Window with its <c>glass</c> + <c>wood</c> slots. The keys are written out here (not taken from
    /// <see cref="ArtKeys"/>) so a typo in either list fails. Synchronous <c>[Test]</c>s only.
    /// </summary>
    public class WorkshopAssetsTests
    {
        /// <summary>ASSETS.md "Keys for tests", model part (18 workshop models + the character).</summary>
        public static readonly string[] ModelKeys =
        {
            "Workshop/Table", "Workshop/Chair", "Workshop/Bookshelf", "Workshop/Books", "Workshop/DisplayShelf", "Workshop/Bed",
            "Workshop/Nightstand", "Workshop/TrashCan", "Workshop/Door", "Workshop/Window", "Workshop/LampTable",
            "Workshop/LampFloor", "Workshop/Rug", "Workshop/PottedPlant", "Workshop/PlantSmall", "Workshop/Box",
            "Workshop/Paneling", "Workshop/CoatRack", "Characters/Crafter",
        };

        public static readonly string[] TextureKeys = { "Surfaces/WoodFloor", "Surfaces/PlasterWall" };

        private readonly List<Object> _spawned = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _spawned)
                if (o != null) Object.Destroy(o);
            _spawned.Clear();
        }

        [Test]
        public void ArtKeys_ListsExactlyTheAssetsMdKeys()
        {
            Assert.AreEqual(21, ModelKeys.Length + TextureKeys.Length, "ASSETS.md lists 21 keys");
            CollectionAssert.AreEqual(ModelKeys, ArtKeys.Models.ToArray());
            CollectionAssert.AreEqual(TextureKeys, ArtKeys.Textures.ToArray());
        }

        [TestCaseSource(nameof(ModelKeys))]
        public void Model_LoadsFromResources_WithARenderer(string key)
        {
            var prefab = Resources.Load<GameObject>(key);
            Assert.IsNotNull(prefab, $"Resources.Load<GameObject>(\"{key}\") returned null");
            var go = Object.Instantiate(prefab);
            _spawned.Add(go);
            var renderers = go.GetComponentsInChildren<Renderer>(true);
            Assert.That(renderers.Length, Is.GreaterThanOrEqualTo(1), key + ": no Renderer");
            foreach (var r in renderers)
            {
                Assert.That(r.sharedMaterials.Length, Is.GreaterThan(0), key + ": renderer without materials");
                foreach (var m in r.sharedMaterials) Assert.IsNotNull(m, key + ": empty material slot");
            }
            Assert.IsTrue(ModelFit.TryWorldBounds(go, out var b), key + ": no measurable bounds");
            Assert.That(b.size.x > 0f && b.size.y > 0f && b.size.z > 0f, key + ": empty bounds " + b);
            TestContext.WriteLine($"{key}: {renderers.Length} renderer(s), native size {b.size.ToString("F3")}, min {b.min.ToString("F3")}");
        }

        [TestCaseSource(nameof(TextureKeys))]
        public void Texture_LoadsAsTexture2D_AtMost1024(string key)
        {
            var tex = Resources.Load<Texture2D>(key);
            if (tex == null)
            {
                var any = Resources.Load(key);
                Assert.Fail($"Resources.Load<Texture2D>(\"{key}\") returned null; untyped load = " +
                    (any == null ? "null" : any.GetType().Name + " '" + any.name + "'"));
            }
            TestContext.WriteLine($"{key}: {tex.width}x{tex.height}, format {tex.format}, wrap {tex.wrapMode}, mips {tex.mipmapCount}");
            Assert.That(tex.width, Is.InRange(1, 1024));
            Assert.That(tex.height, Is.InRange(1, 1024));
            Assert.AreEqual(TextureWrapMode.Repeat, tex.wrapMode, "ArtImportRules: Repeat");
        }

        [Test]
        public void Crafter_HasAnimation_WithIdleWalkSitDownStandUp()
        {
            var prefab = Resources.Load<GameObject>("Characters/Crafter");
            Assert.IsNotNull(prefab);
            var go = Object.Instantiate(prefab);
            _spawned.Add(go);
            var anim = go.GetComponentInChildren<Animation>(true);
            Assert.IsNotNull(anim, "Legacy Animation component on the model");
            var names = new List<string>();
            foreach (AnimationState s in anim) names.Add(s.name);
            TestContext.WriteLine("Crafter clips: " + string.Join(", ", names));
            foreach (var clip in new[] { "Idle", "Walk", "SitDown", "StandUp" })
            {
                var state = anim[clip];
                Assert.IsNotNull(state, "missing clip " + clip + " in " + string.Join(", ", names));
                Assert.That(state.length, Is.GreaterThan(0f), clip + " length");
                Assert.IsTrue(state.clip.legacy, clip + " is legacy");
                TestContext.WriteLine($"  {clip}: {state.length:F3} s");
            }
            Assert.That(go.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length, Is.GreaterThan(0));
        }

        [Test]
        public void Window_HasOneMeshRenderer_WithGlassAndWoodMaterials()
        {
            var prefab = Resources.Load<GameObject>("Workshop/Window");
            Assert.IsNotNull(prefab);
            var go = Object.Instantiate(prefab);
            _spawned.Add(go);
            var renderers = go.GetComponentsInChildren<MeshRenderer>(true);
            Assert.AreEqual(1, renderers.Length, "Window.fbx has one mesh (extracted from wallWindow)");
            var names = renderers[0].sharedMaterials.Select(m => m != null ? m.name : "null").ToList();
            TestContext.WriteLine("Window materials: " + string.Join(", ", names));
            Assert.IsTrue(names.Any(n => n.IndexOf("glass", System.StringComparison.OrdinalIgnoreCase) >= 0), "glass slot");
            Assert.IsTrue(names.Any(n => n.IndexOf("wood", System.StringComparison.OrdinalIgnoreCase) >= 0), "wood slot");
        }
    }
}
