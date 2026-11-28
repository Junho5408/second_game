using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Origami.Runtime.Systems
{
    /// <summary>How <see cref="ModelFit"/> scales a model (ARCHITECTURE §14.7, DECISIONS 2026-11-28, <c>Art/ASSETS.md</c> "Fit").</summary>
    public enum FitMode
    {
        /// <summary>Per-axis scale so the bounds fill <see cref="FitSpec.Size"/> exactly (furniture whose work height matters).
        /// If one axis scale deviates more than <see cref="ModelFit.MaxAxisDeviation"/> from the mean of the three, the
        /// fit falls back to <see cref="Inside"/> (uniform), still bottom-centred on the target point.</summary>
        Box,

        /// <summary>One uniform scale: the largest that keeps the bounds inside <see cref="FitSpec.Size"/>
        /// (axes with size ≤ 0 are ignored).</summary>
        Inside,

        /// <summary>Uniform, so the bounds height equals <see cref="FitSpec.Value"/>.</summary>
        Height,

        /// <summary>Uniform scale <see cref="FitSpec.Value"/> (kit proportions, e.g. Kenney 0.2 m per unit).</summary>
        Scale,
    }

    /// <summary>A fit request: mode + target size (model-local axes, before the yaw) or value.</summary>
    public readonly struct FitSpec
    {
        public readonly FitMode Mode;
        public readonly Vector3 Size;
        public readonly float Value;

        private FitSpec(FitMode mode, Vector3 size, float value)
        {
            Mode = mode;
            Size = size;
            Value = value;
        }

        public static FitSpec Box(Vector3 size) => new FitSpec(FitMode.Box, size, 0f);

        public static FitSpec Inside(Vector3 size) => new FitSpec(FitMode.Inside, size, 0f);

        public static FitSpec Height(float height) => new FitSpec(FitMode.Height, Vector3.zero, height);

        public static FitSpec Scale(float scale) => new FitSpec(FitMode.Scale, Vector3.zero, scale);

        public override string ToString() => Mode + (Mode == FitMode.Box || Mode == FitMode.Inside ? Size.ToString("F3") : "(" + Value + ")");
    }

    /// <summary>
    /// Loads a model from <c>Resources</c> and fits it to a target box by its renderer bounds (ARCHITECTURE §14.7, C-195).
    /// <para>
    /// Result layout: a new wrapper GameObject (<paramref name="name"/>, child of the parent, local position = the target
    /// bottom centre, local rotation = yaw about +y, local scale = the fit scale) with the model instance as its only child,
    /// shifted so the bottom centre of the combined bounds sits on the wrapper origin (or, with <c>anchorOrigin</c>, the
    /// model's own origin stays there). Bounds are measured at scale 1 with the wrapper unparented at the world origin,
    /// so the parent's pose does not matter. Two refinement passes re-measure after scaling (child parts with
    /// rotations), so <see cref="FitMode.Box"/> fills the box to float precision.
    /// </para>
    /// <para>
    /// Never throws and never logs: a missing key, a model without renderers or with empty bounds, or an unexpected
    /// error returns null after destroying anything half-built, and the caller draws its code-built fallback.
    /// The loaded prefab, its shared meshes and materials are never modified. The wrapper is owned by the caller
    /// (destroyed with its parent).
    /// </para>
    /// </summary>
    public static class ModelFit
    {
        /// <summary>Allowed per-axis deviation of a <see cref="FitMode.Box"/> scale from the mean scale (±40 %, DECISIONS 2026-11-28).</summary>
        public const float MaxAxisDeviation = 0.4f;

        private const float MinSize = 1e-5f;
        private const int RefinePasses = 2;

        /// <summary><c>Resources.Load&lt;GameObject&gt;(key)</c>; null for a null/empty key or a missing asset.</summary>
        public static GameObject Load(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            try
            {
                var go = Resources.Load<GameObject>(key);
                return go != null ? go : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>Loads <paramref name="key"/> and fits it (see the class remarks). Null → use the fallback.</summary>
        public static GameObject Spawn(string key, Transform parent, string name, FitSpec fit, Vector3 bottomCentre, float yaw,
            bool anchorOrigin = false)
        {
            var prefab = Load(key);
            return prefab == null ? null : Place(prefab, parent, name, fit, bottomCentre, yaw, anchorOrigin);
        }

        /// <summary>Instantiates <paramref name="source"/> (a prefab or any GameObject) and fits it. Null on failure.</summary>
        public static GameObject Place(GameObject source, Transform parent, string name, FitSpec fit, Vector3 bottomCentre, float yaw,
            bool anchorOrigin = false)
        {
            if (source == null) return null;
            GameObject wrapper = null;
            try
            {
                wrapper = new GameObject(string.IsNullOrEmpty(name) ? source.name : name);
                var w = wrapper.transform;
                var inst = Object.Instantiate(source, w, false);
                inst.name = source.name;
                if (!TryWorldBounds(wrapper, out var b) || !(b.size.x >= 0f && b.size.y >= 0f && b.size.z >= 0f))
                {
                    DestroySafe(wrapper);
                    return null;
                }
                var scale = ScaleFor(fit, b.size, out bool uniformFallback);
                if (!Valid(scale))
                {
                    DestroySafe(wrapper);
                    return null;
                }
                var it = inst.transform;
                if (!anchorOrigin) it.localPosition -= BottomCentre(b);
                w.localScale = scale;

                for (int pass = 0; pass < RefinePasses; pass++)
                {
                    if (!TryWorldBounds(wrapper, out b)) break;
                    if (fit.Mode == FitMode.Box && !uniformFallback)
                    {
                        var s = w.localScale;
                        var corrected = new Vector3(
                            b.size.x > MinSize ? s.x * fit.Size.x / b.size.x : s.x,
                            b.size.y > MinSize ? s.y * fit.Size.y / b.size.y : s.y,
                            b.size.z > MinSize ? s.z * fit.Size.z / b.size.z : s.z);
                        if (Valid(corrected)) w.localScale = corrected;
                        if (!TryWorldBounds(wrapper, out b)) break;
                    }
                    if (!anchorOrigin) it.localPosition -= Divide(BottomCentre(b), w.localScale);
                }

                w.SetParent(parent, false);
                w.localPosition = bottomCentre;
                w.localRotation = Quaternion.Euler(0f, yaw, 0f);
                return wrapper;
            }
            catch (Exception)
            {
                DestroySafe(wrapper);
                return null;
            }
        }

        /// <summary>
        /// Scale for a model whose bounds at scale 1 are <paramref name="native"/> (see <see cref="FitMode"/>).
        /// <paramref name="uniformFallback"/> = a Box fit broke the ±40 % rule and was made uniform. Returns
        /// <c>Vector3.zero</c> if no finite positive scale exists.
        /// </summary>
        public static Vector3 ScaleFor(FitSpec fit, Vector3 native, out bool uniformFallback)
        {
            uniformFallback = false;
            switch (fit.Mode)
            {
                case FitMode.Box:
                {
                    float sx = Ratio(fit.Size.x, native.x), sy = Ratio(fit.Size.y, native.y), sz = Ratio(fit.Size.z, native.z);
                    if (sx <= 0f || sy <= 0f || sz <= 0f)
                    {
                        uniformFallback = true;
                        return Uniform(Inside(fit.Size, native));
                    }
                    float mean = (sx + sy + sz) / 3f;
                    if (Deviates(sx, mean) || Deviates(sy, mean) || Deviates(sz, mean))
                    {
                        uniformFallback = true;
                        return Uniform(Mathf.Min(sx, Mathf.Min(sy, sz)));
                    }
                    return new Vector3(sx, sy, sz);
                }
                case FitMode.Inside:
                    return Uniform(Inside(fit.Size, native));
                case FitMode.Height:
                    return Uniform(Ratio(fit.Value, native.y));
                case FitMode.Scale:
                    return Uniform(fit.Value > 0f && !float.IsInfinity(fit.Value) ? fit.Value : 0f);
                default:
                    return Vector3.zero;
            }
        }

        /// <summary>
        /// Combined world bounds of every renderer under <paramref name="root"/> (inactive children included). Mesh renderers
        /// are measured from <c>MeshFilter.sharedMesh.bounds</c> and the current transform matrix (the same AABB Unity
        /// computes, without depending on when the renderer refreshes its bounds). Skinned meshes are measured the same way,
        /// from <c>sharedMesh.bounds</c> (bind-pose vertices) and the SkinnedMeshRenderer transform — the bind pose by definition
        /// maps mesh space through that transform. <c>SkinnedMeshRenderer.bounds</c> is NOT used: it comes from the imported
        /// <c>localBounds</c>, which for the Quaternius FBX (×100 Lcl Scaling, cm file units) are 100× the drawn character
        /// (review ORIGAMI-019 F-1: 328.7 units vs a 3.3-unit mesh). False if there is no renderer with a mesh.
        /// </summary>
        public static bool TryWorldBounds(GameObject root, out Bounds bounds)
        {
            bounds = default;
            if (root == null) return false;
            bool any = false;
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                Bounds rb;
                if (r is MeshRenderer)
                {
                    var mf = r.GetComponent<MeshFilter>();
                    if (mf == null || mf.sharedMesh == null) continue;
                    rb = TransformBounds(mf.sharedMesh.bounds, r.transform.localToWorldMatrix);
                }
                else if (r is SkinnedMeshRenderer smr)
                {
                    if (smr.sharedMesh == null) continue;
                    rb = TransformBounds(smr.sharedMesh.bounds, smr.transform.localToWorldMatrix);
                }
                else
                {
                    rb = r.bounds;
                }
                if (!any) bounds = rb;
                else bounds.Encapsulate(rb);
                any = true;
            }
            return any;
        }

        /// <summary>Destroy in play mode, DestroyImmediate in edit mode (null-safe).</summary>
        public static void DestroySafe(Object o)
        {
            if (o == null) return;
            if (Application.isPlaying) Object.Destroy(o);
            else Object.DestroyImmediate(o);
        }

        private static Bounds TransformBounds(Bounds local, Matrix4x4 m)
        {
            var c = local.center;
            var e = local.extents;
            var b = new Bounds(m.MultiplyPoint3x4(c), Vector3.zero);
            for (int i = 0; i < 8; i++)
            {
                var corner = c + new Vector3((i & 1) == 0 ? -e.x : e.x, (i & 2) == 0 ? -e.y : e.y, (i & 4) == 0 ? -e.z : e.z);
                b.Encapsulate(m.MultiplyPoint3x4(corner));
            }
            return b;
        }

        private static Vector3 BottomCentre(Bounds b) => new Vector3(b.center.x, b.min.y, b.center.z);

        private static Vector3 Divide(Vector3 v, Vector3 s) =>
            new Vector3(s.x != 0f ? v.x / s.x : 0f, s.y != 0f ? v.y / s.y : 0f, s.z != 0f ? v.z / s.z : 0f);

        private static bool Deviates(float s, float mean) => s < mean * (1f - MaxAxisDeviation) || s > mean * (1f + MaxAxisDeviation);

        private static float Ratio(float target, float native)
        {
            if (!(target > 0f) || float.IsInfinity(target) || !(native > MinSize) || float.IsInfinity(native)) return 0f;
            return target / native;
        }

        private static float Inside(Vector3 size, Vector3 native)
        {
            float s = float.PositiveInfinity;
            float r;
            if ((r = Ratio(size.x, native.x)) > 0f) s = Mathf.Min(s, r);
            if ((r = Ratio(size.y, native.y)) > 0f) s = Mathf.Min(s, r);
            if ((r = Ratio(size.z, native.z)) > 0f) s = Mathf.Min(s, r);
            return float.IsInfinity(s) ? 0f : s;
        }

        private static Vector3 Uniform(float s) => s > 0f && !float.IsInfinity(s) && !float.IsNaN(s) ? new Vector3(s, s, s) : Vector3.zero;

        private static bool Valid(Vector3 s) =>
            s.x > 0f && s.y > 0f && s.z > 0f && !float.IsInfinity(s.x) && !float.IsInfinity(s.y) && !float.IsInfinity(s.z);
    }
}
