using System;
using UnityEngine;

namespace Fold3DSpike.Runtime.Systems
{
    /// <summary>
    /// Code-only materials that survive build shader stripping (no material assets in this project).
    /// Lit: "Legacy Shaders/Diffuse" (in the default Always Included Shaders list of the built-in pipeline),
    /// else the shader of a temporary primitive's built-in material, else "Standard", else the unlit fallback.
    /// Unlit/transparent: "Sprites/Default" (also always included; vertex colour * _Color, no culling).
    /// Never logs; never returns a material with the error shader.
    /// </summary>
    public static class SpikeMaterials
    {
        private static Shader s_lit;
        private static Shader s_unlit;

        private static bool Usable(Shader s)
        {
            return s != null && s.isSupported && s.name != "Hidden/InternalErrorShader";
        }

        public static Shader LitShader
        {
            get
            {
                if (s_lit != null) return s_lit;
                var s = Shader.Find("Legacy Shaders/Diffuse");
                if (!Usable(s)) s = PrimitiveShader();
                if (!Usable(s)) s = Shader.Find("Standard");
                if (!Usable(s)) s = UnlitShader;
                s_lit = s;
                return s_lit;
            }
        }

        public static Shader UnlitShader
        {
            get
            {
                if (s_unlit != null) return s_unlit;
                var s = Shader.Find("Sprites/Default");
                if (!Usable(s)) s = Shader.Find("UI/Default");
                if (!Usable(s)) s = Shader.Find("Legacy Shaders/Diffuse");
                if (!Usable(s)) s = PrimitiveShader();
                s_unlit = s;
                return s_unlit;
            }
        }

        /// <summary>Opaque lit material (matte paper look).</summary>
        public static Material Lit(Color color)
        {
            var m = new Material(LitShader) { color = color };
            m.hideFlags = HideFlags.DontSave;
            return m;
        }

        /// <summary>Unlit, alpha-blended material; multiply with mesh vertex colours.</summary>
        public static Material Unlit(Color color)
        {
            var m = new Material(UnlitShader) { color = color };
            m.hideFlags = HideFlags.DontSave;
            return m;
        }

        private static Shader PrimitiveShader()
        {
            GameObject go = null;
            try
            {
                go = GameObject.CreatePrimitive(PrimitiveType.Quad);
                go.SetActive(false);
                var r = go.GetComponent<MeshRenderer>();
                var mat = r != null ? r.sharedMaterial : null;
                return mat != null ? mat.shader : null;
            }
            catch (Exception)
            {
                return null;
            }
            finally
            {
                if (go != null) UnityEngine.Object.Destroy(go);
            }
        }
    }
}
