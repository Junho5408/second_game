using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Origami.Tests.PlayMode.AssetPipeline
{
    /// <summary>
    /// ORIGAMI-017: proves the code-only import pipeline (gen_meta minimal metas + Editor/ArtImportRules) with the CC0
    /// samples in Art/_PipelineSample/Resources/_PipelineSample/ (credits: Art/_PipelineSample/CREDITS.md).
    /// Checks: load from Resources, mesh + material present, materials use the built-in "Standard" shader (matte),
    /// the animated character imports Legacy clips playable by the Animation component without an AnimatorController,
    /// and sampling a clip at two explicit times moves bones. Everything is synchronous (<c>[Test]</c>, explicit
    /// sample times) — nothing depends on frame timing.
    /// </summary>
    public class AssetPipelineTests
    {
        private const string ChairKey = "_PipelineSample/Chair";
        private const string CharacterKey = "_PipelineSample/CasualMale";
        private const string TextureKey = "_PipelineSample/PaperSwatch";
        private const string SampleFolder = "Assets/Game/Art/_PipelineSample/Resources/_PipelineSample/";

        private readonly List<Object> _spawned = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _spawned)
            {
                if (o != null) Object.Destroy(o);
            }
            _spawned.Clear();
        }

        private GameObject Spawn(string key)
        {
            var prefab = Resources.Load<GameObject>(key);
            Assert.IsNotNull(prefab, $"Resources.Load<GameObject>(\"{key}\") returned null — model not imported as a model?");
            var go = Object.Instantiate(prefab);
            _spawned.Add(go);
            return go;
        }

        private static Bounds RendererBounds(GameObject go)
        {
            var renderers = go.GetComponentsInChildren<Renderer>();
            var b = renderers[0].bounds;
            foreach (var r in renderers) b.Encapsulate(r.bounds);
            return b;
        }

        private static void AssertMatteStandard(Material[] materials, string what)
        {
            Assert.That(materials.Length, Is.GreaterThan(0), what + ": no materials");
            foreach (var m in materials)
            {
                Assert.IsNotNull(m, what + ": null material slot");
                Assert.IsNotNull(m.shader, what + ": material without shader");
                Assert.AreEqual("Standard", m.shader.name, what + ": ArtImportRules should set the built-in Standard shader");
                if (m.HasProperty("_Metallic")) Assert.AreEqual(0f, m.GetFloat("_Metallic"), 1e-4f, what + " metallic");
                if (m.HasProperty("_Glossiness")) Assert.That(m.GetFloat("_Glossiness"), Is.LessThanOrEqualTo(0.2f + 1e-4f), what + " smoothness");
#if UNITY_EDITOR
                // Embedded in the model (never extracted to new .mat assets).
                var path = UnityEditor.AssetDatabase.GetAssetPath(m);
                Assert.IsTrue(path.StartsWith(SampleFolder, System.StringComparison.Ordinal) && path.EndsWith(".fbx", System.StringComparison.Ordinal),
                    what + ": material should be a sub-asset of the model, was \"" + path + "\"");
#endif
            }
        }

        [Test]
        public void StaticModel_LoadsFromResources_WithMeshAndMaterial()
        {
            var go = Spawn(ChairKey);
            var filters = go.GetComponentsInChildren<MeshFilter>();
            Assert.That(filters.Length, Is.GreaterThan(0), "chair has no MeshFilter");
            foreach (var f in filters)
            {
                Assert.IsNotNull(f.sharedMesh, "MeshFilter without mesh");
                Assert.That(f.sharedMesh.vertexCount, Is.GreaterThan(0));
                var r = f.GetComponent<MeshRenderer>();
                Assert.IsNotNull(r, "MeshFilter without MeshRenderer");
                AssertMatteStandard(r.sharedMaterials, "chair");
            }
            var b = RendererBounds(go);
            Assert.That(b.size.x > 0f && b.size.y > 0f && b.size.z > 0f, "chair bounds are empty: " + b);
            TestContext.WriteLine($"Chair bounds size (m, file scale): {b.size}, center {b.center}");
        }

        [Test]
        public void Character_LoadsWithSkinnedMeshBonesAndMaterials()
        {
            var go = Spawn(CharacterKey);
            var skins = go.GetComponentsInChildren<SkinnedMeshRenderer>();
            Assert.That(skins.Length, Is.GreaterThan(0), "character has no SkinnedMeshRenderer");
            foreach (var s in skins)
            {
                Assert.IsNotNull(s.sharedMesh, "SkinnedMeshRenderer without mesh");
                Assert.That(s.sharedMesh.vertexCount, Is.GreaterThan(0));
                Assert.That(s.bones.Length, Is.GreaterThan(0), "skinned mesh without bones");
                AssertMatteStandard(s.sharedMaterials, "character");
            }
            var b = RendererBounds(go);
            Assert.That(b.size.y, Is.GreaterThan(0f), "character bounds are empty: " + b);
            TestContext.WriteLine($"CasualMale bounds size (m, file scale): {b.size}, center {b.center}");
        }

        [Test]
        public void Character_HasLegacyClipsOnAnimationComponent_WithoutAnimator()
        {
            var go = Spawn(CharacterKey);
            var anim = go.GetComponent<Animation>();
            Assert.IsNotNull(anim, "Legacy import should put an Animation component on the model root");
            Assert.IsNull(go.GetComponentInChildren<Animator>(), "no Animator expected (Legacy, no AnimatorController)");
            var names = ClipNames(anim);
            TestContext.WriteLine("Clips: " + string.Join(", ", names));
            Assert.That(names.Count, Is.GreaterThanOrEqualTo(2));
            foreach (AnimationState state in anim)
            {
                Assert.IsTrue(state.clip.legacy, state.name + " is not a legacy clip");
                Assert.That(state.clip.length, Is.GreaterThan(0f), state.name + " has zero length");
            }
            Assert.IsNotNull(FindClip(anim, "Walk"), "no Walk clip: " + string.Join(", ", names));
            Assert.IsNotNull(FindClip(anim, "Idle"), "no Idle clip: " + string.Join(", ", names));
        }

        [Test]
        public void Character_ClipNames_LoseArmaturePrefix()
        {
            var go = Spawn(CharacterKey);
            var anim = go.GetComponent<Animation>();
            Assert.IsNotNull(anim);
            var names = ClipNames(anim);
            CollectionAssert.Contains(names, "Walk", "ArtImportRules should rename \"CharacterArmature|Walk\" to \"Walk\"");
            Assert.IsFalse(names.Any(n => n.Contains("|")), "clip names still contain '|': " + string.Join(", ", names));
        }

        [Test]
        public void Character_SamplingWalkAtTwoTimes_MovesBones_ViaAnimationComponent()
        {
            var go = Spawn(CharacterKey);
            var anim = go.GetComponent<Animation>();
            Assert.IsNotNull(anim);
            var walk = FindClip(anim, "Walk");
            Assert.IsNotNull(walk, "no Walk clip");
            var state = anim[walk];

            // Runtime convention (CODING_CONVENTION "Art assets"): speed 0, time set from dt, Sample().
            anim.Stop();
            state.enabled = true;
            state.weight = 1f;
            state.speed = 0f;

            state.time = 0.25f * state.length;
            anim.Sample();
            var a = Pose(go.transform);
            state.time = 0.75f * state.length;
            anim.Sample();
            var b = Pose(go.transform);

            AssertPoseChanged(a, b, "Animation.Sample");
        }

        [Test]
        public void Character_ClipSampleAnimation_MovesBones()
        {
            var go = Spawn(CharacterKey);
            var anim = go.GetComponent<Animation>();
            Assert.IsNotNull(anim);
            var walk = FindClip(anim, "Walk");
            Assert.IsNotNull(walk, "no Walk clip");
            var clip = anim[walk].clip;
            anim.Stop();

            clip.SampleAnimation(go, 0.25f * clip.length);
            var a = Pose(go.transform);
            clip.SampleAnimation(go, 0.75f * clip.length);
            var b = Pose(go.transform);

            AssertPoseChanged(a, b, "AnimationClip.SampleAnimation");
        }

        [Test]
        public void Texture_LoadsFromResources_AsTexture2D()
        {
            var tex = Resources.Load<Texture2D>(TextureKey);
            Assert.IsNotNull(tex, $"Resources.Load<Texture2D>(\"{TextureKey}\") returned null — PNG not imported as a texture?");
            Assert.AreEqual(256, tex.width);
            Assert.AreEqual(256, tex.height);
            Assert.That(tex.mipmapCount, Is.GreaterThan(1), "ArtImportRules enables mipmaps");
            Assert.AreEqual(TextureWrapMode.Repeat, tex.wrapMode);
        }

        // ---- helpers ----

        private static List<string> ClipNames(Animation anim)
        {
            var names = new List<string>();
            foreach (AnimationState s in anim) names.Add(s.name);
            return names;
        }

        /// <summary>Clip state name equal to <paramref name="key"/> or ending in "|key" (robust to the rename rule).</summary>
        private static string FindClip(Animation anim, string key)
        {
            foreach (AnimationState s in anim)
            {
                if (s.name == key || s.name.EndsWith("|" + key, System.StringComparison.Ordinal)) return s.name;
            }
            return null;
        }

        private static Dictionary<Transform, (Vector3 pos, Quaternion rot)> Pose(Transform root)
        {
            var pose = new Dictionary<Transform, (Vector3, Quaternion)>();
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t != root) pose[t] = (t.localPosition, t.localRotation);
            }
            return pose;
        }

        private static void AssertPoseChanged(
            Dictionary<Transform, (Vector3 pos, Quaternion rot)> a,
            Dictionary<Transform, (Vector3 pos, Quaternion rot)> b,
            string how)
        {
            var maxAngle = 0f;
            var maxMove = 0f;
            string bone = null;
            foreach (var kv in a)
            {
                var other = b[kv.Key];
                var angle = Quaternion.Angle(kv.Value.rot, other.rot);
                var move = (kv.Value.pos - other.pos).magnitude;
                if (angle > maxAngle)
                {
                    maxAngle = angle;
                    bone = kv.Key.name;
                }
                maxMove = Mathf.Max(maxMove, move);
            }
            TestContext.WriteLine($"{how}: max bone rotation change {maxAngle:F2} deg ({bone}), max local move {maxMove:F4}");
            Assert.That(maxAngle > 1f || maxMove > 1e-3f,
                $"{how}: sampling Walk at 25% and 75% did not move any bone (max {maxAngle} deg, {maxMove})");
        }
    }
}
