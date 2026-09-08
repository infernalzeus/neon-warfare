using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace NW.App
{
    /// <summary>
    /// Static tooltip panel with cyberpunk flavor text for all game elements.
    /// Call Init() once when the canvas is built. Call Show()/Hide() from TooltipTarget.
    /// </summary>
    public static class TooltipSystem
    {
        static GameObject    _go;
        static RectTransform _rt;
        static Text          _titleTxt;
        static Text          _bodyTxt;
        static Canvas        _canvas;

        public static void Init(Font font, Transform canvasRoot)
        {
            if (_go != null) Object.Destroy(_go);

            _canvas = canvasRoot.GetComponent<Canvas>()
                   ?? canvasRoot.GetComponentInParent<Canvas>();

            _go = new GameObject("[Tooltip]");
            _go.transform.SetParent(canvasRoot, false);
            _go.transform.SetAsLastSibling();

            _rt = _go.AddComponent<RectTransform>();
            _rt.anchorMin = _rt.anchorMax = new Vector2(0.5f, 0.5f);
            _rt.pivot = new Vector2(0f, 1f);
            _rt.sizeDelta = new Vector2(290, 72);
            // Never block pointer events — tooltip appearing near cursor must not fire OnPointerExit on the target below
            var cg = _go.AddComponent<CanvasGroup>();
            cg.blocksRaycasts = false;
            cg.interactable   = false;

            // Shadow
            var shadowGo = new GameObject("shadow");
            shadowGo.transform.SetParent(_go.transform, false);
            shadowGo.transform.SetAsFirstSibling();
            var shadowRt = shadowGo.AddComponent<RectTransform>();
            shadowRt.anchorMin = Vector2.zero; shadowRt.anchorMax = Vector2.one;
            shadowRt.offsetMin = new Vector2(-3, -4); shadowRt.offsetMax = new Vector2(4, 3);
            shadowGo.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.65f);

            // Cyan neon border
            var bdrGo = new GameObject("border");
            bdrGo.transform.SetParent(_go.transform, false);
            bdrGo.transform.SetAsFirstSibling();
            var bdrRt = bdrGo.AddComponent<RectTransform>();
            bdrRt.anchorMin = Vector2.zero; bdrRt.anchorMax = Vector2.one;
            bdrRt.offsetMin = new Vector2(-1, -1); bdrRt.offsetMax = new Vector2(1, 1);
            bdrGo.AddComponent<Image>().color = new Color(0.0f, 0.85f, 1.0f, 0.70f);

            // Background
            _go.AddComponent<Image>().color = new Color(0.03f, 0.06f, 0.12f, 0.97f);

            // Title text
            var titleGo = new GameObject("title");
            titleGo.transform.SetParent(_go.transform, false);
            var titleRt = titleGo.AddComponent<RectTransform>();
            titleRt.anchorMin = new Vector2(0f, 0.52f); titleRt.anchorMax = new Vector2(1f, 1f);
            titleRt.offsetMin = new Vector2(10, 0); titleRt.offsetMax = new Vector2(-8, -4);
            _titleTxt = titleGo.AddComponent<Text>();
            _titleTxt.font = font; _titleTxt.fontSize = 12; _titleTxt.fontStyle = FontStyle.Bold;
            _titleTxt.color = new Color(0.15f, 0.95f, 1.0f);
            _titleTxt.alignment = TextAnchor.LowerLeft;
            _titleTxt.raycastTarget = false;

            // Divider
            var divGo = new GameObject("div");
            divGo.transform.SetParent(_go.transform, false);
            var divRt = divGo.AddComponent<RectTransform>();
            divRt.anchorMin = new Vector2(0.02f, 0.5f); divRt.anchorMax = new Vector2(0.98f, 0.5f);
            divRt.sizeDelta = new Vector2(0, 1);
            divGo.AddComponent<Image>().color = new Color(0f, 0.6f, 0.75f, 0.4f);

            // Body text
            var bodyGo = new GameObject("body");
            bodyGo.transform.SetParent(_go.transform, false);
            var bodyRt = bodyGo.AddComponent<RectTransform>();
            bodyRt.anchorMin = new Vector2(0f, 0f); bodyRt.anchorMax = new Vector2(1f, 0.52f);
            bodyRt.offsetMin = new Vector2(10, 4); bodyRt.offsetMax = new Vector2(-8, 0);
            _bodyTxt = bodyGo.AddComponent<Text>();
            _bodyTxt.font = font; _bodyTxt.fontSize = 10;
            _bodyTxt.color = new Color(0.72f, 0.82f, 0.92f);
            _bodyTxt.alignment = TextAnchor.UpperLeft;
            _bodyTxt.raycastTarget = false;

            _go.SetActive(false);
        }

        public static void Show(string title, string body, Vector2 screenPos)
        {
            if (_go == null || _canvas == null) return;
            _titleTxt.text = title;
            _bodyTxt.text  = body;

            int estimatedLines = Mathf.Max(1, Mathf.CeilToInt(body.Length / 34f));
            _rt.sizeDelta = new Vector2(290, 44 + estimatedLines * 16);

            var canvasRt = _canvas.GetComponent<RectTransform>();
            Vector2 local;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRt, screenPos, null, out local);
            local += new Vector2(20, 8);

            float hw = canvasRt.rect.width * 0.5f, hh = canvasRt.rect.height * 0.5f;
            float tw = _rt.sizeDelta.x, th = _rt.sizeDelta.y;
            local.x = Mathf.Clamp(local.x, -hw, hw - tw);
            local.y = Mathf.Clamp(local.y, -hh + th, hh);

            _rt.anchoredPosition = local;
            _go.SetActive(true);
        }

        public static void Hide()
        {
            if (_go != null) _go.SetActive(false);
        }

        // ──────────────────────────────── flavor text ─────────────────────

        public static (string title, string body) TroopTooltip(string id)
        {
            string displayName = ThemeLocale.TroopName(id);
            int theme = GameSettings.ThemeIndex;
            string body = theme == 2 ? BiopunkTroopBody(id)
                        : theme == 3 ? MedievalTroopBody(id)
                        : theme == 4 ? IndustrialTroopBody(id)
                        : theme == 6 ? SolarTroopBody(id)
                        : theme == 7 ? DawnTroopBody(id)
                        : CyberpunkTroopBody(id);
            return (displayName, body);
        }

        /// <summary>
        /// Theme-aware description of what the attack actually looks like on screen. The base
        /// text in TroopStats describes the science-fiction art ("swept delta wings", "scope
        /// glow"), which is simply wrong once a theme has its own models -- a Rogue on a bat-wing
        /// glider was being described as a delta-wing strafing run. Themes without their own art
        /// keep the base text, because for them it is still accurate.
        /// </summary>
        public static string TroopAttackAnim(string id)
        {
            if (GameSettings.ThemeIndex == 3)
            {
                switch (id)
                {
                    case "drone":       return "Wings beat, body tucks into a dive, scroll-case swings as it passes overhead.";
                    case "trooper":     return "Greatsword hauls back over the shoulder, then arcs through in a full overhead cut; pauldron turns with the arm.";
                    case "sniper":      return "Bow comes up, string draws back to the cheek, arrow leaves on the release and the limbs snap forward.";
                    case "mech":        return "Trebuchet arm winds back past vertical, counterweight drops, and the sling whips the payload down-lane.";
                    case "shield-bot":  return "Tower shield drives forward off the front foot; the mace swings out from behind it a beat later.";
                    case "interceptor": return "Banks on the wing membrane and throws a pair of blades across the lane without breaking its glide.";
                    case "hacker":      return "Staff lifts and tilts, the bound orb flares, and arcane bolts trail out toward the target.";
                    case "titan":       return "Shoulder drops, the whole torso rotates, and a boulder fist comes across in a wide arc that lights the runes.";
                    case "turret":      return "Bow arms flex back against the cord, then snap through and the bolt leaves as the stock recoils.";
                }
            }
            if (GameSettings.ThemeIndex == 4)
            {
                switch (id)
                {
                    case "drone":       return "Ducted fan spins up, the hull tips forward, and it strafes past trailing exhaust.";
                    case "trooper":     return "Rivet gun swings up and kicks back hard on the shot, hopper rattling on the recoil.";
                    case "sniper":      return "Braces the tube against a bolted shoulder plate, fires, and vents backblast out the rear.";
                    case "mech":        return "Lattice boom swings across and the hook block drops through whatever is under it.";
                    case "shield-bot":  return "Drives the blast door forward on hydraulic legs, then jabs once with the pry bar.";
                    case "interceptor": return "Crankshaft turns, the wings beat once hard, and the nose gun opens up mid-pass.";
                    case "hacker":      return "Generator winds up, the coil rings charge, and a forked arc jumps to the target.";
                    case "titan":       return "Firebox flares behind the grate, both hydraulic rams extend, and the fists come through.";
                    case "turret":      return "Barrel cluster spins to speed, the chain feed pulls, and it fires until told to stop.";
                }
            }
            if (GameSettings.ThemeIndex == 2)
            {
                switch (id)
                {
                    case "drone":       return "Gill skirt flares, the bladder contracts, and a spore burst drops onto whatever is below.";
                    case "trooper":     return "Throat sac lights, the overgrown arm hauls back past the shoulder, then the bone club comes down.";
                    case "sniper":      return "Abdomen inflates to charge, the proboscis telescopes out five segments, and it spits.";
                    case "mech":        return "Bile sac swells, the sphincter nozzle opens, and it sprays a stream down the lane.";
                    case "shield-bot":  return "Leans the fungus shelf into the blow, then jabs once with the bone spur from behind it.";
                    case "interceptor": return "Wings beat out of phase to hold station while the tail curls under the body and stabs.";
                    case "hacker":      return "Fruiting sacs swell and tendrils creep out along the floor to reach the target.";
                    case "titan":       return "Brood sac pulses, both mandible arms open wide, then close on whatever is in front.";
                    case "turret":      return "Seed bulb swells against its seams, then splits along both valves and launches spikes.";
                }
            }
            if (GameSettings.ThemeIndex == 6)
            {
                // Solar runs the Medieval geometry under a brass and molten art set, so the
                // motion is identical -- only the material language changes.
                switch (id)
                {
                    case "drone":       return "Wings flare, body tucks into a dive, embers trailing behind it.";
                    case "trooper":     return "Sun-forged blade hauls back, then arcs through in a single overhead cut.";
                    case "sniper":      return "Focusing lens comes up, the beam gathers, then releases in one bright line.";
                    case "mech":        return "Throwing arm winds past vertical and the bellows drop the payload down-lane.";
                    case "shield-bot":  return "Sun-disc shield drives forward; the mace follows a beat behind it.";
                    case "interceptor": return "Banks on burning wings and throws a pair of molten blades across the lane.";
                    case "hacker":      return "Brazier staff lifts, the flame gutters, and fire bolts trail out toward the target.";
                    case "titan":       return "Torso rotates and a bronze fist comes across in a wide arc, molten veins flaring.";
                    case "turret":      return "Mirror arms flex, then snap through as the bolt leaves and the mount recoils.";
                }
            }
            if (GameSettings.ThemeIndex == 4 && id == "trooper")
                return "Rivet gun swings up, kicks back hard on the shot, hopper rattling on the recoil.";
            if (GameSettings.ThemeIndex == 7 && id == "trooper")
                return "Staff lifts and the hung lantern flares, throwing light down the lane ahead of the strike.";
            return TroopStats.TryGet(id, out var info) ? info.AttackAnim : "";
        }

        static string CyberpunkTroopBody(string id) => id switch
        {
            "drone"       => "Cheap. Fast. Expendable. They asked for danger pay. We gave them more drones.",
            "trooper"     => "Neural-linked infantry. Augmented reflexes, zero hesitation. The backbone of your assault.",
            "sniper"      => "One shot, one kill at maximum range. Very dramatic. Very effective. They never see it coming.",
            "mech"        => "4 tons of hydraulic fury. Slow enough that enemies have time to seriously regret their choices.",
            "shield-bot"  => "The bullet magnet your squad needs. Loves taking hits so your fragile units don't have to.",
            "interceptor" => "Delta-wing hunter-killer. Dominates the airspace. Hates being grounded. Or ignored.",
            "hacker"      => "No weapon. Just code. Corrupts enemy targeting from extreme range. Your enemies will be confused.",
            "titan"       => "500HP walking apocalypse. Shoulder-mounted doom cannons. Unlocked at clearance level 15.",
            "turret"      => "Stays where planted. Dual barrels, 360° arc. Excellent for holding pylons. Zero morale issues.",
            _             => "Combat unit. Fights enemies. Doesn't ask questions. Has its own perspective on overtime.",
        };

        static string MedievalTroopBody(string id) => id switch
        {
            "drone"       => "Faster than a scroll, cheaper than a knight. Carries messages and mild disappointment.",
            "trooper"     => "Stalwart. Loyal. Armour dented but pride intact. The backbone of any decent siege.",
            "sniper"      => "Drops enemies from distance with alarming accuracy. Hates melee. Loves high ground.",
            "mech"        => "Rolls forward with geological patience. Also explodes walls, which is considered a bonus.",
            "shield-bot"  => "Holy warrior in service of the realm. Takes hits so your fragile units don't have to.",
            "interceptor" => "Fast. Quiet. Stabby. Acts before enemies realise they should be worried.",
            "hacker"      => "No sword. Just terrible spells at extreme range. Enemies will be confused and briefly on fire.",
            "titan"       => "Stone and dark magic. 500HP of slow, unstoppable bad intentions. Unlocked at keep level 15.",
            "turret"      => "Stays planted. Launches oversized bolts at high velocity. Zero morale issues.",
            _             => "Combat unit. Fights enemies. Has its own perspective on chivalry.",
        };

        static string BiopunkTroopBody(string id) => id switch
        {
            "drone"       => "A floating organ with opinions. Drifts over, opens its gills, ruins someone's day.",
            "trooper"     => "One arm did all the growing. The other gave up. Only one of them needed to work.",
            "sniper"      => "Inflates, aims something that is technically a mouth, and spits with alarming accuracy.",
            "mech"        => "Six legs, one shell, and a sac full of something you do not want on you.",
            "shield-bot"  => "Grew its own wall out of one side. Stands there. Very hard to argue with.",
            "interceptor" => "Four wings, no patience, and a tail that arrives before the rest of it does.",
            "hacker"      => "No head, no hands. Reaches along the floor instead. Enemies notice far too late.",
            "titan"       => "500HP of brood chamber on legs. You can see the next wave moving around inside it.",
            "turret"      => "Rooted, patient, and full of spikes. Swells up before it lets go.",
            _             => "Grown unit. Fights enemies. Was probably something smaller last week.",
        };

        static string SolarTroopBody(string id) => id switch
        {
            "drone"       => "Carries embers instead of messages. Arrives hot, leaves hotter.",
            "trooper"     => "Sun-forged plate, brass to the boots. Marches like the light is on its side.",
            "sniper"      => "Focuses daylight through a lens until something on the other end stops moving.",
            "mech"        => "Bellows-driven and geological. Throws things that are still burning on landing.",
            "shield-bot"  => "Carries a sun disc and stands in front of everyone. Absorbs a great deal.",
            "interceptor" => "Burning wings, molten blades, no patience. Gone before the heat arrives.",
            "hacker"      => "No blade. A brazier and some very poor intentions at extreme range.",
            "titan"       => "Bronze with molten veins. 500HP of slow, inevitable sunrise.",
            "turret"      => "Planted. Aims a mirror array down the lane and does not blink.",
            _             => "Forged unit. Fights enemies. Runs hot.",
        };

        static string DawnTroopBody(string id) => id switch
        {
            "drone"       => "A mote of light on an errand. Faster than anything carrying armour.",
            "trooper"     => "A traveller with a lantern and a staff. Has walked further than your supply line.",
            "sniper"      => "Draws a bow strung with light. Patient in a way that unsettles people.",
            "mech"        => "A draped walker carrying more than it should. Slow, and entirely unbothered.",
            "shield-bot"  => "Raises a wall of light and simply stands in it. Nothing gets past politely.",
            "interceptor" => "Cloth wings, no engine. Crosses lanes on a thermal and a good attitude.",
            "hacker"      => "Reads sigils at range until the enemy forgets what it was aiming at.",
            "titan"       => "A robed colossus. Moves at the pace of weather and hits like it too.",
            "turret"      => "A standing stone with a light in it. Has been here longer than the war.",
            _             => "Wandering unit. Fights when it must. Would rather be walking.",
        };

        static string IndustrialTroopBody(string id) => id switch
        {
            "drone"       => "Small. Expendable. The foreman says there's more where they came from.",
            "trooper"     => "Factory worker. Conscripted, armed, given a helmet. Probably fine.",
            "sniper"      => "Maximum range. Minimum talking. Very effective at both.",
            "mech"        => "A crane on tracks. Swings a weight on a cable at people. Nobody signed off on this.",
            "shield-bot"  => "Armoured walking bulwark. Absorbs punishment without complaint or overtime.",
            "interceptor" => "Mechanical wings, rotating gatling. Patrols the upper lanes.",
            "hacker"      => "Disrupts enemy communications. Dressed like an electrician. Is not one.",
            "titan"       => "Factory-forged walker. Slow enough for enemies to regret. Loud enough to hear coming.",
            "turret"      => "Fires continuously until ordered to stop or the coal runs out.",
            _             => "Industrial unit. Does the job. Doesn't ask questions. Eligible for hazard pay.",
        };

        public static (string title, string body) GemTooltip(int kind)
        {
            int theme     = GameSettings.ThemeIndex;
            string abbrev = ThemeLocale.GemAbbrev[Mathf.Clamp(kind, 0, 4)];
            string name   = ThemeLocale.GemName(kind);
            string body   = theme == 3 ? MedievalGemBody(kind)
                          : theme == 4 ? IndustrialGemBody(kind)
                          : CyberpunkGemBody(kind);
            return ($"{abbrev}  —  {name}", body);
        }

        static string CyberpunkGemBody(int kind) => kind switch
        {
            0 => "Raw grid power. Tapped directly from the city's neural net. Common, essential, combustible.",
            1 => "Superheated ions from collapsed stars. Your mechs run on this. Your enemies bleed it.",
            2 => "Microscopic warfare at the molecular level. Power the bots that maintain the bots that break things.",
            3 => "Entangled-state matter. Unstable, rare, terrifying. Required for elite units. Worth every risk.",
            4 => "Encrypted intel fragments. The true currency of the cyber-age. More valuable than blood. Barely.",
            _ => "Unclassified material. Handle with extreme caution and a little bit of curiosity.",
        };

        static string MedievalGemBody(int kind) => kind switch
        {
            0 => "Gathered from the forest. Builds everything. Available until the forests run out, which is soon.",
            1 => "Smelted from ore. Heavy and reliable. Makes good swords and occasionally terrible decisions.",
            2 => "Quarried from the hills. Foundation of walls, towers, and unreasonable stubbornness.",
            3 => "The root of all military campaigns. More of this and the king is pleased. Less, and he is not.",
            4 => "Ancient magic. Rare and volatile. Required for things wizards point at and then run from.",
            _ => "Unclassified resource. Handle with caution and a prayer to whichever saint is relevant.",
        };

        static string IndustrialGemBody(int kind) => kind switch
        {
            0 => "Liquid capital. Pays for coal, labour, and liability claims from the last shift.",
            1 => "Industrial-grade alloy component. Heavy, useful, gives engineers chronic back problems.",
            2 => "Wiring, pipes, conductors. The economy runs on copper. Literally and structurally.",
            3 => "Rare metal for precision machinery. Extremely blue. Extremely expensive. Worth it.",
            4 => "Glass substrate. Used in everything that sparks, calculates, or shatters under pressure.",
            _ => "Unclassified material. Log it, label it, don't ask where it came from.",
        };

        public static (string title, string body) LaneTooltip(int lane) => lane switch
        {
            0 => ("LANE 1", "Forward assault corridor. First blood, first casualty. Tap a card, then this lane to deploy."),
            1 => ("LANE 2", "Secondary ground lane. Flanking route preferred by Troopers and fast assault units."),
            2 => ("LANE 3", "Mid-field pressure lane. Heavy units shine here. Capture the pylons to build control."),
            3 => ("LANE 4", "Deep strike corridor. High-risk, high-reward. Breakthrough here ends the fight fast."),
            4 => ("AIR LANE", "Aerial combat zone above all ground lanes. Only air units operate here."),
            _ => ("LANE",        "Combat lane. Capture pylons. Destroy the enemy core."),
        };
    }

    /// <summary>
    /// Attach to any UI element to show a tooltip. Desktop (mouse): on hover. Touch: on
    /// long-press (hold ~0.4s), since touch has no hover. Both paths coexist for the
    /// responsive-both build — chosen by pointerId (mouse &lt; 0, touch &gt;= 0).
    /// </summary>
    public sealed class TooltipTarget : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        public string Title;
        public string Body;

        const float LongPressDelay = 0.4f;
        Coroutine _pressRoutine;
        bool _shown;

        public void OnPointerEnter(PointerEventData e)
        {
            if (e.pointerId < 0) { TooltipSystem.Show(Title, Body, e.position); _shown = true; } // mouse hover
        }

        public void OnPointerExit(PointerEventData e)
        {
            if (e.pointerId < 0 && _shown) { TooltipSystem.Hide(); _shown = false; }
        }

        public void OnPointerDown(PointerEventData e)
        {
            if (e.pointerId >= 0) _pressRoutine = StartCoroutine(LongPress(e.position)); // touch long-press
        }

        public void OnPointerUp(PointerEventData e)
        {
            if (_pressRoutine != null) { StopCoroutine(_pressRoutine); _pressRoutine = null; }
            if (_shown) { TooltipSystem.Hide(); _shown = false; }
        }

        IEnumerator LongPress(Vector2 pos)
        {
            yield return new WaitForSecondsRealtime(LongPressDelay);
            TooltipSystem.Show(Title, Body, pos);
            _shown = true;
            _pressRoutine = null;
        }
    }
}
