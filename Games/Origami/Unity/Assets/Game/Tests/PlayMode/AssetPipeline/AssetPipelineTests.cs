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
        private const string TexturePath = SampleFolder + "PaperSwatch.png";

        // Values forced by Editor/ArtImportRules (Version 2). They differ from Unity's import defaults
        // (smoothness from FBX shininess, e.g. 0.31; aniso 1; max size 2048), so passing proves the rules ran.
        private const float RuleSmoothness = 0.15f;
        private const int RuleAnisoLevel = 2;
        private const int RuleMaxTextureSize = 1024;

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
                TestContext.WriteLine($"{what} material '{m.name}': shader {m.shader.name}, " +
                    $"_Metallic {(m.HasProperty("_Metallic") ? m.GetFloat("_Metallic") : float.NaN)}, " +
                    $"_Glossiness {(m.HasProperty("_Glossiness") ? m.GetFloat("_Glossiness") : float.NaN)}, " +
                    $"_GlossMapScale {(m.HasProperty("_GlossMapScale") ? m.GetFloat("_GlossMapScale") : float.NaN)}");
                Assert.IsTrue(m.HasProperty("_Metallic") && m.HasProperty("_Glossiness"), what + ": Standard material without _Metallic/_Glossiness");
                Assert.AreEqual(0f, m.GetFloat("_Metallic"), 1e-4f, what + " metallic");
                Assert.AreEqual(RuleSmoothness, m.GetFloat("_Glossiness"), 1e-4f, what + " smoothness (ArtImportRules sets exactly " + RuleSmoothness + ")");
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
            AssertModelImporterRules("Chair.fbx");
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
            AssertModelImporterRules("CasualMale.fbx");
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
            if (tex == null)
            {
                // ORIGAMI-017 F-2: CI run 46089349441 returned null here although TextureImporter imported the PNG.
                // Print everything that can explain it before failing, so the next CI run settles the cause.
                var diag = TextureDiagnostics();
                Debug.LogWarning("[AssetPipelineTests] PaperSwatch diagnostics:\n" + diag);
                TestContext.WriteLine(diag);
                Assert.Fail($"Resources.Load<Texture2D>(\"{TextureKey}\") returned null.\n{diag}");
            }
            TestContext.WriteLine($"PaperSwatch: {tex.width}x{tex.height}, mips {tex.mipmapCount}, wrap {tex.wrapMode}, " +
                $"filter {tex.filterMode}, aniso {tex.anisoLevel}, format {tex.format}");
            Assert.AreEqual(256, tex.width);
            Assert.AreEqual(256, tex.height);
            Assert.That(tex.mipmapCount, Is.GreaterThan(1), "ArtImportRules enables mipmaps");
            Assert.AreEqual(TextureWrapMode.Repeat, tex.wrapMode);
            Assert.AreEqual(FilterMode.Bilinear, tex.filterMode);
            Assert.AreEqual(RuleAnisoLevel, tex.anisoLevel, "ArtImportRules sets anisoLevel (Unity default 1)");
#if UNITY_EDITOR
            var ti = UnityEditor.AssetImporter.GetAtPath(TexturePath) as UnityEditor.TextureImporter;
            Assert.IsNotNull(ti, "no TextureImporter for " + TexturePath);
            Assert.AreEqual(UnityEditor.TextureImporterType.Default, ti.textureType);
            Assert.AreEqual(UnityEditor.TextureImporterShape.Texture2D, ti.textureShape);
            Assert.AreEqual(RuleMaxTextureSize, ti.maxTextureSize, "ArtImportRules caps textures at 1024 (Unity default 2048)");
            Assert.AreEqual(RuleAnisoLevel, ti.anisoLevel);
#endif
        }

        // ---- helpers ----

        private static void AssertModelImporterRules(string file)
        {
#if UNITY_EDITOR
            var path = SampleFolder + file;
            var mi = UnityEditor.AssetImporter.GetAtPath(path) as UnityEditor.ModelImporter;
            Assert.IsNotNull(mi, "no ModelImporter for " + path);
            Assert.AreEqual(UnityEditor.ModelImporterAnimationType.Legacy, mi.animationType, file + " animationType");
            Assert.AreEqual(UnityEditor.ModelImporterMaterialImportMode.ImportViaMaterialDescription, mi.materialImportMode, file + " materialImportMode");
            Assert.AreEqual(UnityEditor.ModelImporterMaterialLocation.InPrefab, mi.materialLocation, file + " materialLocation");
            Assert.IsFalse(mi.addCollider, file + " addCollider");
            Assert.IsFalse(mi.importCameras || mi.importLights, file + " cameras/lights");
#endif
        }

        private static string TextureDiagnostics()
        {
            var sb = new System.Text.StringBuilder();
            var untyped = Resources.Load(TextureKey);
            sb.AppendLine($"Resources.Load(\"{TextureKey}\") = {Describe(untyped)}");
            sb.AppendLine($"Resources.Load<Texture>(...) = {Describe(Resources.Load<Texture>(TextureKey))}");
            sb.AppendLine($"Resources.Load<Sprite>(...) = {Describe(Resources.Load<Sprite>(TextureKey))}");
            var all = Resources.LoadAll("_PipelineSample");
            sb.AppendLine($"Resources.LoadAll(\"_PipelineSample\"): {all.Length} objects: " +
                string.Join(", ", all.Select(Describe)));
#if UNITY_EDITOR
            var full = System.IO.Path.GetFullPath(TexturePath);
            if (System.IO.File.Exists(full))
            {
                var bytes = System.IO.File.ReadAllBytes(full);
                var head = System.Text.Encoding.ASCII.GetString(bytes, 0, System.Math.Min(48, bytes.Length))
                    .Replace("\r", "\\r").Replace("\n", "\\n");
                sb.AppendLine($"file {full}: {bytes.Length} bytes, head \"{head}\"");
            }
            else
            {
                sb.AppendLine($"file {full}: MISSING");
            }
            var importer = UnityEditor.AssetImporter.GetAtPath(TexturePath);
            sb.AppendLine($"importer: {(importer == null ? "null" : importer.GetType().FullName)}");
            if (importer is UnityEditor.TextureImporter ti)
            {
                sb.AppendLine($"  textureType {ti.textureType}, shape {ti.textureShape}, spriteMode {ti.spriteImportMode}, " +
                    $"maxSize {ti.maxTextureSize}, aniso {ti.anisoLevel}, mips {ti.mipmapEnabled}, compression {ti.textureCompression}");
            }
            sb.AppendLine($"main asset type: {UnityEditor.AssetDatabase.GetMainAssetTypeAtPath(TexturePath)}");
            var subs = UnityEditor.AssetDatabase.LoadAllAssetsAtPath(TexturePath);
            sb.AppendLine($"LoadAllAssetsAtPath: {subs.Length} objects: " + string.Join(", ", subs.Select(Describe)));
            sb.AppendLine($"defaultBehaviorMode: {UnityEditor.EditorSettings.defaultBehaviorMode}");
            sb.AppendLine($"Application.isPlaying {Application.isPlaying}, graphics {SystemInfo.graphicsDeviceType}");
#endif
            return sb.ToString();
        }

        private static string Describe(Object o) => o == null ? "null" : $"{o.GetType().Name} '{o.name}'";

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
