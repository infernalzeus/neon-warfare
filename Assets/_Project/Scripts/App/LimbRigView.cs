using UnityEngine;
using UnityEngine.UI;

namespace NW.App
{
    /// <summary>
    /// Shared limb-rig plumbing for UI previews outside the battlefield -- the troop-info
    /// modal's hero icon and the level-select demo lane -- that want the same continuous
    /// hip/knee bend BattlefieldView drives, instead of jump-cutting between pose frames.
    /// Construction mirrors BattlefieldView's own rig setup exactly (same pivot math, same
    /// parenting), so a preview built here lands in the same place a battlefield unit would.
    /// BattlefieldView's own rig code is left untouched; this is only for the two contexts
    /// that never had rig support at all.
    /// </summary>
    public static class LimbRigView
    {
        public static RawImage[] Build(RectTransform hostRt, string artId, bool isPlayer)
        {
            // hostRt.sizeDelta is only the actual pixel size for a fixed-size RectTransform.
            // BattlefieldView's own v.Rt (where this pivot math was copied from) is always
            // fixed-size, but the level-select demo lane's sprite node is stretch-anchored to
            // fill its parent -- there sizeDelta resolves to ~zero, which baked every limb at
            // zero size: present, correctly textured and positioned, but invisible. rect.size
            // is the actual resolved size either way.
            Vector2 size = hostRt.rect.size;
            var limbs = new RawImage[NeonArt.LimbSegCount];
            for (int li = 0; li < NeonArt.LimbSegCount; li++)
            {
                var info = NeonArt.LimbInfo(artId, li);
                var lgo = new GameObject("limb" + li);
                var parentRt = info.Parent >= 0 ? limbs[info.Parent].rectTransform : hostRt;
                lgo.transform.SetParent(parentRt, false);
                var lr = lgo.AddComponent<RectTransform>();
                var img = lgo.AddComponent<RawImage>();
                img.raycastTarget = false;
                limbs[li] = img;
                lr.sizeDelta = size;
                lr.pivot = info.Joint;
                lr.anchorMin = lr.anchorMax = new Vector2(0.5f, 0.5f);
                lr.anchoredPosition = info.Parent >= 0
                    ? new Vector2((info.Joint.x - NeonArt.LimbInfo(artId, info.Parent).Joint.x) * size.x,
                                  (info.Joint.y - NeonArt.LimbInfo(artId, info.Parent).Joint.y) * size.y)
                    : new Vector2((info.Joint.x - 0.5f) * size.x,
                                  (info.Joint.y - 0.5f) * size.y);
                lr.localRotation = Quaternion.identity;
                img.texture = NeonArt.UnitLimb(artId, isPlayer, li);
                img.enabled = false;
                if (info.Parent < 0)
                    lr.SetSiblingIndex(info.Near ? hostRt.childCount - 1 : 0);
            }
            return limbs;
        }

        public static void SetEnabled(RawImage[] limbs, bool on, Color color)
        {
            if (limbs == null) return;
            for (int i = 0; i < limbs.Length; i++)
            {
                if (limbs[i] == null) continue;
                limbs[i].enabled = on;
                if (on) limbs[i].color = color;
            }
        }

        /// <summary>Same curves as BattlefieldView.DriveLimbRig: a hip swing, a knee that folds
        /// only on the forward half of the stride, arms counter-swinging the legs. Pass a
        /// continuously increasing phase (radians); it doesn't need to wrap. artId selects the
        /// per-unit stride amplitude (NeonArt.RigSwingDeg) so a preview matches what the
        /// battlefield actually does for that unit.</summary>
        public static void Drive(RawImage[] limbs, float phase, bool isPlayer, string artId)
        {
            if (limbs == null) return;
            NeonArt.RigSwingDeg(artId, out float hipDeg, out float kneeDeg, out float armDeg);
            float mirr = isPlayer ? 1f : -1f;
            float a = phase;
            float hipN  =  Mathf.Cos(a) * hipDeg;
            float hipF  = -Mathf.Cos(a) * hipDeg;
            float kneeN = Mathf.Max(0f, -Mathf.Sin(a)) * kneeDeg;
            float kneeF = Mathf.Max(0f,  Mathf.Sin(a)) * kneeDeg;
            float armN  = -Mathf.Cos(a) * armDeg;
            float armF  =  Mathf.Cos(a) * armDeg;
            Set(limbs, 0, hipF * mirr); Set(limbs, 1, -kneeF * mirr); Set(limbs, 2, hipN * mirr);
            Set(limbs, 3, -kneeN * mirr); Set(limbs, 4, armF * mirr); Set(limbs, 5, armN * mirr);
        }

        static void Set(RawImage[] limbs, int i, float deg)
        {
            var img = limbs[i];
            if (img == null) return;
            img.rectTransform.localRotation = Quaternion.Euler(0f, 0f, deg);
        }

        // ── rotor rig (Probe) ───────────────────────────────────────────────────────────
        public static RawImage[] BuildRotors(RectTransform hostRt, string artId, bool isPlayer)
        {
            Vector2 size = hostRt.rect.size;   // see the note in Build() above
            var rotors = new RawImage[2];
            for (int side = 0; side < 2; side++)
            {
                var go = new GameObject("rotor" + side);
                go.transform.SetParent(hostRt, false);
                var rt = go.AddComponent<RectTransform>();
                var img = go.AddComponent<RawImage>();
                img.raycastTarget = false;
                rotors[side] = img;
                rt.sizeDelta = size;
                Vector2 hub = NeonArt.RotorHub01(side);
                rt.pivot = hub;
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = new Vector2((hub.x - 0.5f) * size.x, (hub.y - 0.5f) * size.y);
                img.texture = NeonArt.UnitRotor(artId, isPlayer, side);
                img.enabled = false;
            }
            return rotors;
        }

        /// <summary>Spins both rotors to the same absolute angle -- pass a continuously
        /// increasing value (e.g. Time.time * degreesPerSecond); it doesn't need to wrap.</summary>
        public static void SpinRotors(RawImage[] rotors, float angleDeg)
        {
            if (rotors == null) return;
            for (int i = 0; i < rotors.Length; i++)
                if (rotors[i]) rotors[i].rectTransform.localRotation = Quaternion.Euler(0f, 0f, angleDeg);
        }
    }
}
