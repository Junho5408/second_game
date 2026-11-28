using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Origami.EditorTools
{
    /// <summary>
    /// Fixes the import settings of external art in code (ORIGAMI-017, ARCHITECTURE §14, CODING_CONVENTION "Art assets").
    /// Nobody opens the Unity Editor, so .meta files only carry a GUID (gen_meta.py writes a minimal meta for
    /// models/textures/audio/fonts) and every setting that matters is forced here on each import.
    ///
    /// Applies to every model and texture under <see cref="Root"/> (Assets/Game/):
    /// <list type="bullet">
    /// <item>Models: file units → metres (<c>useFileScale</c>, <c>globalScale</c> 1); no cameras/lights;
    /// materials embedded in the model (never extracted — extracted .mat files would be new, uncommitted assets
    /// with random GUIDs in CI); <b>Legacy</b> animation type so clips play with the <c>Animation</c> component and
    /// no AnimatorController asset; clip names lose the "Armature|" prefix of Blender exports.</item>
    /// <item>Materials created by a model import: built-in "Standard" shader, metallic 0, smoothness ≤ 0.2
    /// (one matte, cozy look for every kit).</item>
    /// <item>Textures: colour textures sRGB + mipmaps, max 1024 px, normal-compressed; files ending in
    /// "_Normal"/"_NormalGL"/"_nrm" import as normal maps.</item>
    /// </list>
    /// Shaders: the build includes "Standard" because the embedded materials of models in a Resources folder
    /// reference it (only the variants those materials use). It is deliberately not added to Always Included
    /// Shaders (that compiles every Standard variant). Runtime code never calls Shader.Find for model shaders.
    /// Bump <see cref="Version"/> whenever a rule changes so Unity reimports affected assets.
    /// </summary>
    public sealed class ArtImportRules : AssetPostprocessor
    {
        /// <summary>Asset path prefix the rules apply to.</summary>
        public const string Root = "Assets/Game/";

        /// <summary>Postprocessor version (see class remarks).</summary>
        public const uint Version = 1;

        /// <summary>Largest texture size kept in the build (repository/build size budget).</summary>
        public const int MaxTextureSize = 1024;

        /// <summary>Upper bound of the Standard smoothness for imported materials.</summary>
        public const float MaxSmoothness = 0.2f;

        private const string StandardShader = "Standard";
        private const string FallbackShader = "Legacy Shaders/Diffuse";

        public override uint GetVersion() => Version;

        private bool Applies => assetPath.StartsWith(Root, System.StringComparison.Ordinal);

        private void OnPreprocessModel()
        {
            if (!Applies) return;
            var mi = assetImporter as ModelImporter;
            if (mi == null) return;

            mi.globalScale = 1f;
            mi.useFileScale = true;
            mi.importCameras = false;
            mi.importLights = false;
            mi.importVisibility = false;
            mi.addCollider = false; // no 3D physics module in this project (ARCHITECTURE §7)
            mi.isReadable = false;
            mi.meshCompression = ModelImporterMeshCompression.Off;

            mi.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
            mi.materialLocation = ModelImporterMaterialLocation.InPrefab;

            mi.animationType = ModelImporterAnimationType.Legacy;
            mi.importAnimation = true;
        }

        private void OnPreprocessAnimation()
        {
            if (!Applies) return;
            var mi = assetImporter as ModelImporter;
            if (mi == null) return;
            if (mi.clipAnimations != null && mi.clipAnimations.Length > 0) return; // explicit clips win

            var clips = mi.defaultClipAnimations;
            if (clips == null || clips.Length == 0) return;

            var seen = new HashSet<string>();
            var changed = false;
            foreach (var clip in clips)
            {
                var shortName = ShortClipName(clip.name);
                if (!seen.Add(shortName)) return; // two takes would collide: keep the original names
                if (shortName != clip.name) changed = true;
            }
            if (!changed) return;
            foreach (var clip in clips) clip.name = ShortClipName(clip.name);
            mi.clipAnimations = clips;
        }

        private void OnPostprocessMaterial(Material material)
        {
            if (!Applies || material == null) return;
            var shader = Shader.Find(StandardShader);
            if (shader == null) shader = Shader.Find(FallbackShader);
            if (shader != null && material.shader != shader)
            {
                // Keep the colour (and main texture) chosen by the model file when switching shaders.
                var color = material.HasProperty("_Color") ? material.color : Color.white;
                var tex = material.HasProperty("_MainTex") ? material.mainTexture : null;
                material.shader = shader;
                material.color = color;
                if (tex != null) material.mainTexture = tex;
            }
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0f);
            if (material.HasProperty("_Glossiness"))
            {
                material.SetFloat("_Glossiness", Mathf.Min(material.GetFloat("_Glossiness"), MaxSmoothness));
            }
        }

        private void OnPreprocessTexture()
        {
            if (!Applies) return;
            var ti = assetImporter as TextureImporter;
            if (ti == null) return;

            var normal = IsNormalMapPath(assetPath);
            ti.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            ti.sRGBTexture = !normal;
            ti.mipmapEnabled = true;
            ti.isReadable = false;
            ti.maxTextureSize = MaxTextureSize;
            ti.textureCompression = TextureImporterCompression.Compressed;
            ti.filterMode = FilterMode.Bilinear;
            ti.wrapMode = TextureWrapMode.Repeat;
        }

        /// <summary>"CharacterArmature|Walk" → "Walk" (text after the last '|'); other names unchanged.</summary>
        public static string ShortClipName(string name)
        {
            if (string.IsNullOrEmpty(name)) return name;
            var i = name.LastIndexOf('|');
            return i >= 0 && i < name.Length - 1 ? name.Substring(i + 1) : name;
        }

        /// <summary>True for texture files named like a normal map (…_Normal, …_NormalGL, …_nrm).</summary>
        public static bool IsNormalMapPath(string path)
        {
            var file = System.IO.Path.GetFileNameWithoutExtension(path) ?? string.Empty;
            return file.EndsWith("_Normal", System.StringComparison.OrdinalIgnoreCase)
                || file.EndsWith("_NormalGL", System.StringComparison.OrdinalIgnoreCase)
                || file.EndsWith("_nrm", System.StringComparison.OrdinalIgnoreCase);
        }
    }
}
