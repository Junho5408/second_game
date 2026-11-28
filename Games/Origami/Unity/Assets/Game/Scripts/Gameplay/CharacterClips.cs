using Origami.Runtime.Systems;
using UnityEngine;

namespace Origami.Runtime.Gameplay
{
    /// <summary>
    /// Deterministic Legacy clip driver for the character model (ARCHITECTURE §14.6, A2-5): every clip state has speed 0;
    /// <see cref="Tick"/> sets each state's time from dt, its weight and enabled flag, then calls <c>Animation.Sample()</c>.
    /// The engine re-evaluates the same times in its own animation update, so nothing advances twice and tests that call
    /// <c>ProcessFrame</c> with a fixed dt see exactly the same poses as a player.
    /// <para>
    /// Standing: <c>Idle</c> and <c>Walk</c> loop and cross-fade over <see cref="Crossfade"/> seconds (walk weight moves
    /// linearly toward 1 when the speed is above <see cref="WalkThreshold"/>, toward 0 otherwise). Walk playback rate =
    /// speed / <see cref="ClipGroundSpeed"/> clamped to [<see cref="MinRate"/>, <see cref="MaxRate"/>].
    /// Sitting: <c>SitDown</c> once from t = 0, then its last frame is held (ClampForever). Standing up switches straight
    /// back to Idle (Core moves the character off the chair in the same frame, see the Gameplay README).
    /// </para>
    /// </summary>
    internal sealed class CharacterClips
    {
        public const float Crossfade = 0.15f, WalkThreshold = 0.1f, MinRate = 0.5f, MaxRate = 2.5f, DefaultClipGroundSpeed = 1.5f;

        private readonly Animation _anim;
        private readonly AnimationState _idle, _walk, _sit;
        private float _idleTime, _walkTime, _sitTime, _walkWeight;
        private bool _sitting;

        private CharacterClips(Animation anim, AnimationState idle, AnimationState walk, AnimationState sit)
        {
            _anim = anim;
            _idle = idle;
            _walk = walk;
            _sit = sit;
            anim.playAutomatically = false;
            anim.Stop();
            foreach (AnimationState s in anim)
            {
                s.speed = 0f;
                s.enabled = false;
                s.weight = 0f;
            }
            idle.wrapMode = WrapMode.Loop;
            if (walk != null) walk.wrapMode = WrapMode.Loop;
            if (sit != null) sit.wrapMode = WrapMode.ClampForever;
            ClipGroundSpeed = DefaultClipGroundSpeed;
        }

        /// <summary>Driver for <paramref name="anim"/>; null if there is no Animation or no Idle clip.</summary>
        public static CharacterClips Create(Animation anim)
        {
            if (anim == null) return null;
            var idle = Find(anim, ArtKeys.ClipIdle);
            if (idle == null || !(idle.length > 0f)) return null;
            return new CharacterClips(anim, idle, Valid(Find(anim, ArtKeys.ClipWalk)), Valid(Find(anim, ArtKeys.ClipSitDown)));
        }

        public Animation Animation => _anim;

        public bool HasWalk => _walk != null;

        public bool HasSit => _sit != null;

        /// <summary>Ground speed (m/s) of the Walk clip at rate 1 (measured by <see cref="CharacterView"/>, default 1.5).</summary>
        public float ClipGroundSpeed { get; set; }

        /// <summary>Dominant clip: SitDown while sitting (if present), else Walk when its weight ≥ 0.5, else Idle.</summary>
        public string Current => _sitting && _sit != null ? ArtKeys.ClipSitDown : (_walkWeight >= 0.5f && _walk != null ? ArtKeys.ClipWalk : ArtKeys.ClipIdle);

        public float WalkWeight => _walkWeight;

        /// <summary>0..1 through SitDown while sitting (1 without a SitDown clip); 0 when standing.</summary>
        public float SitProgress => !_sitting ? 0f : (_sit == null ? 1f : Mathf.Clamp01(_sitTime / _sit.length));

        public float WalkRate(float speed)
        {
            float clipSpeed = ClipGroundSpeed > 0.05f ? ClipGroundSpeed : DefaultClipGroundSpeed;
            return Mathf.Clamp(speed / clipSpeed, MinRate, MaxRate);
        }

        /// <summary>One frame: <paramref name="dt"/> seconds (NaN/negative → 0), ground speed in m/s, sitting.</summary>
        public void Tick(float dt, float speed, bool sitting)
        {
            if (float.IsNaN(dt) || dt < 0f || float.IsInfinity(dt)) dt = 0f;
            if (float.IsNaN(speed) || speed < 0f || float.IsInfinity(speed)) speed = 0f;
            if (sitting)
            {
                if (!_sitting)
                {
                    _sitting = true;
                    _sitTime = 0f;
                }
                _walkWeight = 0f;
                if (_sit != null)
                {
                    _sitTime = Mathf.Min(_sit.length, _sitTime + dt);
                    Apply(_idle, 0f, 0f);
                    Apply(_walk, 0f, 0f);
                    Apply(_sit, _sitTime, 1f);
                }
                else
                {
                    _idleTime = Wrap(_idleTime + dt, _idle.length);
                    Apply(_idle, _idleTime, 1f);
                    Apply(_walk, 0f, 0f);
                }
            }
            else
            {
                if (_sitting)
                {
                    _sitting = false;
                    _walkWeight = 0f;
                    _idleTime = 0f;
                    _walkTime = 0f;
                }
                float target = speed > WalkThreshold && _walk != null ? 1f : 0f;
                _walkWeight = Mathf.MoveTowards(_walkWeight, target, Crossfade > 0f ? dt / Crossfade : 1f);
                _idleTime = Wrap(_idleTime + dt, _idle.length);
                if (_walk != null) _walkTime = Wrap(_walkTime + dt * WalkRate(speed), _walk.length);
                Apply(_sit, 0f, 0f);
                Apply(_idle, _idleTime, 1f - _walkWeight);
                Apply(_walk, _walkTime, _walkWeight);
            }
            _anim.Sample();
        }

        /// <summary>
        /// Samples one clip alone at <paramref name="normalizedTime"/> (0..1) — used for runtime measurements. The next
        /// <see cref="Tick"/> restores the driven state. False if the clip does not exist.
        /// </summary>
        public bool Pose(string clip, float normalizedTime)
        {
            var target = Find(_anim, clip);
            if (target == null) return false;
            foreach (AnimationState s in _anim)
            {
                if (s != target) Apply(s, 0f, 0f);
            }
            Apply(target, Mathf.Clamp01(normalizedTime) * target.length, 1f);
            _anim.Sample();
            return true;
        }

        private static void Apply(AnimationState s, float time, float weight)
        {
            if (s == null) return;
            s.speed = 0f;
            if (weight <= 0f)
            {
                s.weight = 0f;
                s.enabled = false;
                return;
            }
            s.enabled = true;
            s.weight = weight;
            s.time = time;
        }

        private static float Wrap(float t, float length)
        {
            if (!(length > 0f)) return 0f;
            t %= length;
            return t < 0f ? t + length : t;
        }

        private static AnimationState Valid(AnimationState s) => s != null && s.length > 0f ? s : null;

        /// <summary>State named <paramref name="name"/>, or ending in "|name" (robust to the import rename rule).</summary>
        private static AnimationState Find(Animation anim, string name)
        {
            if (anim == null || string.IsNullOrEmpty(name)) return null;
            var s = anim[name];
            if (s != null) return s;
            foreach (AnimationState st in anim)
            {
                if (st.name.EndsWith("|" + name, System.StringComparison.Ordinal)) return st;
            }
            return null;
        }
    }
}
