using UnityEngine;

namespace Origami.Runtime.Systems
{
    /// <summary>
    /// Code-only materials (C-190, ported from Fold3DSpike <c>SpikeMaterials</c>; no material assets in this project).
    /// Lit: "Legacy Shaders/Diffuse" → "Standard" → the unlit shader. Unlit (alpha blended, vertex colour × colour):
    /// "Sprites/Default" → "UI/Default" → "Legacy Shaders/Diffuse".
    /// The spike's <c>CreatePrimitive</c> fallback is removed (no 3D physics module, ARCHITECTURE §7).
    /// Never logs. If no shader of a chain is usable (e.g. a batch-mode run without graphics reports shaders as
    /// unsupported), the first shader of the chain that exists is used, then "Hidden/InternalErrorShader" (review
    /// ORIGAMI-008 N-4) — so <see cref="Lit"/>/<see cref="Unlit"/> always return a material and never throw.
    /// The caller owns the returned material and destroys it in its <c>OnDestroy</c>.
    /// </summary>
    public static class Materials
    {
        private static readonly string[] LitChain = { "Legacy Shaders/Diffuse", "Standard" };
        private static readonly string[] UnlitChain = { "Sprites/Default", "UI/Default", "Legacy Shaders/Diffuse" };
        private const string ErrorShaderName = "Hidden/InternalErrorShader";

        private static Shader s_lit;
        private static Shader s_unlit;

        /// <summary>Opaque lit material (matte paper / furniture look) with <paramref name="color"/>.</summary>
        public static Material Lit(Color color) => Create(LitShader(), color);

        /// <summary>Unlit alpha-blended material; multiplies mesh vertex colours by <paramref name="color"/>.</summary>
        public static Material Unlit(Color color) => Create(UnlitShader(), color);

        private static Material Create(Shader shader, Color color)
        {
            // The error shader is part of every player, so shader is not null in practice.
            var m = new Material(shader != null ? shader : Shader.Find(ErrorShaderName));
            m.color = color;
            m.hideFlags = HideFlags.DontSave;
            return m;
        }

        private static bool Usable(Shader s) => s != null && s.isSupported && s.name != ErrorShaderName;

        private static Shader LitShader()
        {
            if (s_lit != null) return s_lit;
            Shader found = null;
            foreach (var name in LitChain)
            {
                var s = Shader.Find(name);
                if (Usable(s))
                {
                    found = s;
                    break;
                }
            }
            if (found == null)
            {
                var unlit = UnlitShader();
                if (Usable(unlit)) found = unlit;
            }
            if (found == null) found = FirstExisting(LitChain);
            if (found == null) found = UnlitShader();
            s_lit = found;
            return s_lit;
        }

        private static Shader UnlitShader()
        {
            if (s_unlit != null) return s_unlit;
            Shader found = null;
            foreach (var name in UnlitChain)
            {
                var s = Shader.Find(name);
                if (Usable(s))
                {
                    found = s;
                    break;
                }
            }
            if (found == null) found = FirstExisting(UnlitChain);
            if (found == null) found = Shader.Find(ErrorShaderName);
            s_unlit = found;
            return s_unlit;
        }

        private static Shader FirstExisting(string[] chain)
        {
            foreach (var name in chain)
            {
                var s = Shader.Find(name);
                if (s != null) return s;
            }
            return null;
        }
    }
}
