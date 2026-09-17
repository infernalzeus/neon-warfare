using UnityEngine;
using UnityEngine.UI;

namespace NW.App
{
    /// <summary>
    /// Drives a troop portrait through the real pose frames so a preview shows the unit ALIVE:
    /// idle, then a walk cycle, then a full attack (windup into strike) before settling.
    /// Uses the same baked textures and the same frame meaning as BattlefieldView, so what a
    /// player sees here is what the unit actually does on the battlefield.
    /// Falls back to a still frame for art ids without pose frames.
    /// </summary>
    public sealed class TroopPoseAnimator : MonoBehaviour
    {
        public RawImage Target;
        public string   ArtId = "";
        public bool     IsPlayer = true;
        // A stationary unit (Speed == 0, e.g. turret) should never fake-walk — that's what
        // made a "Sentry" icon visibly float around before settling. Idle/attack only.
        public bool     IsStationary = false;
        // Flying units (Probe, Talon) shouldn't bounce with a ground unit's walking bob --
        // that sharp up/down abs-sine step is what "hopping around like a pigeon" was
        // describing. They get a smooth continuous hover instead, same idea as the real
        // battlefield's isAir handling.
        public bool     IsAir = false;

        const float WalkSpan   = 2.4f;   // seconds of marching
        const float AttackSpan = 1.5f;   // windup + strike + recover
        const float IdleSpan   = 1.1f;   // settle before looping

        RectTransform _rt;
        Vector2 _basePos;
        float   _clock;
        int     _lastPose = -99;
        RawImage[] _limbs;
        bool    _rigOn;
        RawImage[] _rotors;
        bool    _rotorsOn;

        void Awake()
        {
            _rt = (RectTransform)transform;
            _basePos = _rt.anchoredPosition;
        }

        void OnDisable()
        {
            _clock = 0f;
            _lastPose = -99;
            if (_rigOn) { LimbRigView.SetEnabled(_limbs, false, Color.white); _rigOn = false; }
            if (_rotorsOn) { LimbRigView.SetEnabled(_rotors, false, Color.white); _rotorsOn = false; }
            if (_rt != null)
            {
                _rt.anchoredPosition = _basePos;
                _rt.localRotation    = Quaternion.identity;
            }
        }

        void Update()
        {
            if (Target == null || string.IsNullOrEmpty(ArtId)) return;
            if (!NeonArt.HasPoses(ArtId)) return;      // static art: leave the still frame alone

            // A stationary unit never marches, so skip the walk phase entirely rather than
            // idling through a fake 2.4s "walk" before it can even fire.
            float walkSpan = IsStationary ? 0f : WalkSpan;
            float cycle    = walkSpan + AttackSpan + IdleSpan;

            _clock += Time.unscaledDeltaTime;
            float t = _clock % cycle;

            // Rigged units get a continuous hip/knee bend for the walk phase instead of the
            // two-frame swap below -- the same rig BattlefieldView drives, ported so this
            // single-image preview shows the same motion the battlefield actually has.
            bool wantRig = t < walkSpan && NeonArt.HasLimbRig(ArtId);
            if (wantRig)
            {
                if (_limbs == null) _limbs = LimbRigView.Build(_rt, ArtId, IsPlayer);
                if (!_rigOn) { LimbRigView.SetEnabled(_limbs, true, Target.color); _rigOn = true; }
                if (_lastPose != -1)
                {
                    _lastPose = -1;
                    Target.texture = NeonArt.UnitNoLimbs(ArtId, IsPlayer);
                }
                float ph = t * 5.0f;
                LimbRigView.Drive(_limbs, ph, IsPlayer, ArtId);
                _rt.anchoredPosition = _basePos + new Vector2(0f, Mathf.Abs(Mathf.Sin(ph)) * 3.0f);
                _rt.localRotation    = Quaternion.Euler(0f, 0f, -3.5f);
                return;
            }
            if (_rigOn) { LimbRigView.SetEnabled(_limbs, false, Color.white); _rigOn = false; }

            // Continuously-spinning rotor instead of flipping between a couple of fixed blade
            // angles -- that discrete swap was the "going up and down repeatedly instead of
            // actually spinning" complaint. Runs for the whole cycle (idle/walk/attack), not
            // just the walk phase, since the blades don't stop just because the drone isn't
            // currently marching.
            bool rotorRig = NeonArt.HasRotorRig(ArtId);
            if (rotorRig)
            {
                if (_rotors == null) _rotors = LimbRigView.BuildRotors(_rt, ArtId, IsPlayer);
                if (!_rotorsOn) { LimbRigView.SetEnabled(_rotors, true, Target.color); _rotorsOn = true; }
                if (_lastPose != -2)
                {
                    _lastPose = -2;
                    Target.texture = NeonArt.UnitNoRotor(ArtId, IsPlayer);
                }
                LimbRigView.SpinRotors(_rotors, _clock * 900f);
            }
            else if (_rotorsOn) { LimbRigView.SetEnabled(_rotors, false, Color.white); _rotorsOn = false; }

            int   pose;
            float lean = 0f, lunge = 0f, hop = 0f;

            if (t < walkSpan)
            {
                // march: alternate the stride frames on a steady gait, bob and lean like the field
                float ph = t * 5.0f;
                // Strider holds its idle frame while moving instead of swapping legs -- same
                // "no walk pose, ride the bob/lean instead" direction as the battlefield.
                pose = ArtId == "cybmech" ? 0 : (Mathf.Sin(ph) > 0f ? 1 : 2);
                hop  = Mathf.Abs(Mathf.Sin(ph)) * 3.0f;
                lean = -3.5f;
            }
            else if (t < walkSpan + AttackSpan && NeonArt.HasAttackPoses(ArtId))
            {
                float a = t - walkSpan;
                if (a < 0.40f)        { float k = a / 0.40f;            pose = 3; lean =  3f * k; lunge = -5f * k; }
                else if (a < 0.68f)   { float k = (a - 0.40f) / 0.28f;  pose = 4; lean = -5f * k; lunge = 16f * k; }
                else                  { float k = 1f - Mathf.Min(1f, (a - 0.68f) / 0.5f);
                                        pose = 0; lean = -5f * k;       lunge = 16f * k; }
            }
            else
            {
                pose = 0;
            }

            if (pose != _lastPose && !rotorRig)
            {
                _lastPose = pose;
                // Always the FULL texture here. The battlefield splits body/weapon across two
                // layers, but this preview is a single image -- asking for UnitBody would drop
                // the mech's cannon and the titan's barrels the moment the first pose landed.
                Target.texture = NeonArt.Unit(ArtId, IsPlayer, pose);
            }
            if (IsAir)
            {
                // Smooth continuous hover, not the ground walk-bounce -- no lean either, a
                // hovering unit doesn't tip forward the way a marching one does.
                hop  = Mathf.Sin(_clock * 3f) * 2.2f;
                lean = 0f;
            }
            _rt.anchoredPosition = _basePos + new Vector2(lunge, hop);
            _rt.localRotation    = Quaternion.Euler(0f, 0f, lean);
        }
    }
}
