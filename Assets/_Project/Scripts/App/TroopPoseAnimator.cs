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

        const float WalkSpan   = 2.4f;   // seconds of marching
        const float AttackSpan = 1.5f;   // windup + strike + recover
        const float IdleSpan   = 1.1f;   // settle before looping
        const float Cycle      = WalkSpan + AttackSpan + IdleSpan;

        RectTransform _rt;
        Vector2 _basePos;
        float   _clock;
        int     _lastPose = -99;

        void Awake()
        {
            _rt = (RectTransform)transform;
            _basePos = _rt.anchoredPosition;
        }

        void OnDisable()
        {
            _clock = 0f;
            _lastPose = -99;
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

            _clock += Time.unscaledDeltaTime;
            float t = _clock % Cycle;

            int   pose;
            float lean = 0f, lunge = 0f, hop = 0f;

            if (t < WalkSpan)
            {
                // march: alternate the stride frames on a steady gait, bob and lean like the field
                float ph = t * 5.0f;
                pose = Mathf.Sin(ph) > 0f ? 1 : 2;
                hop  = Mathf.Abs(Mathf.Sin(ph)) * 3.0f;
                lean = -3.5f;
            }
            else if (t < WalkSpan + AttackSpan && NeonArt.HasAttackPoses(ArtId))
            {
                float a = t - WalkSpan;
                if (a < 0.40f)        { float k = a / 0.40f;            pose = 3; lean =  3f * k; lunge = -5f * k; }
                else if (a < 0.68f)   { float k = (a - 0.40f) / 0.28f;  pose = 4; lean = -5f * k; lunge = 16f * k; }
                else                  { float k = 1f - Mathf.Min(1f, (a - 0.68f) / 0.5f);
                                        pose = 0; lean = -5f * k;       lunge = 16f * k; }
            }
            else
            {
                pose = 0;
            }

            if (pose != _lastPose)
            {
                _lastPose = pose;
                // Always the FULL texture here. The battlefield splits body/weapon across two
                // layers, but this preview is a single image -- asking for UnitBody would drop
                // the mech's cannon and the titan's barrels the moment the first pose landed.
                Target.texture = NeonArt.Unit(ArtId, IsPlayer, pose);
            }
            _rt.anchoredPosition = _basePos + new Vector2(lunge, hop);
            _rt.localRotation    = Quaternion.Euler(0f, 0f, lean);
        }
    }
}
